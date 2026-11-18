using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Cofre;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Servicos.Cofre;

/// <summary>
/// Cofre médico (seção 7 do contrato, 1.16): o acesso do socorrista à ficha mínima pelo <c>qrToken</c>
/// do capacete e a auditoria que o entregador consulta.
/// </summary>
/// <remarks>
/// <para>Ordem do acesso: <c>401</c> sem token; <c>403</c> para o token de entregador (sem auditoria);
/// <c>404</c> para o <c>qrToken</c> inexistente (sem auditoria); depois a regra do socorrista, sempre com
/// auditoria: <c>403</c> não verificado, <c>403</c> expirado ou <c>200</c>.</para>
/// <para>O <c>qrToken</c> é credencial: nunca vai para o log, para a auditoria ou para a resposta.</para>
/// </remarks>
public sealed partial class CofreServico
{
    /// <summary>
    /// Mensagem do <c>404</c> do <c>qrToken</c> inexistente (não repete o token).
    /// </summary>
    public const string MensagemQrInvalido = "QR Code inválido ou inexistente.";

    private readonly IAutorizacaoRecursos _autorizacao;
    private readonly IDispositivoRepositorio _dispositivos;
    private readonly ISocorristaRepositorio _socorristas;
    private readonly IEntregadorRepositorio _entregadores;
    private readonly IOcorrenciaRepositorio _ocorrencias;
    private readonly IAcessoCofreRepositorio _acessos;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly TimeProvider _relogio;
    private readonly TimeSpan _ttl;
    private readonly ILogger<CofreServico> _logger;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="autorizacao">Papel do token e dono da auditoria.</param>
    /// <param name="dispositivos">Capacetes (busca pelo <c>qrToken</c>).</param>
    /// <param name="socorristas">Socorristas.</param>
    /// <param name="entregadores">Entregadores (ficha e contatos).</param>
    /// <param name="ocorrencias">Ocorrências (ativa do entregador e fim da janela).</param>
    /// <param name="acessos">Auditoria do Cofre.</param>
    /// <param name="unidadeDeTrabalho">Grava cada tentativa.</param>
    /// <param name="relogio">Relógio do servidor.</param>
    /// <param name="opcoes">Constantes do contrato (<c>TTL_ACESSO_COFRE_MIN</c>).</param>
    /// <param name="logger">Log da aplicação.</param>
    public CofreServico(
        IAutorizacaoRecursos autorizacao,
        IDispositivoRepositorio dispositivos,
        ISocorristaRepositorio socorristas,
        IEntregadorRepositorio entregadores,
        IOcorrenciaRepositorio ocorrencias,
        IAcessoCofreRepositorio acessos,
        IUnidadeDeTrabalho unidadeDeTrabalho,
        TimeProvider relogio,
        IOptions<OpcoesHelmCare> opcoes,
        ILogger<CofreServico> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _autorizacao = autorizacao;
        _dispositivos = dispositivos;
        _socorristas = socorristas;
        _entregadores = entregadores;
        _ocorrencias = ocorrencias;
        _acessos = acessos;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _relogio = relogio;
        _ttl = TimeSpan.FromMinutes(opcoes.Value.TtlAcessoCofreMin);
        _logger = logger;
    }

    /// <summary>
    /// Acesso do socorrista à ficha mínima do dono do capacete, com a auditoria de cada tentativa.
    /// </summary>
    /// <param name="qrToken">Token do QR Code (credencial: nunca registrado).</param>
    /// <param name="ip">IP do cliente depois dos cabeçalhos encaminhados (fica só no banco).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A ficha mínima e os contatos.</returns>
    /// <exception cref="NaoAutenticadoException">Sem token (401).</exception>
    /// <exception cref="AcessoNegadoException">Token de entregador ou de outro papel (403, sem auditoria).</exception>
    /// <exception cref="RecursoNaoEncontradoException"><c>qrToken</c> inexistente (404, sem auditoria).</exception>
    /// <exception cref="AcessoCofreNegadoException">Não verificado ou janela expirada (403, com auditoria).</exception>
    public async Task<AcessoCofreResposta> AcessarAsync(string qrToken, string? ip, CancellationToken cancellationToken)
    {
        if (_autorizacao.EntregadorDoToken is { } entregadorDoToken)
        {
            RegistrarTokenDeEntregador(_logger, entregadorDoToken);
        }

        Guid socorristaId = _autorizacao.ExigirSocorrista();

        Dispositivo dispositivo = await _dispositivos.ObterPorQrTokenAsync(qrToken, cancellationToken) ?? throw QrInexistente(socorristaId);

        // Um token com socorristaId de um cadastro que não existe mais é tratado como QR inválido,
        // sem auditoria (não há socorrista para a chave estrangeira).
        Socorrista socorrista = await _socorristas.ObterPorIdAsync(socorristaId, cancellationToken) ?? throw QrInexistente(socorristaId);

        Guid entregadorId = dispositivo.EntregadorId;
        DateTime agora = AteMicrossegundos(_relogio.GetUtcNow().UtcDateTime);

        Guid? ocorrenciaAtivaId = await _ocorrencias.ObterIdAtivaDoEntregadorAsync(entregadorId, cancellationToken);
        AcessoCofre? ultima = await _acessos.ObterUltimaConcessaoAsync(socorrista.Id, entregadorId, cancellationToken);
        DateTime? fimDaOcorrencia = await FimDaOcorrenciaDaJanelaAsync(ultima, cancellationToken);

        JanelaAcessoCofre? janela = JanelaAcessoCofre.DoPar(ultima is null ? [] : [ultima], fimDaOcorrencia, _ttl);
        DecisaoAcessoCofre decisao = JanelaAcessoCofre.Decidir(janela, socorrista.Verificado, ocorrenciaAtivaId, agora, _ttl);

        AcessoCofre acesso = AcessoCofre.Registrar(entregadorId, socorrista.Id, decisao, agora, ip);
        await _acessos.AdicionarAsync(acesso, cancellationToken);
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        string resultado = decisao.Motivo is { } motivo ? NomeContrato.Para(motivo) : "CONCEDIDO";
        RegistrarTentativa(_logger, acesso.Id, socorrista.Id, entregadorId, resultado);

        if (!decisao.Concedido)
        {
            throw new AcessoCofreNegadoException(decisao.Motivo!.Value);
        }

        return await MontarRespostaAsync(acesso, cancellationToken);
    }

