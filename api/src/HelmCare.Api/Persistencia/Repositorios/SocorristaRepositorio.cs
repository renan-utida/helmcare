using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Implementação do <see cref="ISocorristaRepositorio"/> com o EF Core.
/// </summary>
public sealed class SocorristaRepositorio : RepositorioBase<Socorrista>, ISocorristaRepositorio
{
    /// <summary>
    /// Cria o repositório.
    /// </summary>
    /// <param name="contexto">Contexto do EF Core da requisição.</param>
    public SocorristaRepositorio(AppDbContext contexto)
        : base(contexto)
    {
    }

    /// <inheritdoc/>
    public Task<Socorrista?> ObterPorUsuarioAsync(string usuarioId, CancellationToken cancellationToken)
    {
        return Conjunto.AsNoTracking().FirstOrDefaultAsync(s => s.UsuarioId == usuarioId, cancellationToken);
    }
}
