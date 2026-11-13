using System;
using System.Threading;
using System.Threading.Tasks;

namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Fonte da previsão do tempo usada no Índice de Risco do Turno.
/// </summary>
public interface IPrevisaoTempo
{
    /// <summary>
    /// Obtém a previsão para o período do turno.
    /// </summary>
    /// <param name="inicio">Início do turno, em UTC.</param>
    /// <param name="fim">Fim previsto do turno, em UTC.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>A previsão do período.</returns>
    Task<PrevisaoTurno> ObterAsync(DateTime inicio, DateTime fim, CancellationToken cancellationToken);
}
