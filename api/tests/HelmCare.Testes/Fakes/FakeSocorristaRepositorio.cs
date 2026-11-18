using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Socorristas em memória.
/// </summary>
public sealed class FakeSocorristaRepositorio : ISocorristaRepositorio
{
    private readonly List<Socorrista> _socorristas = [];

    /// <summary>
    /// Socorristas guardados.
    /// </summary>
    public IReadOnlyList<Socorrista> Socorristas => _socorristas;

    /// <summary>
    /// Semeia um socorrista.
    /// </summary>
    /// <param name="socorrista">Socorrista existente.</param>
    public void Semear(Socorrista socorrista)
    {
        _socorristas.Add(socorrista);
    }

    /// <inheritdoc/>
    public Task<Socorrista?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_socorristas.FirstOrDefault(s => s.Id == id));
    }

    /// <inheritdoc/>
    public Task AdicionarAsync(Socorrista entidade, CancellationToken cancellationToken)
    {
        _socorristas.Add(entidade);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Socorrista?> ObterPorUsuarioAsync(string usuarioId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_socorristas.FirstOrDefault(s => s.UsuarioId == usuarioId));
    }
}
