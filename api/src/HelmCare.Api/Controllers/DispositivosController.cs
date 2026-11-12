using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Dispositivos;
using HelmCare.Api.Servicos.Cadastro;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HelmCare.Api.Controllers;

/// <summary>
/// Vínculo dos capacetes com os entregadores (seção 7 do contrato).
/// </summary>
[ApiController]
[Route("api/v1/dispositivos")]
[Produces("application/json")]
public sealed class DispositivosController : ControllerBase
{
    private readonly DispositivoServico _servico;

    /// <summary>
    /// Cria o controller.
    /// </summary>
    /// <param name="servico">Caso de uso do vínculo.</param>
    public DispositivosController(DispositivoServico servico)
    {
        _servico = servico;
    }

    /// <summary>
    /// Vincula um capacete a um entregador e gera o QR token do Cofre.
    /// </summary>
    /// <remarks>
    /// O <c>qrToken</c> só sai nesta resposta, para imprimir o QR Code. O contrato não tem GET de
    /// dispositivo, então o <c>Location</c> aponta para o entregador dono do capacete.
    /// </remarks>
    /// <param name="requisicao">Código do capacete e id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O capacete vinculado.</returns>
    /// <response code="201">Capacete vinculado.</response>
    /// <response code="400">Código fora do formato HC-0000 ou entregador não informado.</response>
    /// <response code="404">Entregador inexistente.</response>
    /// <response code="409">Código já vinculado ou entregador que já tem capacete.</response>
    [HttpPost]
    [ProducesResponseType<DispositivoResposta>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DispositivoResposta>> Vincular(
        VincularDispositivoRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        DispositivoResposta resposta = await _servico.VincularAsync(requisicao, cancellationToken);
        return CreatedAtAction(
            nameof(EntregadoresController.Obter),
            "Entregadores",
            new { id = resposta.EntregadorId },
            resposta);
    }
}
