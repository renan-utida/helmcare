using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace HelmCare.Api.Seguranca;

/// <summary>
/// <see cref="IUsuarioAtual"/> da requisição HTTP, lido do <see cref="HttpContext.User"/> que o
/// esquema bearer do Identity preencheu.
/// </summary>
public sealed class UsuarioAtualHttp : IUsuarioAtual
{
    private readonly IHttpContextAccessor _acesso;

    /// <summary>
    /// Cria o leitor do usuário atual.
    /// </summary>
    /// <param name="acesso">Acesso ao contexto da requisição.</param>
    public UsuarioAtualHttp(IHttpContextAccessor acesso)
    {
        _acesso = acesso;
    }

    /// <inheritdoc/>
    public bool Autenticado => Usuario?.Identity?.IsAuthenticated == true;

    /// <inheritdoc/>
    public bool EhEntregador => Autenticado && Usuario!.IsInRole(Papeis.Entregador);

    /// <inheritdoc/>
    public bool EhSocorrista => Autenticado && Usuario!.IsInRole(Papeis.Socorrista);

    /// <inheritdoc/>
    public Guid? EntregadorId => LerId(ClaimsHelmCare.EntregadorId);

    /// <inheritdoc/>
    public Guid? SocorristaId => LerId(ClaimsHelmCare.SocorristaId);

    private ClaimsPrincipal? Usuario => _acesso.HttpContext?.User;

    private Guid? LerId(string tipo)
    {
        if (!Autenticado)
        {
            return null;
        }

        return Guid.TryParse(Usuario!.FindFirstValue(tipo), out Guid id) ? id : null;
    }
}