    /// <summary>
    /// Auditoria do Cofre do entregador, da tentativa mais recente à mais antiga. Só o próprio entregador.
    /// </summary>
    /// <param name="entregadorId">Entregador dono da ficha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>As tentativas, sem <c>ip</c> e sem dados de saúde.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Entregador inexistente (404).</exception>
    /// <exception cref="AcessoNegadoException">Outro entregador ou token de socorrista (403).</exception>
    public async Task<IReadOnlyList<ItemAuditoriaCofreResposta>> ListarAuditoriaAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        Entregador entregador = await _entregadores.ObterPorIdAsync(entregadorId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Entregador não encontrado.");

        _autorizacao.ExigirDono(entregador.Id);

        IReadOnlyList<ItemAuditoriaCofre> itens = await _acessos.ListarAuditoriaAsync(entregador.Id, cancellationToken);

        return itens
            .Select(i => new ItemAuditoriaCofreResposta
            {
                AcessadoEm = i.AcessadoEm,
                ExpiraEm = i.ExpiraEm,
                Origem = i.Origem,
                Concedido = i.Concedido,
                Motivo = i.Motivo,
                Socorrista = new SocorristaAuditoriaResposta { Nome = i.SocorristaNome, Organizacao = i.SocorristaOrganizacao },
            })
            .ToList();
    }

    // O fim da ocorrência só importa para a janela SOS_ATIVO, e só depois que ela terminou.
    private async Task<DateTime?> FimDaOcorrenciaDaJanelaAsync(AcessoCofre? ultima, CancellationToken cancellationToken)
    {
        if (ultima is not { Origem: OrigemAcessoCofre.SosAtivo, OcorrenciaId: { } ocorrenciaId })
        {
            return null;
        }

        Ocorrencia? ocorrencia = await _ocorrencias.ObterParaConsultaAsync(ocorrenciaId, cancellationToken);
        return ocorrencia is { EstaAtiva: false } ? ocorrencia.EncerradaEm : null;
    }

    // Só o mínimo da ficha: sem medicamentos e sem nome, CPF ou CNH do entregador.
    private async Task<AcessoCofreResposta> MontarRespostaAsync(AcessoCofre acesso, CancellationToken cancellationToken)
    {
        Entregador? entregador = await _entregadores.ObterComCadastroAsync(acesso.EntregadorId, cancellationToken);
        FichaMedica? ficha = entregador?.FichaMedica;

        return new AcessoCofreResposta
        {
            Origem = acesso.Origem,
            OcorrenciaId = acesso.OcorrenciaId,
            AcessadoEm = acesso.AcessadoEm,
            ExpiraEm = acesso.ExpiraEm,
            TipoSanguineo = ficha?.TipoSanguineo,
            Alergias = ficha?.Alergias,
            CondicoesCriticas = ficha?.CondicoesCriticas,
            Contatos = entregador?.Contatos
                .Select(c => new ContatoCofreResposta { Nome = c.Nome, Telefone = c.Telefone, Parentesco = c.Parentesco })
                .ToList() ?? [],
        };
    }

    // O PostgreSQL guarda os instantes com microssegundos, e o .NET tem 100 ns. Sem cortar aqui, o 200
    // que abre a janela devolveria o expiraEm com 7 casas e os seguintes, relidos do banco, com 6: o
    // "mesmo expiraEm" do contrato precisa ser igual também no texto.
    private static DateTime AteMicrossegundos(DateTime instante)
    {
        return new DateTime(instante.Ticks - (instante.Ticks % 10), DateTimeKind.Utc);
    }

    private RecursoNaoEncontradoException QrInexistente(Guid socorristaId)
    {
        RegistrarQrInexistente(_logger, socorristaId);
        return new RecursoNaoEncontradoException(MensagemQrInvalido);
    }

    [LoggerMessage(EventId = 900, Level = LogLevel.Information, Message = "Acesso ao Cofre {AcessoId}: socorrista {SocorristaId}, entregador {EntregadorId}, resultado {Resultado}.")]
    private static partial void RegistrarTentativa(ILogger logger, Guid acessoId, Guid socorristaId, Guid entregadorId, string resultado);

    [LoggerMessage(EventId = 901, Level = LogLevel.Warning, Message = "Acesso ao Cofre recusado: token de entregador {EntregadorId} (403, sem auditoria).")]
    private static partial void RegistrarTokenDeEntregador(ILogger logger, Guid entregadorId);

    [LoggerMessage(EventId = 902, Level = LogLevel.Warning, Message = "Acesso ao Cofre recusado: QR Code inexistente, socorrista {SocorristaId} (404, sem auditoria).")]
    private static partial void RegistrarQrInexistente(ILogger logger, Guid socorristaId);
}
