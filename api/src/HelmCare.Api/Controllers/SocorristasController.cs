using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Contratos.Socorristas;
using HelmCare.Api.Servicos.Identidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HelmCare.Api.Controllers;

/// <summary>
/// Login do socorrista, usado pela página do Cofre (seção 7 do contrato, 1.16).
/// </summary>
[ApiController]
[Route("api/v1/socorristas")]
public sealed class SocorristasController : ControllerBase
{
    private readonly AutenticacaoSocorristaServico _autenticacao;

    /// <summary>
    /// Cria o controller.
    /// </summary>
    /// <param name="autenticacao">Login do socorrista.</param>
    public SocorristasController(AutenticacaoSocorristaServico autenticacao)
    {
        _autenticacao = autenticacao;
    }

    /// <summary>
    /// Login do socorrista com e-mail e senha (pública, até 10 por minuto por IP, sem renovação).
    /// </summary>
    /// <param name="requisicao">E-mail e senha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>Os tokens e o perfil do socorrista.</returns>
    /// <response code="200">Login aceito (também para o socorrista não verificado).</response>
    /// <response code="400">Campo ausente.</response>
    /// <response code="401">Senha errada, conta bloqueada ou conta de entregador (mesmo texto).</response>
    /// <response code="429">Limite de tentativas por minuto atingido.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(ConfiguracaoIdentidade.PoliticaLogin)]
    [ProducesResponseType<LoginSocorristaResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<LoginSocorristaResposta>> Entrar(LoginRequisicao requisicao, CancellationToken cancellationToken)
    {
        return Ok(await _autenticacao.EntrarAsync(requisicao, cancellationToken));
    }
}
