using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Seguranca;

/// <summary>
/// Monta as claims do token do Identity. É a mesma fábrica no login e na renovação (o Identity a
/// chama nos dois), então o token novo tem sempre as mesmas claims: o id do usuário, o security
/// stamp, o papel e o id do perfil (<see cref="ClaimsHelmCare.EntregadorId"/> ou
/// <see cref="ClaimsHelmCare.SocorristaId"/>).
/// </summary>
/// <remarks>
/// O nome de usuário e o e-mail, que a fábrica padrão coloca no token, são retirados: o token é
/// cifrado, mas nenhuma rota precisa deles, e o e-mail é PII (seção 9 do contrato).
/// </remarks>
public sealed class FabricaClaimsUsuario : UserClaimsPrincipalFactory<IdentityUser, IdentityRole>
{
    private readonly AppDbContext _contexto;

    /// <summary>
    /// Cria a fábrica.
    /// </summary>
    /// <param name="userManager">Gerenciador de usuários do Identity.</param>
    /// <param name="roleManager">Gerenciador de papéis do Identity.</param>
    /// <param name="options">Opções do Identity (nomes das claims padrão).</param>
    /// <param name="contexto">Contexto do EF Core, para achar o perfil do usuário.</param>
    public FabricaClaimsUsuario(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options,
        AppDbContext contexto)
        : base(userManager, roleManager, options)
    {
        _contexto = contexto;
    }

    /// <inheritdoc/>
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(IdentityUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        ClaimsIdentity identidade = await base.GenerateClaimsAsync(user);

        foreach (Claim pessoal in identidade.Claims
            .Where(c => c.Type == Options.ClaimsIdentity.UserNameClaimType || c.Type == Options.ClaimsIdentity.EmailClaimType)
            .ToList())
        {
            identidade.RemoveClaim(pessoal);
        }

        Guid? entregadorId = await _contexto.Entregadores
            .AsNoTracking()
            .Where(e => e.UsuarioId == user.Id)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync();

        if (entregadorId is { } idEntregador)
        {
            identidade.AddClaim(new Claim(ClaimsHelmCare.EntregadorId, idEntregador.ToString("D", CultureInfo.InvariantCulture)));
        }

        Guid? socorristaId = await _contexto.Socorristas
            .AsNoTracking()
            .Where(s => s.UsuarioId == user.Id)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync();

        if (socorristaId is { } idSocorrista)
        {
            identidade.AddClaim(new Claim(ClaimsHelmCare.SocorristaId, idSocorrista.ToString("D", CultureInfo.InvariantCulture)));
        }

        return identidade;
    }
}
