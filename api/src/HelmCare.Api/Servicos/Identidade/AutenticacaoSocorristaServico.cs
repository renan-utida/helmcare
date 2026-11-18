using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Contratos.Socorristas;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Entidades;
using Microsoft.Extensions.Logging;

namespace HelmCare.Api.Servicos.Identidade;

/// <summary>
/// Login do socorrista no Cofre (seção 7 do contrato, 1.16). Senha errada, conta bloqueada e conta de
/// entregador dão o mesmo <c>401</c> do login do entregador; o e-mail desconhecido passa pela mesma
/// verificação de hash (no <see cref="IContasIdentity"/>). Não há renovação para o socorrista.
/// </summary>
/// <remarks>
/// O socorrista não verificado também recebe token: é o Cofre que responde <c>403</c> e registra a
/// tentativa. O log leva só o id do socorrista e o resultado.
/// </remarks>
public sealed partial class AutenticacaoSocorristaServico
{
    private readonly IContasIdentity _contas;
    private readonly IEmissorTokens _tokens;
    private readonly ISocorristaRepositorio _socorristas;
    private readonly ILogger<AutenticacaoSocorristaServico> _logger;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="contas">Contas do Identity.</param>
    /// <param name="tokens">Emissão dos tokens.</param>
    /// <param name="socorristas">Socorristas (perfil da conta).</param>
    /// <param name="logger">Log da aplicação.</param>
    public AutenticacaoSocorristaServico(
        IContasIdentity contas,
        IEmissorTokens tokens,
        ISocorristaRepositorio socorristas,
        ILogger<AutenticacaoSocorristaServico> logger)
    {
        _contas = contas;
        _tokens = tokens;
        _socorristas = socorristas;
        _logger = logger;
    }

    /// <summary>
    /// Login com e-mail e senha.
    /// </summary>
    /// <param name="requisicao">E-mail e senha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>Os tokens e o perfil do socorrista.</returns>
    /// <exception cref="NaoAutenticadoException">Credenciais inválidas, conta bloqueada ou conta de outro papel (401).</exception>
    public async Task<LoginSocorristaResposta> EntrarAsync(LoginRequisicao requisicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        VerificacaoSenha verificacao = await _contas.VerificarSenhaAsync(requisicao.Email, requisicao.Senha, cancellationToken);

        Socorrista? socorrista = verificacao.UsuarioId is { } usuarioId
            ? await _socorristas.ObterPorUsuarioAsync(usuarioId, cancellationToken)
            : null;

        if (verificacao.Situacao == SituacaoLogin.Bloqueada)
        {
            RegistrarBloqueio(_logger, socorrista?.Id);
            throw new NaoAutenticadoException(AutenticacaoEntregadorServico.MensagemLoginInvalido);
        }

        // Conta de entregador (ou sem perfil de socorrista) com a senha certa: mesmo 401 da senha errada.
        if (verificacao.Situacao != SituacaoLogin.Valida
            || !verificacao.Papeis.Contains(Papeis.Socorrista)
            || socorrista is null)
        {
            RegistrarFalha(_logger, socorrista?.Id);
            throw new NaoAutenticadoException(AutenticacaoEntregadorServico.MensagemLoginInvalido);
        }

        TokensEmitidos tokens = await _tokens.EmitirAsync(verificacao.UsuarioId!, cancellationToken);

        RegistrarSucesso(_logger, socorrista.Id);
        return LoginSocorristaResposta.De(tokens, socorrista);
    }

    [LoggerMessage(EventId = 810, Level = LogLevel.Information, Message = "Login do socorrista {SocorristaId}: sucesso.")]
    private static partial void RegistrarSucesso(ILogger logger, Guid socorristaId);

    [LoggerMessage(EventId = 811, Level = LogLevel.Warning, Message = "Login de socorrista recusado: falha (socorrista {SocorristaId}).")]
    private static partial void RegistrarFalha(ILogger logger, Guid? socorristaId);

    [LoggerMessage(EventId = 812, Level = LogLevel.Warning, Message = "Login de socorrista recusado: bloqueio (socorrista {SocorristaId}).")]
    private static partial void RegistrarBloqueio(ILogger logger, Guid? socorristaId);
}
