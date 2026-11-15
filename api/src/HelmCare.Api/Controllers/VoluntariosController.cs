using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Voluntarios;
using HelmCare.Api.Servicos.Voluntarios;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HelmCare.Api.Controllers;

/// <summary>
/// Rede de voluntários: cadastro e disponibilidade (seção 7 do contrato). O <c>POST /voluntarios</c>
/// entrou no Contrato 1.13.
/// </summary>
[ApiController]
[Route("api/v1/voluntarios")]
public sealed class VoluntariosController : ControllerBase
{
    private readonly VoluntarioServico _servico;

    /// <summary>
    /// Cria o controller.
    /// </summary>
    /// <param name="servico">Caso de uso da rede de voluntários.</param>
    public VoluntariosController(VoluntarioServico servico)
    {
        _servico = servico;
    }

    /// <summary>
    /// Torna um entregador cadastrado voluntário da rede. Ele começa indisponível e sem posição.
    /// </summary>
    /// <remarks>
    /// O contrato não tem GET de voluntário, então o <c>Location</c> aponta para o entregador
    /// (regra dos recursos sem GET próprio, seção 7).
    /// </remarks>
    /// <param name="requisicao">Entregador e tipo.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O voluntário cadastrado.</returns>
    /// <response code="201">Voluntário cadastrado, indisponível e sem posição.</response>
    /// <response code="400">Campo ausente ou tipo fora de ENTREGADOR e VIGIA_MOTO.</response>
    /// <response code="404">Entregador inexistente.</response>
    /// <response code="409">Entregador que já é voluntário.</response>
    [HttpPost]
    [ProducesResponseType<VoluntarioResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<VoluntarioResposta>> Cadastrar(
        CadastrarVoluntarioRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        VoluntarioResposta resposta = await _servico.CadastrarAsync(requisicao, cancellationToken);
        return CreatedAtAction(
            nameof(EntregadoresController.Obter),
            "Entregadores",
            new { id = resposta.EntregadorId },
            resposta);
    }

    /// <summary>
    /// Atualiza a disponibilidade e a posição do voluntário.
    /// </summary>
    /// <remarks>
    /// Disponível exige latitude e longitude. Indisponível apaga a posição, mesmo que ela venha na
    /// requisição. A posição só volta nesta resposta, para o próprio voluntário, e nunca vai para o log.
    /// </remarks>
    /// <param name="id">Id do voluntário.</param>
    /// <param name="requisicao">Disponibilidade e posição.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O voluntário atualizado.</returns>
    /// <response code="200">Voluntário atualizado.</response>
    /// <response code="400">disponivel ausente, coordenada fora da faixa ou disponível sem as duas coordenadas.</response>
    /// <response code="404">Voluntário inexistente.</response>
    [HttpPut("{id:guid}/disponibilidade")]
    [ProducesResponseType<VoluntarioResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<VoluntarioResposta>> AtualizarDisponibilidade(
        Guid id,
        DisponibilidadeVoluntarioRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        return Ok(await _servico.AtualizarDisponibilidadeAsync(id, requisicao, cancellationToken));
    }
}
