using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Integracoes.ServicoRisco.Dtos;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Risco;

namespace HelmCare.Api.Integracoes.ServicoRisco;

/// <summary>
/// Cliente HTTP tipado do Serviço de Risco (seção 8 do contrato): <c>POST indice-turno</c> e
/// <c>GET trechos-criticos</c>. A URL base e o tempo limite vêm das <see cref="OpcoesServicoRisco"/>,
/// configuradas no <c>AddHttpClient</c> do <c>Program.cs</c>.
/// </summary>
/// <remarks>
/// <para>
/// O cliente só traduz o HTTP: devolve o resultado ou lança <see cref="ServicoRiscoIndisponivelException"/>
/// com o motivo e o status. Quem decide usar a versão simulada é o <see cref="ServicoRiscoComReserva"/>.
/// Não há novas tentativas: o turno não pode esperar, e a simulada dá o mesmo índice.
/// </para>
/// <para>
/// O cancelamento de quem chamou (o cliente HTTP desistiu da requisição) não é falha do serviço:
/// o <see cref="OperationCanceledException"/> sobe como veio.
/// </para>
/// </remarks>
public sealed class ClienteServicoRisco
{
    // Caminhos relativos, sem barra inicial, para não perder o /risco/v1 da URL base.
    private const string RotaIndiceTurno = "indice-turno";
    private const string RotaTrechosCriticos = "trechos-criticos";

    // Até quantos nomes de campos de um 4xx vão para o log, e com quantos caracteres cada um.
    private const int MaximoCamposNoLog = 10;
    private const int MaximoCaracteresPorCampo = 64;

