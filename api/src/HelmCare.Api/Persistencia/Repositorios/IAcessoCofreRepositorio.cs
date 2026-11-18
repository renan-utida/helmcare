using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Acesso à auditoria do Cofre (<see cref="AcessoCofre"/>).
/// </summary>
public interface IAcessoCofreRepositorio : IRepositorio<AcessoCofre>
{
    /// <summary>
    /// Busca, sem rastrear, a última concessão desse socorrista para esse entregador, que descreve a
    /// janela de acesso atual do par (negações não contam).
    /// </summary>
    /// <param name="socorristaId">Socorrista.</param>
    /// <param name="entregadorId">Entregador dono da ficha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A última concessão, ou nulo se o par nunca teve acesso concedido.</returns>
    Task<AcessoCofre?> ObterUltimaConcessaoAsync(Guid socorristaId, Guid entregadorId, CancellationToken cancellationToken);

    /// <summary>
    /// Lista a auditoria do entregador, do acesso mais recente ao mais antigo, com o nome e a
    /// organização do socorrista. Nunca traz o <c>ip</c>.
    /// </summary>
    /// <param name="entregadorId">Entregador dono da ficha.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>Os itens da auditoria.</returns>
    Task<IReadOnlyList<ItemAuditoriaCofre>> ListarAuditoriaAsync(Guid entregadorId, CancellationToken cancellationToken);
}
