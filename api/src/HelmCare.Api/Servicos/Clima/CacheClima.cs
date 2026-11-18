using System;
using System.Threading;
using HelmCare.Api.Contratos.Clima;

namespace HelmCare.Api.Servicos.Clima;

/// <summary>
/// Última resposta boa do <c>GET /clima</c>, guardada na memória com o instante em que foi obtida. É
/// singleton e usa o instante que o serviço passa (do <see cref="TimeProvider"/>), então os testes
/// vencem o cache avançando o relógio, sem esperar.
/// </summary>
public sealed class CacheClima
{
    private readonly Lock _trava = new();
    private ClimaResposta? _resposta;
    private DateTimeOffset _guardadaEm;

    /// <summary>
    /// Devolve a resposta guardada se ela ainda estiver dentro da validade.
    /// </summary>
    /// <param name="agora">Instante atual.</param>
    /// <param name="validade">Tempo de vida do cache (10 min por padrão).</param>
    /// <param name="resposta">A resposta guardada, se válida.</param>
    /// <param name="idade">Há quanto tempo ela foi guardada.</param>
    /// <returns><see langword="true"/> com uma resposta válida.</returns>
    public bool TentarObter(DateTimeOffset agora, TimeSpan validade, out ClimaResposta? resposta, out TimeSpan idade)
    {
        lock (_trava)
        {
            idade = agora - _guardadaEm;
            resposta = _resposta is not null && idade < validade ? _resposta : null;
            return resposta is not null;
        }
    }

    /// <summary>
    /// Guarda a resposta nova.
    /// </summary>
    /// <param name="resposta">Resposta boa da Open-Meteo.</param>
    /// <param name="agora">Instante em que ela foi obtida.</param>
    public void Guardar(ClimaResposta resposta, DateTimeOffset agora)
    {
        lock (_trava)
        {
            _resposta = resposta;
            _guardadaEm = agora;
        }
    }
}
