using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Servicos.Risco;
using Microsoft.Extensions.Logging;

namespace HelmCare.Api.Integracoes.OpenMeteo;

/// <summary>
/// Previsão do <c>POST /turnos</c> com reserva (seção 7 do contrato, 1.17), como o
/// <c>ServicoRiscoComReserva</c>: tenta a Open-Meteo e, em qualquer das quatro causas
/// (<see cref="MotivoFalhaOpenMeteo"/>), usa a <see cref="PrevisaoTempoSimulada"/>. O turno nunca falha
/// por causa do tempo.
/// </summary>
/// <remarks>
/// O log leva só o motivo e o tempo gasto, nunca a coordenada nem o corpo. O cancelamento da
/// requisição do cliente não vira reserva: sobe como está.
/// </remarks>
public sealed partial class PrevisaoTempoComReserva : IPrevisaoTempo
{
    private readonly PrevisaoTempoOpenMeteo _openMeteo;
    private readonly PrevisaoTempoSimulada _simulada;
    private readonly TimeProvider _relogio;
    private readonly ILogger<PrevisaoTempoComReserva> _logger;

    /// <summary>
    /// Cria o decorador.
    /// </summary>
    /// <param name="openMeteo">Previsão real.</param>
    /// <param name="simulada">Previsão simulada da reserva.</param>
    /// <param name="relogio">Relógio, para medir o tempo gasto.</param>
    /// <param name="logger">Log da aplicação.</param>
    public PrevisaoTempoComReserva(
        PrevisaoTempoOpenMeteo openMeteo,
        PrevisaoTempoSimulada simulada,
        TimeProvider relogio,
        ILogger<PrevisaoTempoComReserva> logger)
    {
        _openMeteo = openMeteo;
        _simulada = simulada;
        _relogio = relogio;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PrevisaoTurno> ObterAsync(DateTime inicio, DateTime fim, CancellationToken cancellationToken)
    {
        long inicioMedicao = _relogio.GetTimestamp();

        try
        {
            PrevisaoTurno previsao = await _openMeteo.ObterAsync(inicio, fim, cancellationToken);
            long tempoMs = Milissegundos(inicioMedicao);
            RegistrarSucesso(_logger, tempoMs);
            return previsao;
        }
        catch (OpenMeteoIndisponivelException e)
        {
            long tempoMs = Milissegundos(inicioMedicao);
            RegistrarReserva(_logger, e.Motivo, tempoMs);
            return await _simulada.ObterAsync(inicio, fim, cancellationToken);
        }
    }

    private long Milissegundos(long inicioMedicao)
    {
        return (long)_relogio.GetElapsedTime(inicioMedicao).TotalMilliseconds;
    }

    [LoggerMessage(EventId = 620, Level = LogLevel.Information, Message = "Previsão do turno obtida da Open-Meteo em {TempoMs} ms.")]
    private static partial void RegistrarSucesso(ILogger logger, long tempoMs);

    [LoggerMessage(EventId = 621, Level = LogLevel.Warning, Message = "Open-Meteo indisponível no turno (motivo {Motivo}, {TempoMs} ms). Turno criado com a previsão simulada.")]
    private static partial void RegistrarReserva(ILogger logger, MotivoFalhaOpenMeteo motivo, long tempoMs);
}
