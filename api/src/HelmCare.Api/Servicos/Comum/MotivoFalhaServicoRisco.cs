namespace HelmCare.Api.Servicos.Comum;

/// <summary>
/// Por que a chamada ao Serviço de Risco falhou. Os três primeiros são falhas do serviço ou da
/// rede (aviso no log); os dois últimos são erros de integração entre a API e o serviço (erro no
/// log), porque indicam requisição ou resposta fora do contrato.
/// </summary>
public enum MotivoFalhaServicoRisco
{
    /// <summary>
    /// O serviço respondeu 5xx (inclusive o 503 da degradação, seção 8).
    /// </summary>
    ServicoIndisponivel,

    /// <summary>
    /// Não houve resposta: conexão recusada, nome não resolvido ou conexão interrompida.
    /// </summary>
    ErroDeRede,

    /// <summary>
    /// O serviço não respondeu dentro do tempo limite (<c>ServicoRisco:TempoLimiteS</c>).
    /// </summary>
    TempoEsgotado,

    /// <summary>
    /// O serviço respondeu 4xx: a API mandou algo que o serviço recusou.
    /// </summary>
    ErroDeIntegracao,

    /// <summary>
    /// A resposta 200 veio fora do formato da seção 8 (JSON inválido, campo faltando ou valor fora da faixa).
    /// </summary>
    RespostaInvalida,
}
