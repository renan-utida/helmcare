using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Ocorrencias;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Servicos.Ocorrencias;

/// <summary>
/// Ações sobre a ocorrência (seção 7): o aceite de um voluntário, o cancelamento pelo entregador e o
/// encerramento. As regras ficam na <see cref="Ocorrencia"/> (máquina de estados, quem pode aceitar e
/// o indicador); aqui ficam a gravação, a nova tentativa do aceite e o comando ao capacete, publicado
/// depois de gravar e sem bloquear a rota.
/// </summary>
public sealed partial class AtendimentoOcorrenciaServico
{
    private readonly IOcorrenciaRepositorio _ocorrencias;
    private readonly IVoluntarioRepositorio _voluntarios;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly PublicadorComandosOcorrencia _comandos;
    private readonly OcorrenciaServico _consulta;
    private readonly IAutorizacaoRecursos _autorizacao;
    private readonly TimeProvider _relogio;
    private readonly double _raioCamada1Km;
    private readonly TimeSpan _trechoCapacetePadrao;
    private readonly ILogger<AtendimentoOcorrenciaServico> _logger;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="ocorrencias">Repositório de ocorrências.</param>
    /// <param name="voluntarios">Repositório de voluntários.</param>
    /// <param name="unidadeDeTrabalho">Grava e, na nova tentativa do aceite, descarta o que está rastreado.</param>
    /// <param name="comandos">Publicação dos comandos da ocorrência sem bloquear.</param>
    /// <param name="consulta">Monta o corpo do <c>GET /ocorrencias/{id}</c>.</param>
    /// <param name="autorizacao">Quem pode aceitar, cancelar e encerrar.</param>
    /// <param name="relogio">Relógio do servidor (instante do aceite e do fim).</param>
    /// <param name="opcoes">Constantes do contrato (<c>RAIO_CAMADA_1_KM</c> e o trecho padrão do capacete).</param>
    /// <param name="logger">Log da aplicação.</param>
    public AtendimentoOcorrenciaServico(
        IOcorrenciaRepositorio ocorrencias,
        IVoluntarioRepositorio voluntarios,
        IUnidadeDeTrabalho unidadeDeTrabalho,
        PublicadorComandosOcorrencia comandos,
        OcorrenciaServico consulta,
        IAutorizacaoRecursos autorizacao,
        TimeProvider relogio,
        IOptions<OpcoesHelmCare> opcoes,
        ILogger<AtendimentoOcorrenciaServico> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _ocorrencias = ocorrencias;
        _voluntarios = voluntarios;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _comandos = comandos;
        _consulta = consulta;
        _autorizacao = autorizacao;
        _relogio = relogio;
        _raioCamada1Km = opcoes.Value.RaioCamada1Km;
        _trechoCapacetePadrao = opcoes.Value.TrechoCapacetePadrao;
        _logger = logger;
    }

    /// <summary>
    /// Registra o aceite do voluntário, grava e publica <c>SOCORRO_A_CAMINHO</c> no capacete da vítima.
    /// </summary>
    /// <remarks>
    /// <para>Conflito de concorrência na gravação: o <c>xmin</c> da ocorrência muda tanto com outro
    /// aceite quanto com o escalonamento para a camada 2, que roda em segundo plano a cada segundo. No
    /// segundo caso o aceite é legítimo (a camada 2 também aceita) e o voluntário não deve receber 409
    /// por azar de tempo. Por isso há uma única nova tentativa: descarta o que está rastreado, relê a
    /// ocorrência e, se ela ainda aguarda aceite, aceita de novo; se não aguarda mais (outro voluntário
    /// aceitou antes ou ela terminou), 409. Um segundo conflito seguido sobe e vira 409.</para>
    /// <para>A violação do índice único de <c>AceiteSocorro.OcorrenciaId</c> também vira 409, no tratador.</para>
    /// </remarks>
    /// <param name="ocorrenciaId">Ocorrência aceita.</param>
    /// <param name="voluntarioId">Voluntário que aceita.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O corpo do <c>GET /ocorrencias/{id}?voluntarioId=</c> desse voluntário (localização exata).</returns>
    /// <exception cref="RecursoNaoEncontradoException">Ocorrência ou voluntário inexistente (404).</exception>
    /// <exception cref="AcessoNegadoException">O voluntário não é o do token (403).</exception>
    /// <exception cref="RegraNegocioException">O voluntário não pode atender, ou outro aceitou antes (409).</exception>
    /// <exception cref="TransicaoInvalidaException">A ocorrência não aguarda aceite (409).</exception>
    public async Task<OcorrenciaResposta> AceitarAsync(Guid ocorrenciaId, Guid voluntarioId, CancellationToken cancellationToken)
    {
        (Ocorrencia ocorrencia, Voluntario voluntario) = await CarregarParaAceiteAsync(ocorrenciaId, voluntarioId, cancellationToken);

        // Só o voluntário do próprio entregador aceita (403 depois dos dois 404).
        _autorizacao.ExigirDono(voluntario.EntregadorId);

        ocorrencia.Aceitar(voluntario, Agora(), _raioCamada1Km, _trechoCapacetePadrao);

        try
        {
            await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            RegistrarNovaTentativa(_logger, ocorrenciaId);
            _unidadeDeTrabalho.DescartarAlteracoes();

            (ocorrencia, voluntario) = await CarregarParaAceiteAsync(ocorrenciaId, voluntarioId, cancellationToken);

            if (!ocorrencia.EstaAguardandoAceite)
            {
                throw new RegraNegocioException(
                    "A ocorrência já não aguarda aceite: outro voluntário aceitou antes ou ela terminou.");
            }

            ocorrencia.Aceitar(voluntario, Agora(), _raioCamada1Km, _trechoCapacetePadrao);
            await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        }

        RegistrarAceite(_logger, ocorrencia.Id, voluntarioId);
        await _comandos.PublicarSemBloquearAsync(ocorrencia, TipoComandoMqtt.SocorroACaminho, cancellationToken);

        return await _consulta.MontarRespostaAsync(ocorrencia, voluntarioId, cancellationToken);
    }

