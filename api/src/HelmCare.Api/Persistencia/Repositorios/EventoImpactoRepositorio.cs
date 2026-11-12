using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Implementação do <see cref="IEventoImpactoRepositorio"/> com o EF Core.
/// </summary>
public sealed class EventoImpactoRepositorio : RepositorioBase<EventoImpacto>, IEventoImpactoRepositorio
{
    /// <summary>
    /// Cria o repositório.
    /// </summary>
    /// <param name="contexto">Contexto do EF Core da operação.</param>
    public EventoImpactoRepositorio(AppDbContext contexto)
        : base(contexto)
    {
    }

    /// <inheritdoc/>
    public Task<EventoImpacto?> ObterEmAbertoMaisRecenteAsync(Guid dispositivoId, CancellationToken cancellationToken)
    {
        // Usa o índice (DispositivoId, OcorridoEm). "Em aberto" é Resultado nulo (EventoImpacto.EstaEmAberto).
        return Conjunto
            .Where(e => e.DispositivoId == dispositivoId && e.Resultado == null)
            .OrderByDescending(e => e.OcorridoEm)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
