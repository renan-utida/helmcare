using System;

namespace HelmCare.Dominio.Comum;

/// <summary>
/// Índice de calor da NOAA (seção 6.1 do contrato), o mesmo que o capacete calcula com
/// <c>computeHeatIndex(temperatura, umidade, false)</c> da DHT sensor library (Adafruit).
/// A conta é feita em Fahrenheit e o resultado volta em Celsius. O Serviço de Risco usa a
/// mesma fórmula no componente <c>calor</c>, e os valores de referência da seção 6.1
/// (33,4 °C e 58% dão 39,8 °C; 23,5 °C e 58% dão 23,4 °C) valem para as duas partes.
/// </summary>
public static class IndiceCalor
{
    /// <summary>
    /// Calcula o índice de calor, em °C, sem arredondar (o capacete publica com uma casa decimal).
    /// </summary>
    /// <param name="temperaturaC">Temperatura do ar, em °C.</param>
    /// <param name="umidadePct">Umidade relativa, em %.</param>
    /// <returns>O índice de calor, em °C.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Quando algum valor não é um número finito.</exception>
    public static double CalcularC(double temperaturaC, double umidadePct)
    {
        if (!double.IsFinite(temperaturaC))
        {
            throw new ArgumentOutOfRangeException(nameof(temperaturaC), "A temperatura deve ser um número finito.");
        }

        if (!double.IsFinite(umidadePct))
        {
            throw new ArgumentOutOfRangeException(nameof(umidadePct), "A umidade deve ser um número finito.");
        }

        double t = (temperaturaC * 1.8) + 32;
        double u = umidadePct;

        // Fórmula simplificada de Steadman, que vale para o calor ameno.
        double indice = 0.5 * (t + 61.0 + ((t - 68.0) * 1.2) + (u * 0.094));

        // Acima de 79 °F (cerca de 26 °C) entra a regressão de Rothfusz. A biblioteca usa "> 79",
        // e não os 80 °F do texto da NOAA; aqui fica igual à biblioteca, que é a referência do contrato.
        if (indice > 79)
        {
            indice = -42.379
                + (2.04901523 * t)
                + (10.14333127 * u)
                - (0.22475541 * t * u)
                - (0.00683783 * t * t)
                - (0.05481717 * u * u)
                + (0.00122874 * t * t * u)
                + (0.00085282 * t * u * u)
                - (0.00000199 * t * t * u * u);

            // Ajustes de umidade da NOAA: ar muito seco ou muito úmido em certas temperaturas.
            if (u < 13 && t >= 80.0 && t <= 112.0)
            {
                indice -= (13.0 - u) * 0.25 * Math.Sqrt((17.0 - Math.Abs(t - 95.0)) * 0.05882);
            }
            else if (u > 85.0 && t >= 80.0 && t <= 87.0)
            {
                indice += (u - 85.0) * 0.1 * ((87.0 - t) * 0.2);
            }
        }

        // A biblioteca converte de volta com 0,55555 (e não 5/9); mantido para dar o mesmo número.
        return (indice - 32) * 0.55555;
    }
}
