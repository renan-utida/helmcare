using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Comum;
using Microsoft.EntityFrameworkCore;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Implementação das operações comuns com o EF Core. Cada repositório específico herda desta
/// classe e acrescenta só as consultas próprias da sua entidade.
/// </summary>
/// <typeparam name="T">Entidade do domínio.</typeparam>
public abstract class RepositorioBase<T> : IRepositorio<T>
    where T : Entidade
{
    /// <summary>
    /// Cria o repositório sobre o contexto da requisição.
    /// </summary>
    /// <param name="contexto">Contexto do EF Core (um por requisição).</param>
    protected RepositorioBase(AppDbContext contexto)
    {
        Contexto = contexto;
    }

    /// <summary>
    /// Contexto do EF Core, compartilhado com a <see cref="IUnidadeDeTrabalho"/> da mesma requisição.
    /// </summary>
    protected AppDbContext Contexto { get; }

    /// <summary>
    /// Tabela da entidade.
    /// </summary>
    protected DbSet<T> Conjunto => Contexto.Set<T>();

    /// <inheritdoc/>
    public Task<T?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Conjunto.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AdicionarAsync(T entidade, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entidade);
        await Conjunto.AddAsync(entidade, cancellationToken);
    }
}
