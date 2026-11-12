namespace HelmCare.Api.Mensageria;

/// <summary>
/// Canal de um tópico que a API assina.
/// </summary>
public enum CanalMqtt
{
    /// <summary>
    /// <c>{raiz}/{codigo}/telemetria</c> (seção 6.1).
    /// </summary>
    Telemetria,

    /// <summary>
    /// <c>{raiz}/{codigo}/evento</c> (seção 6.2).
    /// </summary>
    Evento,
}