    /// <summary>
    /// O entregador informou que está bem: cancela o SOS (só em <c>SOS_CAMADA_1</c> ou
    /// <c>SOS_CAMADA_2</c>), grava e publica <c>ENCERRAR</c>.
    /// </summary>
    /// <param name="ocorrenciaId">Ocorrência cancelada.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O corpo do <c>GET /ocorrencias/{id}</c>, com a localização aproximada.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Ocorrência inexistente (404).</exception>
    /// <exception cref="AcessoNegadoException">Quem pede não é a vítima (403).</exception>
    /// <exception cref="TransicaoInvalidaException">Fora da espera de aceite (409).</exception>
    public Task<OcorrenciaResposta> CancelarAsync(Guid ocorrenciaId, CancellationToken cancellationToken)
    {
        return FinalizarAsync(
            ocorrenciaId,
            (o, _) =>
            {
                _autorizacao.ExigirDono(o.EntregadorId);
                return Task.CompletedTask;
            },
            (o, agora) => o.Cancelar(agora),
            cancellationToken);
    }

    /// <summary>
    /// Encerra a ocorrência, com ou sem aceite (sem aceite, o indicador fica nulo), grava e publica <c>ENCERRAR</c>.
    /// </summary>
    /// <param name="ocorrenciaId">Ocorrência encerrada.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O corpo do <c>GET /ocorrencias/{id}</c>, com a localização aproximada.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Ocorrência inexistente (404).</exception>
    /// <exception cref="AcessoNegadoException">Quem pede não é a vítima nem o voluntário que aceitou (403).</exception>
    /// <exception cref="TransicaoInvalidaException">Ocorrência já encerrada ou cancelada (409).</exception>
    public Task<OcorrenciaResposta> EncerrarAsync(Guid ocorrenciaId, CancellationToken cancellationToken)
    {
        return FinalizarAsync(
            ocorrenciaId,
            _autorizacao.ExigirEncerramentoAsync,
            (o, agora) => o.Encerrar(agora),
            cancellationToken);
    }

    // Cancelar e encerrar só diferem em quem pode e na transição do domínio; o resto (404, depois a
    // conferência de quem pede, gravar, ENCERRAR sem bloquear e a resposta sem voluntário) é igual.
    private async Task<OcorrenciaResposta> FinalizarAsync(
        Guid ocorrenciaId,
        Func<Ocorrencia, CancellationToken, Task> autorizar,
        Action<Ocorrencia, DateTime> transicao,
        CancellationToken cancellationToken)
    {
        Ocorrencia ocorrencia = await _ocorrencias.ObterAsync(ocorrenciaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Ocorrência não encontrada.");

        await autorizar(ocorrencia, cancellationToken);
        transicao(ocorrencia, Agora());
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        string estadoFinal = NomeContrato.Para(ocorrencia.Estado);
        RegistrarFim(_logger, ocorrencia.Id, estadoFinal);
        await _comandos.PublicarSemBloquearAsync(ocorrencia, TipoComandoMqtt.Encerrar, cancellationToken);

        return await _consulta.MontarRespostaAsync(ocorrencia, null, cancellationToken);
    }

    private async Task<(Ocorrencia Ocorrencia, Voluntario Voluntario)> CarregarParaAceiteAsync(
        Guid ocorrenciaId,
        Guid voluntarioId,
        CancellationToken cancellationToken)
    {
        Ocorrencia ocorrencia = await _ocorrencias.ObterAsync(ocorrenciaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Ocorrência não encontrada.");

        Voluntario voluntario = await _voluntarios.ObterPorIdAsync(voluntarioId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Voluntário não encontrado.");

        return (ocorrencia, voluntario);
    }

    private DateTime Agora()
    {
        return _relogio.GetUtcNow().UtcDateTime;
    }

    [LoggerMessage(EventId = 740, Level = LogLevel.Information, Message = "Ocorrência {OcorrenciaId} aceita pelo voluntário {VoluntarioId}.")]
    private static partial void RegistrarAceite(ILogger logger, Guid ocorrenciaId, Guid voluntarioId);

    [LoggerMessage(EventId = 741, Level = LogLevel.Information, Message = "Aceite da ocorrência {OcorrenciaId} com conflito de concorrência; nova tentativa com a ocorrência relida.")]
    private static partial void RegistrarNovaTentativa(ILogger logger, Guid ocorrenciaId);

    [LoggerMessage(EventId = 742, Level = LogLevel.Information, Message = "Ocorrência {OcorrenciaId} terminada em {Estado}.")]
    private static partial void RegistrarFim(ILogger logger, Guid ocorrenciaId, string estado);
}
