using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Implementação do <see cref="IEntregadorRepositorio"/> com o EF Core.
/// </summary>
public sealed class EntregadorRepositorio : RepositorioBase<Entregador>, IEntregadorRepositorio
{
    /// <summary>
    /// Cria o repositório.
    /// </summary>
    /// <param name="contexto">Contexto do EF Core da requisição.</param>
    public EntregadorRepositorio(AppDbContext contexto)
        : base(contexto)
    {
    }

    /// <inheritdoc/>
    public Task<Entregador?> ObterComCadastroAsync(Guid id, CancellationToken cancellationToken)
    {
        return Conjunto
            .Include(e => e.FichaMedica)
            .Include(e => e.Contatos)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> CpfJaCadastradoAsync(string cpf, CancellationToken cancellationToken)
    {
        return Conjunto.AnyAsync(e => e.Cpf == cpf, cancellationToken);
    }
}
