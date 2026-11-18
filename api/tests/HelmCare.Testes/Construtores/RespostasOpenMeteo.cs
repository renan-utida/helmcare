using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using HelmCare.Api.Integracoes.OpenMeteo;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Options;

namespace HelmCare.Testes.Construtores;

/// <summary>
/// Corpos JSON da Open-Meteo para os testes, no formato real (<c>hourly</c> e <c>current</c>), e o
/// cliente montado sobre o <see cref="FakeHttpMessageHandler"/>.
/// </summary>
public static class RespostasOpenMeteo
{
    /// <summary>
    /// URL base usada nos testes.
    /// </summary>
    public static readonly Uri UrlBase = new("https://api.open-meteo.teste/");

    /// <summary>
    /// Corpo horário com uma linha por hora.
    /// </summary>
    /// <param name="horas">Início de cada hora (UTC), temperatura, umidade e chuva.</param>
    /// <returns>O JSON.</returns>
    public static string Horaria(IEnumerable<(DateTime Hora, double T, double U, double P)> horas)
    {
        List<(DateTime Hora, double T, double U, double P)> lista = horas.ToList();
        return "{\"latitude\":-23.56,\"longitude\":-46.65,\"hourly\":{"
            + "\"time\":[" + string.Join(",", lista.Select(h => $"\"{h.Hora.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)}\"")) + "],"
            + "\"temperature_2m\":[" + string.Join(",", lista.Select(h => Numero(h.T))) + "],"
            + "\"relative_humidity_2m\":[" + string.Join(",", lista.Select(h => Numero(h.U))) + "],"
            + "\"precipitation\":[" + string.Join(",", lista.Select(h => Numero(h.P))) + "]}}";
    }

    /// <summary>
    /// Corpo horário com o mesmo valor em todas as horas do dia inteiro (UTC) dos dias informados.
    /// </summary>
    /// <param name="primeiroDia">Primeiro dia.</param>
    /// <param name="dias">Quantidade de dias.</param>
    /// <param name="valor">Temperatura, umidade e chuva de cada hora.</param>
    /// <returns>O JSON.</returns>
    public static string DiasInteiros(DateTime primeiroDia, int dias, Func<DateTime, (double T, double U, double P)> valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        IEnumerable<DateTime> horas = Enumerable.Range(0, dias * 24).Select(h => primeiroDia.Date.AddHours(h));
        return Horaria(horas.Select(h =>
        {
            (double t, double u, double p) = valor(DateTime.SpecifyKind(h, DateTimeKind.Utc));
            return (h, t, u, p);
        }));
    }

    /// <summary>
    /// Corpo das condições atuais.
    /// </summary>
    /// <param name="horario">Horário da medição (GMT), no formato da Open-Meteo.</param>
    /// <param name="t">Temperatura.</param>
    /// <param name="u">Umidade.</param>
    /// <param name="p">Precipitação.</param>
    /// <returns>O JSON.</returns>
    public static string Atual(string horario, double t, double u, double p)
    {
        return "{\"latitude\":-23.56,\"longitude\":-46.65,\"current\":{"
            + $"\"time\":\"{horario}\",\"interval\":900,\"temperature_2m\":{Numero(t)},\"relative_humidity_2m\":{Numero(u)},\"precipitation\":{Numero(p)}}}}}";
    }

    /// <summary>
    /// HttpClient sobre o handler falso, com o tempo limite informado. Quem recebe descarta.
    /// </summary>
    /// <param name="handler">Handler falso.</param>
    /// <param name="tempoLimite">Tempo limite do HttpClient (3 s por padrão).</param>
    /// <returns>O HttpClient.</returns>
    public static HttpClient Http(FakeHttpMessageHandler handler, TimeSpan? tempoLimite = null)
    {
        return new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = UrlBase,
            Timeout = tempoLimite ?? TimeSpan.FromSeconds(3),
        };
    }

    /// <summary>
    /// Cliente da Open-Meteo sobre o HttpClient, com a configuração padrão.
    /// </summary>
    /// <param name="http">HttpClient (de <see cref="Http"/>).</param>
    /// <returns>O cliente.</returns>
    public static ClienteOpenMeteo Cliente(HttpClient http)
    {
        return new ClienteOpenMeteo(http, Options.Create(new OpcoesOpenMeteo()));
    }

    private static string Numero(double valor)
    {
        return valor.ToString("0.0##", CultureInfo.InvariantCulture);
    }
}