    // camelCase como o contrato. Números só como número: "60" entre aspas é resposta fora do formato.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
    };

    private readonly HttpClient _http;

    /// <summary>
    /// Cria o cliente com o <see cref="HttpClient"/> já configurado pela fábrica.
    /// </summary>
    /// <param name="http">HttpClient com <c>BaseAddress</c> e <c>Timeout</c> das opções.</param>
    public ClienteServicoRisco(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Calcula o Índice de Risco do Turno no serviço.
    /// </summary>
    /// <param name="inicio">Início do turno, em UTC.</param>
    /// <param name="fim">Fim previsto do turno, em UTC.</param>
    /// <param name="previsao">Previsão do tempo do turno inteiro.</param>
    /// <param name="cancellationToken">Cancelamento de quem chamou.</param>
    /// <returns>O índice, a faixa, os componentes e a explicação.</returns>
    /// <exception cref="ServicoRiscoIndisponivelException">O serviço falhou, não respondeu ou respondeu fora do formato.</exception>
    /// <exception cref="OperationCanceledException">Quem chamou cancelou.</exception>
    public async Task<ResultadoRisco> CalcularIndiceAsync(
        DateTime inicio,
        DateTime fim,
        PrevisaoTurno previsao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previsao);

        var corpo = new IndiceTurnoServicoRiscoRequisicao
        {
            Inicio = Iso(inicio),
            Fim = Iso(fim),
            Previsao = new PrevisaoServicoRiscoRequisicao
            {
                ChuvaMmHora = previsao.ChuvaMmHora,
                TemperaturaC = previsao.TemperaturaC,
                UmidadePct = previsao.UmidadePct,
            },
        };

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, RotaIndiceTurno)
        {
            Content = JsonContent.Create(corpo, options: Json),
        };

        IndiceTurnoServicoRiscoResposta resposta =
            await EnviarAsync<IndiceTurnoServicoRiscoResposta>(requisicao, cancellationToken);

        if (resposta.Componentes is not { } componentes
            || string.IsNullOrWhiteSpace(resposta.Explicacao)
            || !Enum.IsDefined(resposta.FaixaRisco)
            || !DeZeroACem(resposta.IndiceRisco)
            || !DeZeroACem(componentes.Sinistro)
            || !DeZeroACem(componentes.Chuva)
            || !DeZeroACem(componentes.Calor))
        {
            throw RespostaInvalida("O índice veio sem componentes ou explicação, ou com valor fora de 0 a 100.");
        }

        return new ResultadoRisco(
            resposta.IndiceRisco,
            resposta.FaixaRisco,
            componentes.Sinistro,
            componentes.Chuva,
            componentes.Calor,
            resposta.Explicacao);
    }

    /// <summary>
    /// Lista os trechos com mais sinistros graves, na ordem recebida.
    /// </summary>
    /// <param name="limite">Quantidade de trechos (o serviço aceita de 1 a 50).</param>
    /// <param name="cancellationToken">Cancelamento de quem chamou.</param>
    /// <returns>O período e os trechos.</returns>
    /// <exception cref="ServicoRiscoIndisponivelException">O serviço falhou, não respondeu ou respondeu fora do formato.</exception>
    /// <exception cref="OperationCanceledException">Quem chamou cancelou.</exception>
    public async Task<ListaTrechosCriticos> ListarTrechosCriticosAsync(int limite, CancellationToken cancellationToken)
    {
        string rota = $"{RotaTrechosCriticos}?limite={limite.ToString(CultureInfo.InvariantCulture)}";
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);

        TrechosServicoRiscoResposta resposta = await EnviarAsync<TrechosServicoRiscoResposta>(requisicao, cancellationToken);

        if (string.IsNullOrWhiteSpace(resposta.Periodo) || resposta.Trechos is null)
        {
            throw RespostaInvalida("A lista de trechos veio sem período ou sem trechos.");
        }

        var trechos = new List<TrechoCritico>(resposta.Trechos.Count);

        foreach (TrechoServicoRiscoResposta? trecho in resposta.Trechos)
        {
            if (trecho is not { Logradouro: { } logradouro } || string.IsNullOrWhiteSpace(logradouro))
            {
                throw RespostaInvalida("A lista de trechos veio com um trecho incompleto.");
            }

            trechos.Add(new TrechoCritico(
                trecho.Posicao,
                logradouro,
                trecho.Latitude,
                trecho.Longitude,
                trecho.QuantidadeSinistrosGraves));
        }

        return new ListaTrechosCriticos(resposta.Periodo, trechos);
    }

    // Envia, traduz o status e lê o JSON. Toda falha vira ServicoRiscoIndisponivelException com o
    // motivo, menos o cancelamento de quem chamou.
    private async Task<T> EnviarAsync<T>(HttpRequestMessage requisicao, CancellationToken cancellationToken)
        where T : class
    {
        HttpResponseMessage resposta;

        try
        {
            resposta = await _http.SendAsync(requisicao, cancellationToken);
        }
        catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            // O HttpClient sinaliza o tempo esgotado com TaskCanceledException (com uma TimeoutException
            // interna). Se o token de quem chamou foi cancelado, o filtro deixa a exceção subir.
            throw new ServicoRiscoIndisponivelException(
                MotivoFalhaServicoRisco.TempoEsgotado, null, "O Serviço de Risco não respondeu no tempo limite.", e);
        }
        catch (HttpRequestException e)
        {
            throw new ServicoRiscoIndisponivelException(
                MotivoFalhaServicoRisco.ErroDeRede, null, "Não foi possível conectar ao Serviço de Risco.", e);
        }

        using (resposta)
        {
            int status = (int)resposta.StatusCode;

            if (status >= 500)
            {
                throw new ServicoRiscoIndisponivelException(
                    MotivoFalhaServicoRisco.ServicoIndisponivel, status, "O Serviço de Risco respondeu com erro.");
            }

            if (status >= 400)
            {
                IReadOnlyList<string> campos = await LerCamposComErroAsync(resposta, cancellationToken);
                throw new ServicoRiscoIndisponivelException(
                    MotivoFalhaServicoRisco.ErroDeIntegracao,
                    status,
                    "O Serviço de Risco recusou a requisição da API.",
                    camposComErro: campos);
            }

            if (!resposta.IsSuccessStatusCode)
            {
                throw RespostaInvalida("Status inesperado do Serviço de Risco.", status);
            }

            try
            {
                await using Stream corpo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonSerializer.DeserializeAsync<T>(corpo, Json, cancellationToken)
                    ?? throw RespostaInvalida("O Serviço de Risco respondeu null.", status);
            }
            catch (JsonException e)
            {
                throw new ServicoRiscoIndisponivelException(
                    MotivoFalhaServicoRisco.RespostaInvalida, status, "O Serviço de Risco respondeu fora do formato.", e);
            }
        }
    }

    // Só os nomes das chaves de "errors" (ProblemDetails de validação), nunca os valores, que podem
    // repetir o que a API mandou. Corpo que não é JSON dá a lista vazia.
    private static async Task<IReadOnlyList<string>> LerCamposComErroAsync(
        HttpResponseMessage resposta,
        CancellationToken cancellationToken)
    {
        try
        {
            await using Stream corpo = await resposta.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument documento = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);

            if (documento.RootElement.ValueKind != JsonValueKind.Object
                || !documento.RootElement.TryGetProperty("errors", out JsonElement erros)
                || erros.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return erros.EnumerateObject()
                .Select(p => p.Name.Length > MaximoCaracteresPorCampo ? p.Name[..MaximoCaracteresPorCampo] : p.Name)
                .Take(MaximoCamposNoLog)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // ISO 8601 UTC com Z e no máximo 6 casas de fração (microssegundos), que é a precisão do
    // datetime do Python; com fração zero, sai sem ela (2026-11-12T20:00:00Z). O turno já chega em
    // UTC; um instante Local é convertido, e um sem fuso vale como UTC, como no TurnoServico.
    private static string Iso(DateTime instante)
    {
        DateTime utc = instante.Kind == DateTimeKind.Local ? instante.ToUniversalTime() : instante;
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFF'Z'", CultureInfo.InvariantCulture);
    }

    private static bool DeZeroACem(int valor)
    {
        return valor is >= 0 and <= 100;
    }

    private static ServicoRiscoIndisponivelException RespostaInvalida(string mensagem, int? status = 200)
    {
        return new ServicoRiscoIndisponivelException(MotivoFalhaServicoRisco.RespostaInvalida, status, mensagem);
    }
}
