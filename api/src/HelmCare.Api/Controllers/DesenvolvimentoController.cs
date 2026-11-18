using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Contratos.Socorristas;
using HelmCare.Api.Servicos.Cadastro;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HelmCare.Api.Controllers;

/// <summary>
/// Rotas só do ambiente Development (seção 7 do contrato, "Rota de desenvolvimento"). Fora dele, o
/// controller nem é registrado: as rotas dão 404 e não aparecem no Scalar.
/// </summary>
/// <remarks>
/// A rota não exige token: serve justamente para o entregador anterior à 1.15, que ainda não tem conta
/// e não consegue fazer login.
/// </remarks>
[ApiController]
[Route("api/v1/dev")]
[SoEmDesenvolvimento]
[AllowAnonymous]
public sealed class DesenvolvimentoController : ControllerBase
{
    private readonly EntregadorServico _entregadores;
    private readonly SocorristaServico _socorristas;

    /// <summary>
    /// Cria o controller.
    /// </summary>
    /// <param name="entregadores">Casos de uso do cadastro.</param>
    /// <param name="socorristas">Cadastro dos socorristas da demonstração.</param>
    public DesenvolvimentoController(EntregadorServico entregadores, SocorristaServico socorristas)
    {
        _entregadores = entregadores;
        _socorristas = socorristas;
    }

    /// <summary>
    /// Cria a conta (e-mail e senha) de um entregador que já existia antes da 1.15.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="requisicao">E-mail e senha, com as regras do cadastro.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O entregador, com o e-mail mascarado.</returns>
    /// <response code="201">Conta criada.</response>
    /// <response code="400">E-mail ou senha fora da regra.</response>
    /// <response code="404">Entregador inexistente.</response>
    /// <response code="409">O entregador já tem conta, ou o e-mail já é usado.</response>
    [HttpPost("entregadores/{id:guid}/conta")]
    [ProducesResponseType<EntregadorResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EntregadorResposta>> CriarContaEntregador(
        Guid id,
        ContaRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        EntregadorResposta resposta = await _entregadores.CriarContaAsync(id, requisicao, cancellationToken);
        return CreatedAtAction(nameof(EntregadoresController.Obter), "Entregadores", new { id }, resposta);
    }

    /// <summary>
    /// Cria um socorrista da demonstração e a conta dele (papel SOCORRISTA), numa transação só.
    /// </summary>
    /// <remarks>
    /// O socorrista não tem rota de leitura própria, então o 201 sai sem <c>Location</c>.
    /// </remarks>
    /// <param name="requisicao">Dados do socorrista, e-mail e senha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O socorrista, com o e-mail mascarado.</returns>
    /// <response code="201">Socorrista e conta criados.</response>
    /// <response code="400">Dados, e-mail ou senha fora da regra.</response>
    /// <response code="409">E-mail já usado.</response>
    [HttpPost("socorristas")]
    [ProducesResponseType<SocorristaResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SocorristaResposta>> CriarSocorrista(
        CriarSocorristaRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        SocorristaResposta resposta = await _socorristas.CriarAsync(requisicao, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, resposta);
    }
}
