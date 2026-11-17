using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Entidades;
using Microsoft.Extensions.Logging;

namespace HelmCare.Api.Servicos.Identidade;

/// <summary>
/// Login, renovação do token e sessão do entregador (seção 7 do contrato, "Identidade e
/// autorização"). E-mail ou senha errados, conta bloqueada e conta de socorrista dão o mesmo
/// <c>401</c>, com o mesmo texto, para não revelar se o e-mail existe.
/// </summary>
/// <remarks>
/// O log leva só o id do perfil e o resultado. E-mail, senha e tokens nunca vão para o log; com um
/// e-mail desconhecido, a linha não leva nada do e-mail.
/// </remarks>
public sealed partial class AutenticacaoEntregadorServico
{
    /// <summary>
    /// Texto único do <c>401</c> do login, igual nos três casos.
    /// </summary>
    public const string MensagemLoginInvalido = "E-mail ou senha inválidos.";

    /// <summary>
    /// Texto do <c>401</c> da renovação com um token inválido ou vencido.
    /// </summary>
    public const string MensagemRenovacaoInvalida = "O refreshToken é inválido ou venceu. Faça o login de novo.";

    private readonly IContasIdentity _contas;
    private readonly IEmissorTokens _tokens;
    private readonly IEntregadorRepositorio _entregadores;
    private readonly IVoluntarioRepositorio _voluntarios;
    private readonly IDispositivoRepositorio _dispositivos;
    private readonly ITurnoRepositorio _turnos;
    private readonly IOcorrenciaRepositorio _ocorrencias;
    private readonly IAutorizacaoRecursos _autorizacao;
    private readonly ILogger<AutenticacaoEntregadorServico> _logger;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="contas">Contas do Identity (conferência da senha e bloqueio).</param>
    /// <param name="tokens">Emissão e renovação dos tokens.</param>
    /// <param name="entregadores">Entregadores (perfil da conta).</param>
    /// <param name="voluntarios">Voluntários (campo <c>voluntarioId</c> da sessão).</param>
    /// <param name="dispositivos">Capacetes (campo <c>codigoDispositivo</c>).</param>
    /// <param name="turnos">Turnos (campo <c>turnoAtivoId</c>).</param>
    /// <param name="ocorrencias">Ocorrências (campo <c>ocorrenciaAtivaId</c>).</param>
    /// <param name="autorizacao">Entregador do token, na sessão.</param>
    /// <param name="logger">Log da aplicação.</param>
    public AutenticacaoEntregadorServico(
        IContasIdentity contas,
        IEmissorTokens tokens,
        IEntregadorRepositorio entregadores,
        IVoluntarioRepositorio voluntarios,
        IDispositivoRepositorio dispositivos,
        ITurnoRepositorio turnos,
        IOcorrenciaRepositorio ocorrencias,
        IAutorizacaoRecursos autorizacao,
        ILogger<AutenticacaoEntregadorServico> logger)
    {
        _contas = contas;
        _tokens = tokens;
        _entregadores = entregadores;
        _voluntarios = voluntarios;
        _dispositivos = dispositivos;
        _turnos = turnos;
        _ocorrencias = ocorrencias;
        _autorizacao = autorizacao;
        _logger = logger;
    }

    /// <summary>
    /// Login com e-mail e senha.
    /// </summary>
    /// <param name="requisicao">E-mail e senha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>Os tokens e a sessão.</returns>
    /// <exception cref="NaoAutenticadoException">Credenciais inválidas, conta bloqueada ou conta de outro papel (401).</exception>
    public async Task<LoginEntregadorResposta> EntrarAsync(LoginRequisicao requisicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        VerificacaoSenha verificacao = await _contas.VerificarSenhaAsync(requisicao.Email, requisicao.Senha, cancellationToken);

        Entregador? entregador = verificacao.UsuarioId is { } usuarioId
            ? await _entregadores.ObterPorUsuarioAsync(usuarioId, cancellationToken)
            : null;

        if (verificacao.Situacao == SituacaoLogin.Bloqueada)
        {
            RegistrarBloqueio(_logger, entregador?.Id);
            throw new NaoAutenticadoException(MensagemLoginInvalido);
        }

        // Conta de socorrista (ou sem perfil de entregador) com a senha certa: mesmo 401 da senha errada.
        if (verificacao.Situacao != SituacaoLogin.Valida
            || !verificacao.Papeis.Contains(Papeis.Entregador)
            || entregador is null)
        {
            RegistrarFalha(_logger, entregador?.Id);
            throw new NaoAutenticadoException(MensagemLoginInvalido);
        }

        TokensEmitidos tokens = await _tokens.EmitirAsync(verificacao.UsuarioId!, cancellationToken);
        SessaoEntregadorResposta sessao = await MontarSessaoAsync(entregador, cancellationToken);

        RegistrarSucesso(_logger, entregador.Id);
        return LoginEntregadorResposta.De(tokens, sessao);
    }

