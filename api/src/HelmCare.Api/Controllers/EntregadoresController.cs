using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Servicos.Cadastro;
using HelmCare.Api.Servicos.Identidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HelmCare.Api.Controllers;

/// <summary>
/// Cadastro, login e sessão do entregador, ficha médica, contatos de emergência e telefone (seção 7
/// do contrato). Os dados pessoais saem sempre mascarados, e a ficha médica só sai na rota própria,
/// sem cache. Todas as rotas com id usam a restrição <c>:guid</c>, então <c>/entregadores/sessao</c>
/// nunca é confundida com <c>/entregadores/{id}</c>.
/// </summary>
[ApiController]
[Route("api/v1/entregadores")]
public sealed class EntregadoresController : ControllerBase
{
    private readonly EntregadorServico _servico;
    private readonly AutenticacaoEntregadorServico _autenticacao;

    /// <summary>
    /// Cria o controller.
    /// </summary>
    /// <param name="servico">Casos de uso do cadastro.</param>
    /// <param name="autenticacao">Login, renovação do token e sessão.</param>
    public EntregadoresController(EntregadorServico servico, AutenticacaoEntregadorServico autenticacao)
    {
        _servico = servico;
        _autenticacao = autenticacao;
    }

    /// <summary>
    /// Cadastra um entregador e cria a conta dele (pública).
    /// </summary>
    /// <remarks>
    /// CPF e CNH com 11 dígitos, telefone com 10 ou 11, com ou sem máscara, e-mail até 254 caracteres e
    /// senha de 8 a 64, com maiúscula, minúscula e dígito. A resposta traz a PII mascarada e não traz
    /// token: o app chama o login logo depois.
    /// </remarks>
    /// <param name="requisicao">Dados do cadastro.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O entregador criado.</returns>
    /// <response code="201">Entregador e conta criados.</response>
    /// <response code="400">Dados fora das validações de cadastro.</response>
    /// <response code="409">CPF, CNH, telefone ou e-mail já cadastrado.</response>
    [HttpPost]
    [AllowAnonymous]
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
    /// Login do entregador com e-mail e senha (pública, até 10 por minuto por IP).
    /// </summary>
    /// <param name="requisicao">E-mail e senha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>Os tokens e a sessão.</returns>
    /// <response code="200">Login aceito.</response>
    /// <response code="400">Campo ausente.</response>
    /// <response code="401">E-mail ou senha errados, conta bloqueada ou conta de socorrista (mesmo texto).</response>
    /// <response code="429">Limite de tentativas por minuto atingido.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(ConfiguracaoIdentidade.PoliticaLogin)]
    [ProducesResponseType<LoginEntregadorResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<LoginEntregadorResposta>> Entrar(
        LoginRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        return Ok(await _autenticacao.EntrarAsync(requisicao, cancellationToken));
    }

    /// <summary>
    /// Troca o <c>refreshToken</c> por um token novo (pública, até 10 por minuto por IP).
    /// </summary>
    /// <param name="requisicao">Token de renovação.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O mesmo corpo do login.</returns>
    /// <response code="200">Tokens renovados.</response>
    /// <response code="400">Campo ausente.</response>
    /// <response code="401">Token de renovação inválido ou vencido: o app volta para o login.</response>
    /// <response code="429">Limite de tentativas por minuto atingido.</response>
    [HttpPost("renovar-token")]
    [AllowAnonymous]
    [EnableRateLimiting(ConfiguracaoIdentidade.PoliticaLogin)]
    [ProducesResponseType<LoginEntregadorResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<LoginEntregadorResposta>> RenovarToken(
        RenovarTokenRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        return Ok(await _autenticacao.RenovarAsync(requisicao, cancellationToken));
    }

    /// <summary>
    /// Sessão do entregador do token: o corpo do login, sem os campos do token.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A sessão.</returns>
    /// <response code="200">Sessão do entregador.</response>
    /// <response code="401">Sem token, ou token inválido ou vencido.</response>
    /// <response code="403">Token de socorrista.</response>
    [HttpGet("sessao")]
    [ProducesResponseType<SessaoEntregadorResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<SessaoEntregadorResposta>> ObterSessao(CancellationToken cancellationToken)
    {
        return Ok(await _autenticacao.ObterSessaoAsync(cancellationToken));
    }

    /// <summary>
    /// Consulta o entregador, com a PII mascarada (inclusive o e-mail) e os contatos de emergência.
    /// Só o próprio entregador.
    /// </summary>
    /// <remarks>
    /// A ficha médica nunca vem aqui: só <c>possuiFichaMedica</c>.
    /// </remarks>
    /// <param name="id">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O entregador.</returns>
    /// <response code="200">Entregador encontrado.</response>
    /// <response code="401">Sem token válido.</response>
    /// <response code="403">Outro entregador ou token de socorrista.</response>
    /// <response code="404">Entregador inexistente.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<EntregadorResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<EntregadorResposta>> Obter(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _servico.ObterAsync(id, cancellationToken));
    }

    /// <summary>
    /// Devolve a ficha médica completa, sem cache. Só o próprio entregador.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A ficha.</returns>
    /// <response code="200">Ficha encontrada.</response>
    /// <response code="401">Sem token válido.</response>
    /// <response code="403">Outro entregador ou token de socorrista.</response>
    /// <response code="404">Entregador inexistente ou sem ficha cadastrada.</response>
    [HttpGet("{id:guid}/ficha-medica")]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<FichaMedicaResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<FichaMedicaResposta>> ObterFichaMedica(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _servico.ObterFichaMedicaAsync(id, cancellationToken));
    }

    /// <summary>
    /// Cria ou substitui a ficha médica do entregador, sem cache. Só o próprio entregador.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="requisicao">Dados da ficha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A ficha gravada.</returns>
    /// <response code="200">Ficha criada ou atualizada.</response>
    /// <response code="400">Tipo sanguíneo fora da lista ou texto longo demais.</response>
    /// <response code="401">Sem token válido.</response>
    /// <response code="403">Outro entregador ou token de socorrista.</response>
    /// <response code="404">Entregador inexistente.</response>
    [HttpPut("{id:guid}/ficha-medica")]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<FichaMedicaResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<FichaMedicaResposta>> DefinirFichaMedica(
        Guid id,
        FichaMedicaRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        return Ok(await _servico.DefinirFichaMedicaAsync(id, requisicao, cancellationToken));
    }

    /// <summary>
    /// Troca o telefone do entregador. Só o próprio entregador.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="requisicao">Telefone novo.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O corpo do <c>GET /entregadores/{id}</c>.</returns>
    /// <response code="200">Telefone trocado.</response>
    /// <response code="400">Telefone fora da regra.</response>
    /// <response code="401">Sem token válido.</response>
    /// <response code="403">Outro entregador ou token de socorrista.</response>
    /// <response code="404">Entregador inexistente.</response>
    /// <response code="409">Telefone já usado por outro entregador.</response>
    [HttpPut("{id:guid}/telefone")]
    [ProducesResponseType<EntregadorResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EntregadorResposta>> TrocarTelefone(
        Guid id,
        TelefoneRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        return Ok(await _servico.TrocarTelefoneAsync(id, requisicao, cancellationToken));
    }

    /// <summary>
    /// Adiciona um contato de emergência ao entregador. Só o próprio entregador.
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
    /// <response code="401">Sem token válido.</response>
    /// <response code="403">Outro entregador ou token de socorrista.</response>
    /// <response code="404">Entregador inexistente.</response>
    [HttpPost("{id:guid}/contatos")]
    [ProducesResponseType<ContatoResposta>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ContatoResposta>> AdicionarContato(
        Guid id,
        ContatoRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        ContatoResposta resposta = await _servico.AdicionarContatoAsync(id, requisicao, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id }, resposta);
    }

    /// <summary>
    /// Edita um contato de emergência (mesmo corpo do <c>POST</c>). Só o próprio entregador.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="contatoId">Id do contato.</param>
    /// <param name="requisicao">Dados do contato.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O contato editado, com o telefone mascarado.</returns>
    /// <response code="200">Contato editado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="401">Sem token válido.</response>
    /// <response code="403">Outro entregador ou token de socorrista.</response>
    /// <response code="404">Entregador inexistente, ou contato inexistente ou de outro entregador.</response>
    [HttpPut("{id:guid}/contatos/{contatoId:guid}")]
    [ProducesResponseType<ContatoResposta>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ContatoResposta>> EditarContato(
        Guid id,
        Guid contatoId,
        ContatoRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        return Ok(await _servico.EditarContatoAsync(id, contatoId, requisicao, cancellationToken));
    }

    /// <summary>
    /// Exclui um contato de emergência (excluir o último é permitido). Só o próprio entregador.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="contatoId">Id do contato.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>Sem corpo.</returns>
    /// <response code="204">Contato excluído.</response>
    /// <response code="401">Sem token válido.</response>
    /// <response code="403">Outro entregador ou token de socorrista.</response>
    /// <response code="404">Entregador inexistente, ou contato inexistente ou de outro entregador.</response>
    [HttpDelete("{id:guid}/contatos/{contatoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> RemoverContato(Guid id, Guid contatoId, CancellationToken cancellationToken)
    {
        await _servico.RemoverContatoAsync(id, contatoId, cancellationToken);
        return NoContent();
    }
}
