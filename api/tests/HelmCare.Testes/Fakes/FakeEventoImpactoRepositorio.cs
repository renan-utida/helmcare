using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Repositório de impactos em memória, com a mesma regra do real para o impacto em aberto mais recente.
/// </summary>
public sealed class FakeEventoImpactoRepositorio : IEventoImpactoRepositorio
{
    private readonly List<EventoImpacto> _impactos = [];

    /// <summary>
    /// Impactos adicionados ou semeados.
    /// </summary>
    public IReadOnlyList<EventoImpacto> Impactos => _impactos;

    /// <summary>
    /// Coloca um impacto na lista, como se já estivesse no banco.
    /// </summary>
    /// <param name="impacto">Impacto existente.</param>
    public void Semear(EventoImpacto impacto)
    {
        _impactos.Add(impacto);
    }

    /// <inheritdoc/>
    public Task<EventoImpacto?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_impactos.FirstOrDefault(i => i.Id == id));
    }

    /// <inheritdoc/>
    public Task AdicionarAsync(EventoImpacto entidade, CancellationToken cancellationToken)
    {
        _impactos.Add(entidade);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<EventoImpacto?> ObterEmAbertoMaisRecenteAsync(Guid dispositivoId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_impactos
            .Where(i => i.DispositivoId == dispositivoId && i.Resultado is null)
            .OrderByDescending(i => i.OcorridoEm)
            .FirstOrDefault());
    }
}
