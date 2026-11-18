using System;
using System.Collections.Generic;
using HelmCare.Api.Integracoes.OpenMeteo;
using HelmCare.Dominio.Comum;

namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Regras da previsão do tempo (seção 7 do contrato, 1.17): a agregação das horas do turno, os
/// arredondamentos e as faixas da seção 8. Funções puras, sem rede, usadas pelo turno e pelo clima.
/// </summary>
public static class RegrasPrevisao
{
    /// <summary>
    /// Chuva máxima aceita pelo <c>POST /indice-turno</c>, em mm/h.
    /// </summary>
    public const double ChuvaMaxima = 200;

    /// <summary>
    /// Temperatura mínima aceita, em °C.
    /// </summary>
    public const double TemperaturaMinima = -10;

    /// <summary>
    /// Temperatura máxima aceita, em °C.
    /// </summary>
    public const double TemperaturaMaxima = 50;

    /// <summary>
    /// Agrega as horas do turno numa previsão: a maior chuva, e a temperatura e a umidade da hora de
    /// maior índice de calor (no empate, a primeira hora). O pior momento é o que importa para a segurança.
    /// </summary>
    /// <param name="horas">As horas cheias do turno, na ordem, com os valores da Open-Meteo.</param>
    /// <returns>A previsão, arredondada e dentro das faixas.</returns>
    /// <exception cref="ArgumentException">Sem nenhuma hora.</exception>
    public static PrevisaoTurno Agregar(IReadOnlyList<CondicaoTempo> horas)
    {
        ArgumentNullException.ThrowIfNull(horas);

        if (horas.Count == 0)
        {
            throw new ArgumentException("O turno precisa de pelo menos uma hora.", nameof(horas));
        }

        double maiorChuva = horas[0].ChuvaMmHora;
        CondicaoTempo maisQuente = horas[0];
        double maiorIndice = IndiceCalor.CalcularC(maisQuente.TemperaturaC, maisQuente.UmidadePct);

        for (int i = 1; i < horas.Count; i++)
        {
            CondicaoTempo hora = horas[i];
            maiorChuva = Math.Max(maiorChuva, hora.ChuvaMmHora);
            double indice = IndiceCalor.CalcularC(hora.TemperaturaC, hora.UmidadePct);

            // Maior estrito: no empate, fica a primeira hora.
            if (indice > maiorIndice)
            {
                maiorIndice = indice;
                maisQuente = hora;
            }
        }

        return new PrevisaoTurno(Chuva(maiorChuva), Temperatura(maisQuente.TemperaturaC), Umidade(maisQuente.UmidadePct));
    }

    /// <summary>
    /// Chuva com 1 casa (0,5 para cima), de 0 a 200 mm/h.
    /// </summary>
    /// <param name="valor">Chuva, em mm/h.</param>
    /// <returns>O valor arredondado e limitado.</returns>
    public static double Chuva(double valor)
    {
        return Math.Clamp(Math.Round(valor, 1, MidpointRounding.AwayFromZero), 0, ChuvaMaxima);
    }

    /// <summary>
    /// Temperatura com 1 casa, de -10 a 50 °C.
    /// </summary>
    /// <param name="valor">Temperatura, em °C.</param>
    /// <returns>O valor arredondado e limitado.</returns>
    public static double Temperatura(double valor)
    {
        return Math.Clamp(Math.Round(valor, 1, MidpointRounding.AwayFromZero), TemperaturaMinima, TemperaturaMaxima);
    }

    /// <summary>
    /// Umidade inteira, de 0 a 100%.
    /// </summary>
    /// <param name="valor">Umidade, em %.</param>
    /// <returns>O valor arredondado e limitado.</returns>
    public static int Umidade(double valor)
    {
        return (int)Math.Clamp(Math.Round(valor, 0, MidpointRounding.AwayFromZero), 0, 100);
    }
}
