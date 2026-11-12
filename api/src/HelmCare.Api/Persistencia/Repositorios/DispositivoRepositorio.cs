using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Implementação do <see cref="IDispositivoRepositorio"/> com o EF Core.
/// </summary>
public sealed class DispositivoRepositorio : RepositorioBase<Dispositivo>, IDispositivoRepositorio
{
    /// <summary>
    /// Cria o repositório.
    /// </summary>
    /// <param name="contexto">Contexto do EF Core da requisição.</param>
    public DispositivoRepositorio(AppDbContext contexto)
        : base(contexto)
    {
    }

    /// <inheritdoc/>
    public Task<Dispositivo?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken)
    {
        return Conjunto.FirstOrDefaultAsync(d => d.Codigo == codigo, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> CodigoJaCadastradoAsync(string codigo, CancellationToken cancellationToken)
    {
        return Conjunto.AnyAsync(d => d.Codigo == codigo, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> EntregadorJaTemDispositivoAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        return Conjunto.AnyAsync(d => d.EntregadorId == entregadorId, cancellationToken);
    }
}
