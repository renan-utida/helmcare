namespace HelmCare.Api.Mensageria;

/// <summary>
/// O que o <see cref="RoteadorMensagemMqtt"/> fez com uma mensagem. Serve ao log e aos testes;
/// em nenhum caso a mensagem derruba o assinante.
/// </summary>
public enum ResultadoRoteamento
{
    /// <summary>
    /// Aplicada e gravada.
    /// </summary>
    Processada,

    /// <summary>
    /// Telemetria válida, mas mais antiga que os dados atuais do capacete: nada mudou.
    /// </summary>
    Ignorada,

    /// <summary>
    /// Evento repetido (mesmo dispositivo, tipo e <c>ts</c>), por exemplo o reenvio do SOS.
    /// </summary>
    Duplicada,

    /// <summary>
    /// Tópico ou payload fora do contrato, capacete desconhecido ou evento recusado pelo domínio.
    /// </summary>
    Descartada,

    /// <summary>
    /// Erro inesperado (por exemplo, banco fora do ar). Nada foi gravado; um SOS reenviado tenta de novo.
    /// </summary>
    Falhou,
}
