using System;
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
    /// <param name="servico">Consulta de ocorrências.</param>
    public OcorrenciasController(OcorrenciaServico servico)
    {
        _servico = servico;
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
