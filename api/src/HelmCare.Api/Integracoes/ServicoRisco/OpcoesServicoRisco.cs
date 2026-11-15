using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HelmCare.Api.Integracoes.ServicoRisco;

/// <summary>
/// Endereço e tempo limite do Serviço de Risco, ligados à seção <c>ServicoRisco</c> do
/// <c>appsettings.json</c>. Não há segredo aqui. A validação roda na subida (ValidateOnStart).
/// </summary>
/// <remarks>
/// O padrão usa 127.0.0.1, e não localhost, porque o uvicorn sobe em 127.0.0.1 e, no Windows, o
/// localhost tenta primeiro o IPv6 (::1). Com o serviço fora, o Windows repete cada conexão recusada
/// e leva cerca de 2 s para desistir (2,1 s medidos com 127.0.0.1); com localhost seriam duas
/// tentativas (::1 e depois 127.0.0.1), perto do tempo limite. Na VM, a URL vem da variável
/// <c>ServicoRisco__UrlBase</c>.
/// </remarks>
public sealed class OpcoesServicoRisco : IValidatableObject
{
    /// <summary>
    /// Nome da seção no <c>appsettings.json</c>.
    /// </summary>
    public const string Secao = "ServicoRisco";

    /// <summary>
    /// URL base do serviço, absoluta e terminada em barra, para os caminhos relativos
    /// (<c>indice-turno</c>, <c>trechos-criticos</c>) não perderem o <c>/risco/v1</c>.
    /// </summary>
    [Required]
    public Uri UrlBase { get; set; } = new("http://127.0.0.1:8000/risco/v1/");

    /// <summary>
    /// Segundos até desistir de uma chamada. Esgotado o tempo, o turno usa a versão simulada.
    /// </summary>
    [Range(1, 30)]
    public int TempoLimiteS { get; set; } = 3;

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Sem a barra final, o HttpClient trocaria o último segmento: "risco/v1" + "indice-turno"
        // viraria "risco/indice-turno".
        if (UrlBase is not null && (!UrlBase.IsAbsoluteUri || !UrlBase.AbsolutePath.EndsWith('/')))
        {
            yield return new ValidationResult(
                "A UrlBase do Serviço de Risco deve ser uma URL absoluta terminada em barra.",
                [nameof(UrlBase)]);
        }
    }
}
