using System;
using System.Threading;
using System.Threading.Tasks;

namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Previsão fixa, com os valores do exemplo da seção 8 do contrato (7,0 mm/h, 24,0 °C e 85%).
/// </summary>
/// <remarks>
/// Na sexta entra a implementação com a Open-Meteo; esta continua como reserva, para o turno
/// abrir mesmo sem acesso à previsão real.
/// </remarks>
public sealed class PrevisaoTempoSimulada : IPrevisaoTempo
{
    /// <summary>
    /// Previsão devolvida para qualquer período.
    /// </summary>
    public static readonly PrevisaoTurno Exemplo = new(ChuvaMmHora: 7.0, TemperaturaC: 24.0, UmidadePct: 85);

    /// <inheritdoc/>
    public Task<PrevisaoTurno> ObterAsync(DateTime inicio, DateTime fim, CancellationToken cancellationToken)
    {
        return Task.FromResult(Exemplo);
    }
}
