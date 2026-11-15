using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Ocorrencias;
using HelmCare.Api.Servicos.Ocorrencias;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HelmCare.Api.Controllers;

/// <summary>
/// Ocorrências de SOS (seção 7 do contrato).
/// </summary>
[ApiController]
[Route("api/v1/ocorrencias")]
public sealed class OcorrenciasController : ControllerBase
{
    private readonly OcorrenciaServico _servico;
    private readonly AtendimentoOcorrenciaServico _atendimento;

    /// <summary>
    /// Cria o controller.
    /// </summary>
    /// <param name="servico">Consultas de ocorrências.</param>
    /// <param name="atendimento">Aceite, cancelamento e encerramento.</param>
    public OcorrenciasController(OcorrenciaServico servico, AtendimentoOcorrenciaServico atendimento)
    {
        _servico = servico;
        _atendimento = atendimento;
    }

    /// <summary>
    /// Chamados que o voluntário pode atender, do mais perto para o mais longe.
    /// </summary>
    /// <remarks>
    /// Na camada 1, só os chamados a até <c>RAIO_CAMADA_1_KM</c>; na camada 2, todos. Voluntário
    /// indisponível recebe a lista vazia, e o entregador nunca vê o próprio SOS. A localização é a
    /// aproximada (2 casas) e a <c>distanciaKm</c> é calculada até ela. A rota literal
    /// <c>proximas</c> não colide com <c>{id:guid}</c>, que só aceita um GUID.
    /// </remarks>
    /// <param name="voluntarioId">Voluntário que consulta.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>Os chamados.</returns>
    /// <response code="200">Chamados (lista vazia para o voluntário indisponível).</response>
    /// <response code="400">voluntarioId ausente, vazio ou fora do formato GUID.</response>
    /// <response code="404">Voluntário inexistente.</response>
    [HttpGet("proximas")]
    [ProducesResponseType<IReadOnlyList<OcorrenciaProximaResposta>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<OcorrenciaProximaResposta>>> ListarProximas(
        [FromQuery][Required(ErrorMessage = "O parâmetro voluntarioId é obrigatório.")] Guid? voluntarioId,
        CancellationToken cancellationToken)
    {
        // O [Required] já respondeu 400 sem o parâmetro; aqui ele sempre tem valor.
        return Ok(await _servico.ListarProximasAsync(voluntarioId!.Value, cancellationToken));
    }

    /// <summary>
    /// Status de uma ocorrência e o cronômetro desde a queda.
    /// </summary>
    /// <remarks>
    /// A localização só vem exata para o voluntário que aceitou; sem <c>voluntarioId</c>, ou com
    /// outro voluntário, vem com 2 casas decimais. <c>localizacaoExata</c> diz qual veio.
    /// </remarks>
    /// <param name="id">Id da ocorrência.</param>
    /// <param name="voluntarioId">Voluntário que consulta (opcional).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O status da ocorrência.</returns>
    /// <response code="200">Status da ocorrência.</response>
    /// <response code="404">Ocorrência inexistente, ou voluntário informado e inexistente.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OcorrenciaResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OcorrenciaResposta>> Obter(
        Guid id,
        [FromQuery] Guid? voluntarioId,
        CancellationToken cancellationToken)
    {
        return Ok(await _servico.ObterAsync(id, voluntarioId, cancellationToken));
    }

    /// <summary>
    /// O voluntário aceita o chamado: a ocorrência vai para <c>SOCORRO_A_CAMINHO</c> e a API publica
    /// <c>SOCORRO_A_CAMINHO</c> no capacete da vítima.
    /// </summary>
    /// <remarks>
    /// Devolve o mesmo corpo do <c>GET /ocorrencias/{id}?voluntarioId=</c> desse voluntário: localização
    /// exata, cronômetro parado e o <c>tempoAteAceiteSegundos</c>. O comando é publicado depois de gravar
    /// e não bloqueia a rota: com o broker fora, o 201 sai do mesmo jeito.
    /// </remarks>
    /// <param name="id">Id da ocorrência.</param>
    /// <param name="requisicao">Voluntário que aceita.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A ocorrência aceita.</returns>
    /// <response code="201">Aceite registrado; Location para o GET da ocorrência com esse voluntário.</response>
    /// <response code="400">voluntarioId ausente.</response>
    /// <response code="404">Ocorrência ou voluntário inexistente.</response>
    /// <response code="409">O voluntário não pode atender, ou a ocorrência não aguarda aceite (inclusive aceite simultâneo).</response>
    [HttpPost("{id:guid}/aceites")]
    [ProducesResponseType<OcorrenciaResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OcorrenciaResposta>> Aceitar(
        Guid id,
        AceiteRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        // O [Required] já respondeu 400 sem o voluntarioId; aqui ele sempre tem valor.
        Guid voluntarioId = requisicao.VoluntarioId!.Value;
        OcorrenciaResposta resposta = await _atendimento.AceitarAsync(id, voluntarioId, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id, voluntarioId }, resposta);
    }

    /// <summary>
    /// O entregador informou que está bem: cancela o SOS e publica <c>ENCERRAR</c> no capacete.
    /// </summary>
    /// <remarks>
    /// Só vale em <c>SOS_CAMADA_1</c> ou <c>SOS_CAMADA_2</c>. O comando não bloqueia a rota.
    /// </remarks>
    /// <param name="id">Id da ocorrência.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A ocorrência cancelada, com a localização aproximada.</returns>
    /// <response code="200">Ocorrência cancelada.</response>
    /// <response code="404">Ocorrência inexistente.</response>
    /// <response code="409">Ocorrência fora da espera de aceite.</response>
    [HttpPost("{id:guid}/cancelar")]
    [ProducesResponseType<OcorrenciaResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OcorrenciaResposta>> Cancelar(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _atendimento.CancelarAsync(id, cancellationToken));
    }

    /// <summary>
    /// Encerra a ocorrência, com ou sem aceite (por exemplo, o SAMU chegou antes), e publica
    /// <c>ENCERRAR</c> no capacete.
    /// </summary>
    /// <remarks>
    /// Sem aceite, o <c>tempoAteAceiteSegundos</c> fica nulo. O comando não bloqueia a rota.
    /// </remarks>
    /// <param name="id">Id da ocorrência.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A ocorrência encerrada, com a localização aproximada.</returns>
    /// <response code="200">Ocorrência encerrada.</response>
    /// <response code="404">Ocorrência inexistente.</response>
    /// <response code="409">Ocorrência já encerrada ou cancelada.</response>
    [HttpPost("{id:guid}/encerrar")]
    [ProducesResponseType<OcorrenciaResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OcorrenciaResposta>> Encerrar(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _atendimento.EncerrarAsync(id, cancellationToken));
    }
}
