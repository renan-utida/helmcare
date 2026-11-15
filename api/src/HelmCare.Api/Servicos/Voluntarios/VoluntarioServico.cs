using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Voluntarios;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Dominio.ObjetosDeValor;
using Microsoft.Extensions.Logging;

namespace HelmCare.Api.Servicos.Voluntarios;

/// <summary>
/// Cadastro e disponibilidade dos voluntários da rede (seção 7 do contrato, "Regras da rede de
/// voluntários"). As regras ficam no <see cref="Voluntario"/>: começar indisponível, exigir a posição
/// para ficar disponível e apagar a posição ao ficar indisponível.
/// </summary>
/// <remarks>
/// A posição é localização pessoal (seção 9): os logs daqui levam só o id e a disponibilidade.
/// </remarks>
public sealed partial class VoluntarioServico
{
    /// <summary>
    /// Mensagem do 409 de entregador que já é voluntário (também usada pelo <c>TratadorExcecoes</c> na corrida).
    /// </summary>
    public const string MensagemJaVoluntario = "O entregador já é voluntário da rede.";

    private readonly IVoluntarioRepositorio _voluntarios;
    private readonly IEntregadorRepositorio _entregadores;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly ILogger<VoluntarioServico> _logger;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="voluntarios">Repositório de voluntários.</param>
    /// <param name="entregadores">Repositório de entregadores (para conferir se o entregador existe).</param>
    /// <param name="unidadeDeTrabalho">Confirma a gravação.</param>
    /// <param name="logger">Log da aplicação.</param>
    public VoluntarioServico(
        IVoluntarioRepositorio voluntarios,
        IEntregadorRepositorio entregadores,
        IUnidadeDeTrabalho unidadeDeTrabalho,
        ILogger<VoluntarioServico> logger)
    {
        _voluntarios = voluntarios;
        _entregadores = entregadores;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _logger = logger;
    }

    /// <summary>
    /// Torna um entregador cadastrado voluntário da rede, indisponível e sem posição.
    /// </summary>
    /// <remarks>
    /// Duas requisições ao mesmo tempo passam pela checagem e a segunda esbarra no índice único de
    /// <c>Voluntario.EntregadorId</c>; o <c>TratadorExcecoes</c> responde 409 com a mesma mensagem.
    /// </remarks>
    /// <param name="requisicao">Entregador e tipo.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O voluntário cadastrado.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o id ou o tipo são inválidos (400).</exception>
    /// <exception cref="RecursoNaoEncontradoException">Quando o entregador não existe (404).</exception>
    /// <exception cref="RegraNegocioException">Quando o entregador já é voluntário (409).</exception>
    public async Task<VoluntarioResposta> CadastrarAsync(
        CadastrarVoluntarioRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        // O DTO já recusou o tipo inválido; a conferência repete a regra para o serviço não depender disso.
        if (!NomeContrato.TentarLer(requisicao.Tipo, out TipoVoluntario tipo))
        {
            throw new ValidacaoDominioException("O campo tipo deve ser ENTREGADOR ou VIGIA_MOTO.");
        }

        // O domínio valida o id antes de qualquer consulta (Guid.Empty no lugar do nulo cai na mesma regra).
        var voluntario = new Voluntario(requisicao.EntregadorId ?? Guid.Empty, tipo);

        if (await _entregadores.ObterPorIdAsync(voluntario.EntregadorId, cancellationToken) is null)
        {
            throw new RecursoNaoEncontradoException("Entregador não encontrado.");
        }

        if (await _voluntarios.ExisteDoEntregadorAsync(voluntario.EntregadorId, cancellationToken))
        {
            throw new RegraNegocioException(MensagemJaVoluntario);
        }

        await _voluntarios.AdicionarAsync(voluntario, cancellationToken);
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        RegistrarCadastro(_logger, voluntario.Id);
        return VoluntarioResposta.De(voluntario);
    }

    /// <summary>
    /// Liga ou desliga o recebimento de chamados. Disponível exige latitude e longitude;
    /// indisponível apaga a posição, mesmo que ela venha na requisição.
    /// </summary>
    /// <param name="id">Id do voluntário.</param>
    /// <param name="requisicao">Disponibilidade e posição.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O voluntário atualizado.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Quando o voluntário não existe (404).</exception>
    /// <exception cref="ValidacaoDominioException">Quando fica disponível sem as duas coordenadas (400).</exception>
    public async Task<VoluntarioResposta> AtualizarDisponibilidadeAsync(
        Guid id,
        DisponibilidadeVoluntarioRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        Voluntario voluntario = await _voluntarios.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Voluntário não encontrado.");

        // Só monta a coordenada com as duas partes; com uma só, fica nula e o domínio decide:
        // disponível sem posição é 400, indisponível descarta o que veio.
        Coordenada? posicao = requisicao is { Latitude: { } latitude, Longitude: { } longitude }
            ? new Coordenada(latitude, longitude)
            : null;

        voluntario.AtualizarDisponibilidade(requisicao.Disponivel ?? false, posicao);
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        RegistrarDisponibilidade(_logger, voluntario.Id, voluntario.Disponivel);
        return VoluntarioResposta.De(voluntario);
    }

    [LoggerMessage(EventId = 700, Level = LogLevel.Information, Message = "Voluntário {VoluntarioId} cadastrado (indisponível).")]
    private static partial void RegistrarCadastro(ILogger logger, Guid voluntarioId);

    [LoggerMessage(EventId = 701, Level = LogLevel.Information, Message = "Voluntário {VoluntarioId} com disponibilidade {Disponivel}.")]
    private static partial void RegistrarDisponibilidade(ILogger logger, Guid voluntarioId, bool disponivel);
}
