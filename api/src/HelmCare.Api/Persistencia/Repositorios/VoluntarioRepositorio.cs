using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Implementação do <see cref="IVoluntarioRepositorio"/> com o EF Core.
/// </summary>
public sealed class VoluntarioRepositorio : RepositorioBase<Voluntario>, IVoluntarioRepositorio
{
    /// <summary>
    /// Cria o repositório.
    /// </summary>
    /// <param name="contexto">Contexto do EF Core da requisição.</param>
    public VoluntarioRepositorio(AppDbContext contexto)
        : base(contexto)
    {
    }

    /// <inheritdoc/>
    public Task<bool> ExisteAsync(Guid id, CancellationToken cancellationToken)
    {
        return Conjunto.AnyAsync(v => v.Id == id, cancellationToken);
    }
}
