using System;
using System.Threading;
using System.Threading.Tasks;

namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Cálculo do Índice de Risco do Turno. A API é a porta de entrada: o app nunca chama o
/// Serviço de Risco direto (seção 1 do contrato).
/// </summary>
public interface IServicoRisco
{
    /// <summary>
    /// Calcula o índice do turno.
    /// </summary>
    /// <param name="inicio">Início do turno, em UTC.</param>
    /// <param name="fim">Fim previsto do turno, em UTC.</param>
    /// <param name="previsao">Previsão do tempo do período.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>O índice, a faixa, os componentes e a explicação.</returns>
    Task<ResultadoRisco> CalcularAsync(DateTime inicio, DateTime fim, PrevisaoTurno previsao, CancellationToken cancellationToken);
}
