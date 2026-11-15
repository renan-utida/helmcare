using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.TrechosCriticos;
using HelmCare.Api.Servicos.TrechosCriticos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HelmCare.Api.Controllers;

/// <summary>
/// Trechos com mais sinistros graves, repassados do Serviço de Risco (seção 7 do contrato).
/// </summary>
[ApiController]
[Route("api/v1/trechos-criticos")]
public sealed class TrechosCriticosController : ControllerBase
{
    private readonly TrechosCriticosServico _servico;

    /// <summary>
    /// Cria o controller.
    /// </summary>
    /// <param name="servico">Repasse dos trechos críticos.</param>
    public TrechosCriticosController(TrechosCriticosServico servico)
    {
        _servico = servico;
    }

    /// <summary>
    /// Lista os trechos com mais sinistros graves, em ordem decrescente.
    /// </summary>
    /// <remarks>
    /// Mesmo formato do <c>GET /risco/v1/trechos-criticos</c>. Sem o Serviço de Risco, responde 503
    /// (não há versão simulada da lista).
    /// </remarks>
    /// <param name="limite">Quantidade de trechos, de 1 a 50 (padrão 10).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O período e os trechos.</returns>
    /// <response code="200">Lista de trechos.</response>
    /// <response code="400">limite fora de 1 a 50.</response>
    /// <response code="503">Serviço de Risco indisponível.</response>
    [HttpGet]
    [ProducesResponseType<TrechosCriticosResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<TrechosCriticosResposta>> Listar(
        [FromQuery][Range(TrechosCriticosServico.LimiteMinimo, TrechosCriticosServico.LimiteMaximo, ErrorMessage = "O parâmetro limite deve ficar entre 1 e 50.")] int limite = TrechosCriticosServico.LimitePadrao,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _servico.ListarAsync(limite, cancellationToken));
    }
}
