using System;
using System.ComponentModel.DataAnnotations;

namespace HelmCare.Api.Configuracao;

/// <summary>
/// Constantes compartilhadas da seção 5 do contrato que a API usa, ligadas à seção
/// <c>HelmCare</c> do <c>appsettings.json</c>. Não são segredo, então ficam no arquivo e podem
/// ser trocadas por variável de ambiente (ex.: <c>HelmCare__RaioCamada1Km=3</c>).
/// Os valores iniciais são os do contrato; a validação roda na subida da API (ValidateOnStart),
/// para um valor absurdo derrubar a API na hora, e não no meio de um SOS.
/// </summary>
public sealed class OpcoesHelmCare
{
    /// <summary>
    /// Nome da seção no <c>appsettings.json</c>.
    /// </summary>
    public const string Secao = "HelmCare";

    /// <summary>
    /// <c>JANELA_OBSERVACAO_S</c>: segundos de observação depois do impacto, antes da contagem.
    /// Usado com a contagem para estimar o <c>ocorridoEm</c> de um SOS sem impacto em aberto.
    /// </summary>
    [Range(1, 60)]
    public int JanelaObservacaoS { get; set; } = 10;

    /// <summary>
    /// <c>CONTAGEM_CANCELAMENTO_S</c>: segundos que o entregador tem para cancelar o alerta.
    /// </summary>
    [Range(5, 120)]
    public int ContagemCancelamentoS { get; set; } = 15;

    /// <summary>
    /// <c>RAIO_CAMADA_1_KM</c>: raio, em km, dos voluntários chamados na camada 1 (inclusivo).
    /// </summary>
    [Range(0.1, 50.0)]
    public double RaioCamada1Km { get; set; } = 2;

    /// <summary>
    /// <c>TEMPO_ACEITE_CAMADA_1_S</c>: segundos sem aceite até a ocorrência escalar para a camada 2.
    /// </summary>
    [Range(10, 600)]
    public int TempoAceiteCamada1S { get; set; } = 60;

    /// <summary>
    /// <c>TTL_ACESSO_COFRE_MIN</c>: minutos de validade de um acesso concedido ao Cofre.
    /// </summary>
    [Range(5, 1440)]
    public int TtlAcessoCofreMin { get; set; } = 60;

    /// <summary>
    /// <c>META_TEMPO_ACEITE_S</c>: meta, em segundos, do tempo entre a queda e o aceite.
    /// </summary>
    [Range(30, 3600)]
    public int MetaTempoAceiteS { get; set; } = 120;

    /// <summary>
    /// <c>ALERTA_CALOR_C</c>: índice de calor, em °C, a partir do qual (inclusive) vale o aviso.
    /// </summary>
    [Range(20.0, 60.0)]
    public double AlertaCalorC { get; set; } = 32;

    /// <summary>
    /// <c>INTERVALO_TELEMETRIA_S</c>: segundos entre duas telemetrias do capacete (e entre os reenvios do SOS).
    /// </summary>
    [Range(1, 300)]
    public int IntervaloTelemetriaS { get; set; } = 10;

    /// <summary>
    /// <c>JANELA_OBSERVACAO_S + CONTAGEM_CANCELAMENTO_S</c>: trecho do capacete no indicador de impacto
    /// quando ele não pode ser medido (impacto estimado ou <c>CONTAGEM_INICIADA</c> perdido, seção 7).
    /// </summary>
    public TimeSpan TrechoCapacetePadrao => TimeSpan.FromSeconds(JanelaObservacaoS + ContagemCancelamentoS);
}
