using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Repositório de capacetes em memória.
/// </summary>
public sealed class FakeDispositivoRepositorio : IDispositivoRepositorio
{
    private readonly List<Dispositivo> _dispositivos = [];

    /// <summary>
    /// Capacetes adicionados.
    /// </summary>
    public IReadOnlyList<Dispositivo> Dispositivos => _dispositivos;

    /// <summary>
    /// Coloca um capacete na lista, como se já estivesse no banco.
    /// </summary>
    /// <param name="dispositivo">Capacete existente.</param>
    public void Semear(Dispositivo dispositivo)
    {
        _dispositivos.Add(dispositivo);
    }

    /// <inheritdoc/>
    public Task<Dispositivo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_dispositivos.FirstOrDefault(d => d.Id == id));
    }

    /// <inheritdoc/>
    public Task AdicionarAsync(Dispositivo entidade, CancellationToken cancellationToken)
    {
        _dispositivos.Add(entidade);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Dispositivo?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken)
    {
        return Task.FromResult(_dispositivos.FirstOrDefault(d => d.Codigo == codigo));
    }

    /// <inheritdoc/>
    public Task<bool> CodigoJaCadastradoAsync(string codigo, CancellationToken cancellationToken)
    {
        return Task.FromResult(_dispositivos.Exists(d => d.Codigo == codigo));
    }

    /// <inheritdoc/>
    public Task<bool> EntregadorJaTemDispositivoAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_dispositivos.Exists(d => d.EntregadorId == entregadorId));
    }

    /// <inheritdoc/>
    public Task<Dispositivo?> ObterDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_dispositivos.FirstOrDefault(d => d.EntregadorId == entregadorId));
    }
}
