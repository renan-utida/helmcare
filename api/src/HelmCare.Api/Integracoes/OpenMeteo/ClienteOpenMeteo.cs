using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Integracoes.OpenMeteo.Dtos;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Integracoes.OpenMeteo;

/// <summary>
/// Cliente HTTP tipado da Open-Meteo (<c>GET v1/forecast</c>), no mesmo padrão do cliente do Serviço de
/// Risco: URL base e tempo limite da configuração, nenhuma nova tentativa, e toda falha vira uma
/// <see cref="OpenMeteoIndisponivelException"/> com o motivo, menos o cancelamento de quem chamou.
/// </summary>
/// <remarks>
/// Os horários vêm no fuso padrão da Open-Meteo (GMT, que já é UTC), então o pedido não manda
/// <c>timezone</c>. A coordenada é a fixa da cidade e nunca vai para o log nem para as mensagens.
/// </remarks>
public sealed class ClienteOpenMeteo
{
    private const string Rota = "v1/forecast";
    private const string Variaveis = "temperature_2m,relative_humidity_2m,precipitation";
    private const string FormatoHorario = "yyyy-MM-dd'T'HH:mm";

    // Números só como número: "29.0" entre aspas é resposta fora do formato.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
    };

    private readonly HttpClient _http;
    private readonly string _coordenada;

    /// <summary>
    /// Cria o cliente.
    /// </summary>
    /// <param name="http">HttpClient da fábrica, com a URL base e o tempo limite.</param>
    /// <param name="opcoes">Configuração da Open-Meteo (coordenada da cidade).</param>
    public ClienteOpenMeteo(HttpClient http, IOptions<OpcoesOpenMeteo> opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _http = http;
        _coordenada = string.Create(
            CultureInfo.InvariantCulture,
            $"latitude={opcoes.Value.Latitude}&longitude={opcoes.Value.Longitude}");
    }

    /// <summary>
    /// Previsão horária dos dias informados (UTC), conferida: uma condição por hora.
    /// </summary>
    /// <param name="primeiroDia">Primeiro dia (UTC) da primeira hora do turno.</param>
    /// <param name="ultimoDia">Último dia (UTC) da última hora do turno.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>As horas da resposta, na ordem.</returns>
    /// <exception cref="OpenMeteoIndisponivelException">Falha, tempo esgotado ou resposta inválida.</exception>
    public async Task<IReadOnlyList<CondicaoTempo>> ObterHorariaAsync(
        DateOnly primeiroDia,
        DateOnly ultimoDia,
        CancellationToken cancellationToken)
    {
        string url = string.Create(
            CultureInfo.InvariantCulture,
            $"{Rota}?{_coordenada}&hourly={Variaveis}&start_date={primeiroDia:yyyy-MM-dd}&end_date={ultimoDia:yyyy-MM-dd}");

        RespostaOpenMeteo resposta = await EnviarAsync(url, cancellationToken);
        DadosHorariosOpenMeteo horas = resposta.Hourly ?? throw Invalida("Sem o bloco hourly.");

        if (horas.Time is null || horas.Temperature2m is null || horas.RelativeHumidity2m is null || horas.Precipitation is null)
        {
            throw Invalida("Lista horária ausente.");
        }

        int total = horas.Time.Count;

        if (horas.Temperature2m.Count != total || horas.RelativeHumidity2m.Count != total || horas.Precipitation.Count != total)
        {
            throw Invalida("Listas horárias de tamanhos diferentes.");
        }

        var condicoes = new List<CondicaoTempo>(total);

        for (int i = 0; i < total; i++)
        {
            condicoes.Add(new CondicaoTempo(
                Horario(horas.Time[i]),
                horas.Temperature2m[i] ?? throw Invalida("Temperatura nula."),
                horas.RelativeHumidity2m[i] ?? throw Invalida("Umidade nula."),
                horas.Precipitation[i] ?? throw Invalida("Precipitação nula.")));
        }

        return condicoes;
    }

    /// <summary>
    /// Condições atuais da cidade (bloco <c>current</c>), conferidas.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>As condições, com o horário da medição em UTC.</returns>
    /// <exception cref="OpenMeteoIndisponivelException">Falha, tempo esgotado ou resposta inválida.</exception>
    public async Task<CondicaoTempo> ObterAtualAsync(CancellationToken cancellationToken)
    {
        string url = string.Create(CultureInfo.InvariantCulture, $"{Rota}?{_coordenada}&current={Variaveis}");

        RespostaOpenMeteo resposta = await EnviarAsync(url, cancellationToken);
        DadosAtuaisOpenMeteo atual = resposta.Current ?? throw Invalida("Sem o bloco current.");

        return new CondicaoTempo(
            Horario(atual.Time),
            atual.Temperature2m ?? throw Invalida("Temperatura ausente."),
            atual.RelativeHumidity2m ?? throw Invalida("Umidade ausente."),
            atual.Precipitation ?? throw Invalida("Precipitação ausente."));
    }

    private static DateTime Horario(string? texto)
    {
        if (texto is null || !DateTime.TryParseExact(
                texto,
                FormatoHorario,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime horario))
        {
            throw Invalida("Horário fora do formato.");
        }

        return horario;
    }

    private static OpenMeteoIndisponivelException Invalida(string detalhe)
    {
        return new OpenMeteoIndisponivelException(MotivoFalhaOpenMeteo.RespostaInvalida, "Resposta inválida da Open-Meteo: " + detalhe);
    }

    private async Task<RespostaOpenMeteo> EnviarAsync(string url, CancellationToken cancellationToken)
    {
        HttpResponseMessage resposta;

        try
        {
            resposta = await _http.GetAsync(new Uri(url, UriKind.Relative), cancellationToken);
        }
        catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            // O tempo limite do HttpClient chega como TaskCanceledException; o cancelamento de quem
            // chamou passa pelo filtro e sobe como está.
            throw new OpenMeteoIndisponivelException(MotivoFalhaOpenMeteo.TempoEsgotado, "A Open-Meteo não respondeu no tempo limite.", e);
        }
        catch (HttpRequestException e)
        {
            throw new OpenMeteoIndisponivelException(MotivoFalhaOpenMeteo.Falha, "Não foi possível conectar à Open-Meteo.", e);
        }

        using (resposta)
        {
            if (!resposta.IsSuccessStatusCode)
            {
                throw new OpenMeteoIndisponivelException(MotivoFalhaOpenMeteo.Falha, "A Open-Meteo respondeu com um status de erro.");
            }

            try
            {
                await using Stream corpo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonSerializer.DeserializeAsync<RespostaOpenMeteo>(corpo, Json, cancellationToken)
                    ?? throw Invalida("Corpo nulo.");
            }
            catch (JsonException e)
            {
                throw new OpenMeteoIndisponivelException(MotivoFalhaOpenMeteo.RespostaInvalida, "A Open-Meteo respondeu fora do formato.", e);
            }
        }
    }
}
