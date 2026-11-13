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
}
