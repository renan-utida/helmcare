using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Servicos.Cadastro;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HelmCare.Api.Controllers;

/// <summary>
/// Cadastro do entregador: dados pessoais, ficha médica e contatos de emergência (seção 7 do contrato).
/// Os dados pessoais saem sempre mascarados, e a ficha médica nunca sai no GET.
/// </summary>
[ApiController]
[Route("api/v1/entregadores")]
public sealed class EntregadoresController : ControllerBase
{
    private readonly EntregadorServico _servico;

    /// <summary>
    /// Cria o controller.
    /// </summary>
    /// <param name="servico">Casos de uso do cadastro.</param>
    public EntregadoresController(EntregadorServico servico)
    {
        _servico = servico;
    }

    /// <summary>
    /// Cadastra um entregador.
    /// </summary>
    /// <remarks>
    /// CPF e CNH com 11 dígitos e telefone com 10 ou 11, com ou sem máscara. A resposta traz a PII
    /// mascarada e o cabeçalho <c>Location</c> aponta para o GET do entregador.
    /// </remarks>
    /// <param name="requisicao">Dados do cadastro.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O entregador criado.</returns>
    /// <response code="201">Entregador criado.</response>
    /// <response code="400">Dados fora das validações de cadastro.</response>
    /// <response code="409">CPF já cadastrado.</response>
    [HttpPost]
    [ProducesResponseType<EntregadorResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EntregadorResposta>> Criar(
        CriarEntregadorRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        EntregadorResposta resposta = await _servico.CriarAsync(requisicao, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = resposta.Id }, resposta);
    }

    /// <summary>
    /// Consulta o entregador, com a PII mascarada e os contatos de emergência.
    /// </summary>
    /// <remarks>
    /// Sem login no protótipo, então a ficha médica nunca vem aqui: só <c>possuiFichaMedica</c>.
    /// </remarks>
    /// <param name="id">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O entregador.</returns>
    /// <response code="200">Entregador encontrado.</response>
    /// <response code="404">Entregador inexistente.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<EntregadorResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<EntregadorResposta>> Obter(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _servico.ObterAsync(id, cancellationToken));
    }

    /// <summary>
    /// Cria ou substitui a ficha médica do entregador.
    /// </summary>
    /// <remarks>
    /// A resposta devolve os quatro campos só para quem acabou de enviá-los.
    /// </remarks>
    /// <param name="id">Id do entregador.</param>
    /// <param name="requisicao">Dados da ficha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A ficha gravada.</returns>
    /// <response code="200">Ficha criada ou atualizada.</response>
    /// <response code="400">Tipo sanguíneo fora da lista ou texto longo demais.</response>
    /// <response code="404">Entregador inexistente.</response>
    [HttpPut("{id:guid}/ficha-medica")]
    [ProducesResponseType<FichaMedicaResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<FichaMedicaResposta>> DefinirFichaMedica(
        Guid id,
        FichaMedicaRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        return Ok(await _servico.DefinirFichaMedicaAsync(id, requisicao, cancellationToken));
    }

    /// <summary>
    /// Adiciona um contato de emergência ao entregador.
    /// </summary>
    /// <remarks>
    /// O contrato não tem GET de contato, então o <c>Location</c> aponta para o entregador, que
    /// lista os contatos.
    /// </remarks>
    /// <param name="id">Id do entregador.</param>
    /// <param name="requisicao">Dados do contato.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O contato criado, com o telefone mascarado.</returns>
    /// <response code="201">Contato criado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Entregador inexistente.</response>
    [HttpPost("{id:guid}/contatos")]
    [ProducesResponseType<ContatoResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ContatoResposta>> AdicionarContato(
        Guid id,
        ContatoRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ContatoResposta resposta = await _servico.AdicionarContatoAsync(id, requisicao, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id }, resposta);
    }
}
