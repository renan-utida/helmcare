using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Cofre;
using HelmCare.Api.Servicos.Cofre;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HelmCare.Api.Controllers;

/// <summary>
/// Rotas JSON do Cofre médico (seção 7 do contrato, 1.16): o acesso do socorrista à ficha mínima pelo
/// <c>qrToken</c> e a auditoria do entregador. A página web fica fora do <c>/api/v1</c>, em
/// <c>GET /cofre/{qrToken}</c>.
/// </summary>
[ApiController]
[Route("api/v1/cofre")]
public sealed class CofreController : ControllerBase
{
    private readonly CofreServico _servico;

    /// <summary>
    /// Cria o controller.
    /// </summary>
    /// <param name="servico">Casos de uso do Cofre.</param>
    public CofreController(CofreServico servico)
    {
        _servico = servico;
    }

    /// <summary>
    /// Socorrista autenticado acessa a ficha mínima do dono do capacete (ou abre a emergência manual).
    /// </summary>
    /// <remarks>
    /// Toda tentativa de um socorrista com <c>qrToken</c> existente gera um registro de auditoria. A
    /// resposta sai com <c>Cache-Control: no-store</c>.
    /// </remarks>
    /// <param name="qrToken">Token do QR Code do capacete.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A ficha mínima e os contatos para ligar.</returns>
    /// <response code="200">Acesso concedido (dentro da janela ou numa janela nova).</response>
    /// <response code="401">Sem token válido.</response>
    /// <response code="403">Token de entregador, socorrista não verificado ou janela expirada (o motivo vem no campo motivo).</response>
    /// <response code="404">QR Code inexistente.</response>
    [HttpPost("{qrToken}/acesso")]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<AcessoCofreResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AcessoCofreResposta>> Acessar(string qrToken, CancellationToken cancellationToken)
    {
        // O IP já vem corrigido pelos cabeçalhos encaminhados; fica só no banco.
        string? ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return Ok(await _servico.AcessarAsync(qrToken, ip, cancellationToken));
    }

    /// <summary>
    /// Histórico de acessos ao Cofre, do mais recente ao mais antigo. Só o próprio entregador.
    /// </summary>
    /// <param name="entregadorId">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>As tentativas, sem ip e sem dados de saúde.</returns>
    /// <response code="200">Auditoria do entregador.</response>
    /// <response code="401">Sem token válido.</response>
    /// <response code="403">Outro entregador ou token de socorrista.</response>
    /// <response code="404">Entregador inexistente.</response>
    [HttpGet("auditoria/{entregadorId:guid}")]
    [ProducesResponseType<IReadOnlyList<ItemAuditoriaCofreResposta>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ItemAuditoriaCofreResposta>>> ListarAuditoria(
        Guid entregadorId,
        CancellationToken cancellationToken)
    {
        return Ok(await _servico.ListarAuditoriaAsync(entregadorId, cancellationToken));
    }
}
