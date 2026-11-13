using System;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Mensageria.Mensagens;

/// <summary>
/// Telemetria do capacete (seção 6.1 do contrato), publicada a cada 10 s em todo estado
/// diferente de <c>DESARMADO</c>. Os nomes JSON são os do contrato, em camelCase.
/// </summary>
public sealed class TelemetriaMensagem
{
    /// <summary>
    /// <c>dispositivo</c>: código do capacete (ex.: <c>HC-0001</c>).
    /// </summary>
    public required string Dispositivo { get; init; }

    /// <summary>
    /// <c>ts</c>: instante da leitura no relógio do capacete, em UTC.
    /// </summary>
    public required DateTime Ts { get; init; }

    /// <summary>
    /// <c>estado</c>: estado atual do capacete.
    /// </summary>
    public required EstadoDispositivo Estado { get; init; }

    /// <summary>
    /// <c>temperaturaC</c>: temperatura em °C, ou nulo enquanto o DHT22 não leu.
    /// </summary>
    public double? TemperaturaC { get; init; }

    /// <summary>
    /// <c>umidadePct</c>: umidade relativa em %, ou nulo enquanto o DHT22 não leu.
    /// </summary>
    public double? UmidadePct { get; init; }

    /// <summary>
    /// <c>indiceCalorC</c>: índice de calor em °C, ou nulo enquanto o DHT22 não leu.
    /// </summary>
    public double? IndiceCalorC { get; init; }

    /// <summary>
    /// <c>presenca</c>: capacete na cabeça.
    /// </summary>
    public required bool Presenca { get; init; }

    /// <summary>
    /// <c>velocidadeKmh</c>: velocidade simulada, em km/h.
    /// </summary>
    public required double VelocidadeKmh { get; init; }

    /// <summary>
    /// <c>bateriaPct</c>: carga da bateria em %, opcional (o capacete do protótipo não envia).
    /// </summary>
    public int? BateriaPct { get; init; }
}
