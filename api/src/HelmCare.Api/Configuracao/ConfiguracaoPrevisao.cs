using System;
using HelmCare.Api.Integracoes.OpenMeteo;
using HelmCare.Api.Servicos.Clima;
using HelmCare.Api.Servicos.Risco;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Configuracao;

/// <summary>
/// Registro da previsão do tempo (seção 7 do contrato, 1.17): o cliente da Open-Meteo, o clima com o
/// cache e o <see cref="IPrevisaoTempo"/> do <c>POST /turnos</c>, real com reserva ou simulado forçado.
/// </summary>
public static class ConfiguracaoPrevisao
{
    /// <summary>
    /// Registra os serviços da previsão.
    /// </summary>
    /// <param name="servicos">Serviços da aplicação (com o <c>IConfiguration</c> registrado).</param>
    /// <param name="simuladaForcada">
    /// <see cref="OpcoesHelmCare.PrevisaoSimuladaForcada"/>: verdadeiro faz o turno usar a previsão simulada
    /// direto, sem chamar a Open-Meteo. O <c>GET /clima</c> continua com a Open-Meteo de qualquer jeito.
    /// </param>
    public static void Registrar(IServiceCollection servicos, bool simuladaForcada)
    {
        servicos.AddOptions<OpcoesOpenMeteo>()
            .BindConfiguration(OpcoesOpenMeteo.Secao)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Sem nova tentativa: o HttpClient tipado só tem o tempo limite da configuração.
        servicos.AddHttpClient<ClienteOpenMeteo>((sp, http) =>
        {
            OpcoesOpenMeteo opcoes = sp.GetRequiredService<IOptions<OpcoesOpenMeteo>>().Value;
            http.BaseAddress = opcoes.UrlBase;
            http.Timeout = TimeSpan.FromSeconds(opcoes.TempoLimiteS);
        });

        servicos.AddSingleton<CacheClima>();
        servicos.AddScoped<ClimaServico>();
        servicos.AddSingleton<PrevisaoTempoSimulada>();

        if (simuladaForcada)
        {
            servicos.AddSingleton<IPrevisaoTempo>(sp => sp.GetRequiredService<PrevisaoTempoSimulada>());
        }
        else
        {
            servicos.AddScoped<PrevisaoTempoOpenMeteo>();
            servicos.AddScoped<IPrevisaoTempo, PrevisaoTempoComReserva>();
        }
    }
}
