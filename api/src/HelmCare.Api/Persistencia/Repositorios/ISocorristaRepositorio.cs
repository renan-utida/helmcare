using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Acesso aos socorristas do Cofre.
/// </summary>
public interface ISocorristaRepositorio : IRepositorio<Socorrista>
{
    /// <summary>
    /// Busca, sem rastrear, o socorrista da conta do Identity.
    /// </summary>
    /// <param name="usuarioId">Id do usuário do Identity.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O socorrista, ou nulo se a conta não é de socorrista.</returns>
    Task<Socorrista?> ObterPorUsuarioAsync(string usuarioId, CancellationToken cancellationToken);
}
