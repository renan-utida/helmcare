using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Seguranca;

/// <summary>
/// <see cref="IEmissorTokens"/> com o Identity: as claims vêm do <see cref="SignInManager{TUser}"/>
/// (a mesma fábrica do login e da renovação) e os tokens, do <see cref="ProtetorTokens"/> com as
/// opções do esquema bearer do Identity.
/// </summary>
public sealed class EmissorTokensIdentity : IEmissorTokens
{
    private readonly SignInManager<IdentityUser> _entrada;
    private readonly UserManager<IdentityUser> _usuarios;
    private readonly IOptionsMonitor<BearerTokenOptions> _opcoesBearer;
    private readonly TimeProvider _relogio;

    /// <summary>
    /// Cria o emissor.
    /// </summary>
    /// <param name="entrada">Monta as claims e confere o security stamp.</param>
    /// <param name="usuarios">Busca o usuário pelo id.</param>
    /// <param name="opcoesBearer">Opções do esquema bearer (protetores e validades).</param>
    /// <param name="relogio">Fonte do instante atual.</param>
    public EmissorTokensIdentity(
        SignInManager<IdentityUser> entrada,
        UserManager<IdentityUser> usuarios,
        IOptionsMonitor<BearerTokenOptions> opcoesBearer,
        TimeProvider relogio)
    {
        _entrada = entrada;
        _usuarios = usuarios;
        _opcoesBearer = opcoesBearer;
        _relogio = relogio;
    }

    /// <inheritdoc/>
    public async Task<TokensEmitidos> EmitirAsync(string usuarioId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IdentityUser usuario = await _usuarios.FindByIdAsync(usuarioId)
            ?? throw new InvalidOperationException("Usuário do Identity não encontrado para emitir o token.");

        ClaimsPrincipal principal = await _entrada.CreateUserPrincipalAsync(usuario);
        return Protetor().Emitir(principal);
    }

    /// <inheritdoc/>
    public async Task<string?> ValidarRenovacaoAsync(string refreshToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Protetor().AbrirRenovacao(refreshToken) is not { } principal)
        {
            return null;
        }

        IdentityUser? usuario = await _entrada.ValidateSecurityStampAsync(principal);
        return usuario?.Id;
    }

    private ProtetorTokens Protetor()
    {
        return new ProtetorTokens(_opcoesBearer.Get(IdentityConstants.BearerScheme), _relogio);
    }
}
