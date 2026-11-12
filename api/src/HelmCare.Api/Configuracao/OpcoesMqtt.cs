using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace HelmCare.Api.Configuracao;

/// <summary>
/// Conexão da API com o broker MQTT (seção 6 do contrato), ligada à seção <c>Mqtt</c> do
/// <c>appsettings.json</c> e trocável por variável de ambiente (ex.: <c>Mqtt__Habilitado=false</c>).
/// Não há segredo aqui: o broker público do protótipo não usa usuário nem senha.
/// </summary>
/// <remarks>
/// A API conecta com sessão limpa (clean session). O capacete publica telemetria e eventos em
/// QoS 0, então o broker não guardaria esses eventos para a API enquanto ela estivesse fora;
/// uma sessão persistente não ganharia nada. O SOS, que é o que não pode se perder, é reenviado
/// pelo capacete com o mesmo <c>ts</c> a cada <c>INTERVALO_TELEMETRIA_S</c> (seção 6.2).
/// </remarks>
public sealed partial class OpcoesMqtt : IValidatableObject
{
    /// <summary>
    /// Nome da seção no <c>appsettings.json</c>.
    /// </summary>
    public const string Secao = "Mqtt";

    /// <summary>
    /// Liga o assinante. Com <see langword="false"/>, a API sobe sem conectar no broker
    /// (útil para rodar sem internet); o publicador responde 503.
    /// </summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>
    /// Endereço do broker.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string Host { get; set; } = "broker.hivemq.com";

    /// <summary>
    /// Porta TCP do broker (1883, sem TLS, como o capacete).
    /// </summary>
    [Range(1, 65535)]
    public int Porta { get; set; } = 1883;

    /// <summary>
    /// <c>clientId</c> da API no broker. Dois clientes com o mesmo id se derrubam, então cada
    /// instância precisa de um id único, e nunca o do capacete (<c>helmcare-rdu-HC-0001</c>).
    /// Na VM ele vem da variável de ambiente <c>Mqtt__ClientId</c>.
    /// </summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string ClientId { get; set; } = "helmcare-rdu-api-dev";

    /// <summary>
    /// Raiz dos tópicos (seção 6), sem barra no fim e sem curingas.
    /// </summary>
    [Required]
    [RegularExpression("^[^#+]+[^/#+]$", ErrorMessage = "A RaizTopicos não pode ter curingas (+ ou #) nem terminar em barra.")]
    public string RaizTopicos { get; set; } = "helmcare-rdu/v1";

    /// <summary>
    /// Segundos entre duas verificações da conexão (e tentativas de reconectar).
    /// </summary>
    [Range(1, 300)]
    public int IntervaloReconexaoS { get; set; } = 5;

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // O id do capacete é "helmcare-rdu-HC-0000"; usar o mesmo derrubaria o capacete do broker.
        if (ClientId is not null && IdDeCapacete().IsMatch(ClientId))
        {
            yield return new ValidationResult(
                "O ClientId da API não pode ser o de um capacete (helmcare-rdu-HC-0000).",
                [nameof(ClientId)]);
        }
    }

    [GeneratedRegex("^helmcare-rdu-HC-[0-9]{4}$")]
    private static partial Regex IdDeCapacete();
}