    /// <summary>
    /// Troca o token de renovação por um par novo, com as mesmas claims e a sessão atualizada.
    /// </summary>
    /// <param name="requisicao">Token de renovação.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O mesmo corpo do login.</returns>
    /// <exception cref="NaoAutenticadoException">Token inválido, vencido ou de outro papel (401).</exception>
    public async Task<LoginEntregadorResposta> RenovarAsync(RenovarTokenRequisicao requisicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        string? usuarioId = await _tokens.ValidarRenovacaoAsync(requisicao.RefreshToken, cancellationToken);

        Entregador? entregador = usuarioId is null
            ? null
            : await _entregadores.ObterPorUsuarioAsync(usuarioId, cancellationToken);

        if (entregador is null)
        {
            RegistrarRenovacaoRecusada(_logger);
            throw new NaoAutenticadoException(MensagemRenovacaoInvalida);
        }

        TokensEmitidos tokens = await _tokens.EmitirAsync(usuarioId!, cancellationToken);
        SessaoEntregadorResposta sessao = await MontarSessaoAsync(entregador, cancellationToken);

        RegistrarRenovacao(_logger, entregador.Id);
        return LoginEntregadorResposta.De(tokens, sessao);
    }

    /// <summary>
    /// Sessão do entregador do token: o corpo do login sem os campos do token.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A sessão.</returns>
    /// <exception cref="NaoAutenticadoException">Sem token (401).</exception>
    /// <exception cref="AcessoNegadoException">Token de outro papel (403).</exception>
    /// <exception cref="RecursoNaoEncontradoException">O entregador do token não existe mais (404).</exception>
    public async Task<SessaoEntregadorResposta> ObterSessaoAsync(CancellationToken cancellationToken)
    {
        Guid entregadorId = _autorizacao.ExigirEntregador();

        Entregador entregador = await _entregadores.ObterPorIdAsync(entregadorId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Entregador não encontrado.");

        return await MontarSessaoAsync(entregador, cancellationToken);
    }

    private async Task<SessaoEntregadorResposta> MontarSessaoAsync(Entregador entregador, CancellationToken cancellationToken)
    {
        Dispositivo? capacete = await _dispositivos.ObterDoEntregadorAsync(entregador.Id, cancellationToken);

        return new SessaoEntregadorResposta
        {
            EntregadorId = entregador.Id,
            Nome = entregador.Nome,
            VoluntarioId = await _voluntarios.ObterIdDoEntregadorAsync(entregador.Id, cancellationToken),
            CodigoDispositivo = capacete?.Codigo,
            TurnoAtivoId = await _turnos.ObterIdAtivoDoEntregadorAsync(entregador.Id, cancellationToken),
            OcorrenciaAtivaId = await _ocorrencias.ObterIdAtivaDoEntregadorAsync(entregador.Id, cancellationToken),
        };
    }

    [LoggerMessage(EventId = 800, Level = LogLevel.Information, Message = "Login do entregador {EntregadorId}: sucesso.")]
    private static partial void RegistrarSucesso(ILogger logger, Guid entregadorId);

    [LoggerMessage(EventId = 801, Level = LogLevel.Warning, Message = "Login recusado: falha (entregador {EntregadorId}).")]
    private static partial void RegistrarFalha(ILogger logger, Guid? entregadorId);

    [LoggerMessage(EventId = 802, Level = LogLevel.Warning, Message = "Login recusado: bloqueio (entregador {EntregadorId}).")]
    private static partial void RegistrarBloqueio(ILogger logger, Guid? entregadorId);

    [LoggerMessage(EventId = 803, Level = LogLevel.Information, Message = "Token renovado para o entregador {EntregadorId}.")]
    private static partial void RegistrarRenovacao(ILogger logger, Guid entregadorId);

    [LoggerMessage(EventId = 804, Level = LogLevel.Warning, Message = "Renovação de token recusada (token inválido, vencido ou de outro papel).")]
    private static partial void RegistrarRenovacaoRecusada(ILogger logger);
}
