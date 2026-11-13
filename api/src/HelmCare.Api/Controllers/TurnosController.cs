using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Turnos;
using HelmCare.Api.Servicos.Turnos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HelmCare.Api.Controllers;

/// <summary>
/// Ciclo do turno: início com o Índice de Risco, encerramento, alerta de queda e cancelamento
/// do alerta (seção 7 do contrato).
/// </summary>
[ApiController]
[Route("api/v1/turnos")]
public sealed class TurnosController : ControllerBase
{
    private readonly TurnoServico _servico;

    /// <summary>
    /// Cria o controller.
    /// </summary>
    /// <param name="servico">Casos de uso do turno.</param>
    public TurnosController(TurnoServico servico)
    {
        _servico = servico;
    }

    /// <summary>
    /// Inicia um turno e devolve o Índice de Risco.
    /// </summary>
    /// <remarks>
    /// Com <c>protecaoAtiva</c> verdadeiro (o padrão), o entregador precisa ter capacete e a API
    /// publica <c>ARMAR</c> antes de gravar. O contrato não tem GET do turno, então o
    /// <c>Location</c> aponta para o <c>GET /turnos/{id}/risco</c>, que devolve o mesmo corpo.
    /// </remarks>
    /// <param name="requisicao">Entregador, período e proteção.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O Índice de Risco do turno.</returns>
    /// <response code="201">Turno iniciado.</response>
    /// <response code="400">Campo ausente, fimPrevisto não posterior ao inicio ou turno de mais de 24 h.</response>
    /// <response code="404">Entregador inexistente.</response>
    /// <response code="409">Turno ativo, ou proteção ativa sem capacete vinculado.</response>
    /// <response code="503">O ARMAR não chegou ao broker; nada foi gravado.</response>
    [HttpPost]
    [ProducesResponseType<RiscoTurnoResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<RiscoTurnoResposta>> Iniciar(
        IniciarTurnoRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        RiscoTurnoResposta resposta = await _servico.IniciarAsync(requisicao, cancellationToken);
        return CreatedAtAction(nameof(ObterRisco), new { id = resposta.TurnoId }, resposta);
    }

    /// <summary>
    /// Índice de Risco do turno, guardado no início (sem nova chamada ao Serviço de Risco).
    /// </summary>
    /// <param name="id">Id do turno.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O Índice de Risco.</returns>
    /// <response code="200">Índice do turno.</response>
    /// <response code="404">Turno inexistente.</response>
    [HttpGet("{id:guid}/risco")]
    [ProducesResponseType<RiscoTurnoResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RiscoTurnoResposta>> ObterRisco(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _servico.ObterRiscoAsync(id, cancellationToken));
    }

    /// <summary>
    /// Encerra o turno e, com a proteção ativa, publica <c>DESARMAR</c>.
    /// </summary>
    /// <param name="id">Id do turno.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O turno encerrado.</returns>
    /// <response code="200">Turno encerrado.</response>
    /// <response code="404">Turno inexistente.</response>
    /// <response code="409">Turno já encerrado, alerta de queda em andamento ou ocorrência ativa.</response>
    /// <response code="503">O DESARMAR não chegou ao broker; nada foi gravado.</response>
    [HttpPost("{id:guid}/encerrar")]
    [ProducesResponseType<TurnoResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<TurnoResposta>> Encerrar(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _servico.EncerrarAsync(id, cancellationToken));
    }

    /// <summary>
    /// Estado do alerta de queda do capacete no turno (tela de alerta do app).
    /// </summary>
    /// <param name="id">Id do turno.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O estado do alerta.</returns>
    /// <response code="200">Estado do alerta.</response>
    /// <response code="404">Turno inexistente.</response>
    /// <response code="409">Entregador sem capacete vinculado.</response>
    [HttpGet("{id:guid}/alerta")]
    [ProducesResponseType<AlertaTurnoResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AlertaTurnoResposta>> ObterAlerta(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _servico.ObterAlertaAsync(id, cancellationToken));
    }

    /// <summary>
    /// Cancela o alerta durante a contagem (publica <c>CANCELAR</c>).
    /// </summary>
    /// <remarks>
    /// Responde 202 sem corpo, com o <c>Location</c> do GET do alerta: o comando foi publicado, e o
    /// app confirma o cancelamento pelo estado do capacete.
    /// </remarks>
    /// <param name="id">Id do turno.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>Sem corpo.</returns>
    /// <response code="202">CANCELAR publicado.</response>
    /// <response code="404">Turno inexistente.</response>
    /// <response code="409">Turno encerrado, entregador sem capacete ou capacete fora de CONTAGEM.</response>
    /// <response code="503">O CANCELAR não chegou ao broker.</response>
    [HttpPost("{id:guid}/alerta/cancelar")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> CancelarAlerta(Guid id, CancellationToken cancellationToken)
    {
        await _servico.CancelarAlertaAsync(id, cancellationToken);
        return AcceptedAtAction(nameof(ObterAlerta), new { id }, value: null);
    }
}
