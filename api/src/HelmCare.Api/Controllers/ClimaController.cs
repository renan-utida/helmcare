using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Clima;
using HelmCare.Api.Servicos.Clima;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HelmCare.Api.Controllers;

/// <summary>
/// Condições atuais do tempo na cidade (seção 7 do contrato, 1.17), para o cartão "Clima" do app.
/// </summary>
[ApiController]
[Route("api/v1/clima")]
public sealed class ClimaController : ControllerBase
{
    private readonly ClimaServico _servico;

    /// <summary>
    /// Cria o controller.
    /// </summary>
    /// <param name="servico">Caso de uso do clima.</param>
    public ClimaController(ClimaServico servico)
    {
        _servico = servico;
    }

    /// <summary>
    /// Condições atuais da cidade pela Open-Meteo (dados CC BY 4.0, "Dados: Open-Meteo").
    /// </summary>
    /// <remarks>
    /// Sem parâmetros: o local é a coordenada fixa da cidade. A resposta fica 10 min no cache. Sem a
    /// Open-Meteo e sem cache válido, <c>503</c>, sem valor simulado.
    /// </remarks>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O clima atual.</returns>
    /// <response code="200">Condições atuais.</response>
    /// <response code="401">Sem token válido.</response>
    /// <response code="403">Token de socorrista.</response>
    /// <response code="503">Open-Meteo indisponível e sem cache válido.</response>
    [HttpGet]
    [ProducesResponseType<ClimaResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ClimaResposta>> Obter(CancellationToken cancellationToken)
    {
        return Ok(await _servico.ObterAsync(cancellationToken));
    }
}
