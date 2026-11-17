using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;

namespace HelmCare.Api.Seguranca;

/// <summary>
/// Gera e abre os tokens com os protetores do <see cref="BearerTokenOptions"/> do esquema bearer do
/// Identity, do mesmo jeito que o <c>BearerTokenHandler</c> faz no <c>SignIn</c>. Assim, o token que
/// o login devolve é aceito pelo próprio esquema bearer nas outras rotas, e o login pode devolver o
/// corpo do contrato (com os campos da sessão), que a resposta automática do <c>SignIn</c> não tem.
/// </summary>
/// <remarks>
/// Não depende do Identity nem do banco, só dos protetores e do relógio: os testes usam os mesmos
/// protetores do Data Protection da aplicação.
/// </remarks>
public sealed class ProtetorTokens
{
    private readonly BearerTokenOptions _opcoes;
    private readonly TimeProvider _relogio;

    /// <summary>
    /// Cria o protetor.
    /// </summary>
    /// <param name="opcoes">Opções do esquema bearer, com os protetores já configurados pelo Data Protection.</param>
    /// <param name="relogio">Fonte do instante atual (validade dos tokens).</param>
    /// <exception cref="InvalidOperationException">Quando os protetores não foram configurados.</exception>
    public ProtetorTokens(BearerTokenOptions opcoes, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(relogio);

        if (opcoes.BearerTokenProtector is null || opcoes.RefreshTokenProtector is null)
        {
            throw new InvalidOperationException("Os protetores do esquema bearer não foram configurados.");
        }

        _opcoes = opcoes;
        _relogio = relogio;
    }

    /// <summary>
    /// Gera o token de acesso e o de renovação para o usuário, cada um com a própria validade.
    /// </summary>
    /// <param name="principal">Usuário, com as claims da <see cref="FabricaClaimsUsuario"/>.</param>
    /// <returns>Os dois tokens e a validade do de acesso, em segundos.</returns>
    public TokensEmitidos Emitir(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        DateTimeOffset agora = _relogio.GetUtcNow();

        var acesso = new AuthenticationTicket(
            principal,
            new AuthenticationProperties { ExpiresUtc = agora + _opcoes.BearerTokenExpiration },
            $"{IdentityConstants.BearerScheme}:AccessToken");

        var renovacao = new AuthenticationTicket(
            principal,
            new AuthenticationProperties { ExpiresUtc = agora + _opcoes.RefreshTokenExpiration },
            $"{IdentityConstants.BearerScheme}:RefreshToken");

        return new TokensEmitidos(
            _opcoes.BearerTokenProtector.Protect(acesso),
            (long)_opcoes.BearerTokenExpiration.TotalSeconds,
            _opcoes.RefreshTokenProtector.Protect(renovacao));
    }

    /// <summary>
    /// Abre um token de acesso com a mesma regra do esquema bearer: ilegível ou vencido dá nulo.
    /// </summary>
    /// <param name="token">Token de acesso.</param>
    /// <returns>O usuário do token, ou nulo.</returns>
    public ClaimsPrincipal? AbrirAcesso(string token)
    {
        return Abrir(_opcoes.BearerTokenProtector, token);
    }

    /// <summary>
    /// Abre um token de renovação: ilegível ou vencido dá nulo. Um token de acesso não serve aqui,
    /// porque os dois protetores têm finalidades diferentes.
    /// </summary>
    /// <param name="token">Token de renovação.</param>
    /// <returns>O usuário do token, ou nulo.</returns>
    public ClaimsPrincipal? AbrirRenovacao(string token)
    {
        return Abrir(_opcoes.RefreshTokenProtector, token);
    }

    private ClaimsPrincipal? Abrir(ISecureDataFormat<AuthenticationTicket> protetor, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // O Unprotect devolve nulo (sem lançar) para um texto adulterado ou cifrado com outra chave.
        AuthenticationTicket? ticket = protetor.Unprotect(token);

        if (ticket?.Properties.ExpiresUtc is not { } expira || _relogio.GetUtcNow() >= expira)
        {
            return null;
        }

        return ticket.Principal;
    }
}
