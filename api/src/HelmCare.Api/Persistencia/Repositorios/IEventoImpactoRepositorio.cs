using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Acesso aos impactos registrados pelos capacetes.
/// </summary>
public interface IEventoImpactoRepositorio : IRepositorio<EventoImpacto>
{
    /// <summary>
    /// Busca o impacto em aberto (sem resultado) mais recente do capacete, pela maior
    /// <c>OcorridoEm</c>. É a ele que os eventos seguintes do mesmo impacto se aplicam.
    /// </summary>
    /// <param name="dispositivoId">Id do capacete.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>O impacto em aberto, ou nulo se não houver.</returns>
    Task<EventoImpacto?> ObterEmAbertoMaisRecenteAsync(Guid dispositivoId, CancellationToken cancellationToken);
}
