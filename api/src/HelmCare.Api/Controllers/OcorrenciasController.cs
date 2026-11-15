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

    /// <summary>
    /// Cria o controller.
    /// </summary>
    /// <param name="servico">Consultas de ocorrências.</param>
    public OcorrenciasController(OcorrenciaServico servico)
    {
        _servico = servico;
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
}
