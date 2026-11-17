using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Repositório de voluntários em memória.
/// </summary>
public sealed class FakeVoluntarioRepositorio : IVoluntarioRepositorio
{
    private readonly List<Voluntario> _voluntarios = [];

    /// <summary>
    /// Voluntários adicionados ou semeados.
    /// </summary>
    public IReadOnlyList<Voluntario> Voluntarios => _voluntarios;

    /// <summary>
    /// Coloca um voluntário na lista, como se já estivesse no banco.
    /// </summary>
    /// <param name="voluntario">Voluntário existente.</param>
    public void Semear(Voluntario voluntario)
    {
        _voluntarios.Add(voluntario);
    }

    /// <inheritdoc/>
    public Task<Voluntario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_voluntarios.FirstOrDefault(v => v.Id == id));
    }

    /// <inheritdoc/>
    public Task<Voluntario?> ObterParaConsultaAsync(Guid id, CancellationToken cancellationToken)
    {
        return ObterPorIdAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task AdicionarAsync(Voluntario entidade, CancellationToken cancellationToken)
    {
        _voluntarios.Add(entidade);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> ExisteAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_voluntarios.Exists(v => v.Id == id));
    }

    /// <inheritdoc/>
    public Task<bool> ExisteDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_voluntarios.Exists(v => v.EntregadorId == entregadorId));
    }

    /// <inheritdoc/>
    public Task<Guid?> ObterIdDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_voluntarios.Where(v => v.EntregadorId == entregadorId).Select(v => (Guid?)v.Id).FirstOrDefault());
    }
}
