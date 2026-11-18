namespace HelmCare.Api.Integracoes.OpenMeteo;

/// <summary>
/// As quatro causas da reserva da previsão (seção 7 do contrato, 1.17). O nome vai para o log, sem
/// a coordenada e sem o corpo da resposta.
/// </summary>
public enum MotivoFalhaOpenMeteo
{
    /// <summary>
    /// Falha de rede, ou status diferente de 2xx.
    /// </summary>
    Falha,

    /// <summary>
    /// Mais que o tempo limite (3 s), sem novas tentativas.
    /// </summary>
    TempoEsgotado,

    /// <summary>
    /// JSON quebrado, campo ausente ou nulo, listas de tamanhos diferentes ou horário fora do formato.
    /// </summary>
    RespostaInvalida,

    /// <summary>
    /// A resposta não cobre todas as horas do turno.
    /// </summary>
    HorasNaoCobertas,
}
