using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Acesso aos voluntários da rede de socorro.
/// </summary>
public interface IVoluntarioRepositorio : IRepositorio<Voluntario>
{
    /// <summary>
    /// Diz se o voluntário existe.
    /// </summary>
    /// <param name="id">Id do voluntário.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns><see langword="true"/> se existe.</returns>
    Task<bool> ExisteAsync(Guid id, CancellationToken cancellationToken);
}
