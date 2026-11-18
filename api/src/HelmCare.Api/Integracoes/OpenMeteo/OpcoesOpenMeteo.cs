using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HelmCare.Api.Integracoes.OpenMeteo;

/// <summary>
/// Configuração da Open-Meteo (seção 1 do contrato, 1.17), ligada à seção <c>OpenMeteo</c> da
/// configuração. São configurações internas da API, fora da seção 5, e nenhuma é segredo: a Open-Meteo
/// não usa chave. Validadas na subida (ValidateOnStart).
/// </summary>
public sealed class OpcoesOpenMeteo : IValidatableObject
{
    /// <summary>
    /// Nome da seção na configuração.
    /// </summary>
    public const string Secao = "OpenMeteo";

    /// <summary>
    /// URL base da Open-Meteo. A rota <c>v1/forecast</c> é relativa a ela, então um caminho, se houver,
    /// termina em barra. Na Infra, a VM precisa do HTTPS de saída para <c>api.open-meteo.com</c>.
    /// </summary>
    [Required]
    public Uri UrlBase { get; set; } = new("https://api.open-meteo.com/");

    /// <summary>
    /// Tempo limite de cada chamada, em segundos, sem novas tentativas.
    /// </summary>
    [Range(1, 30)]
    public int TempoLimiteS { get; set; } = 3;

    /// <summary>
    /// Latitude fixa da cidade (padrão: as coordenadas simuladas da seção 5). Nunca vai para o log.
    /// </summary>
    [Range(-90.0, 90.0)]
    public double Latitude { get; set; } = -23.5641;

    /// <summary>
    /// Longitude fixa da cidade. Nunca vai para o log.
    /// </summary>
    [Range(-180.0, 180.0)]
    public double Longitude { get; set; } = -46.6525;

    /// <summary>
    /// Minutos que a última resposta boa do <c>GET /clima</c> fica no cache da memória.
    /// </summary>
    [Range(1, 60)]
    public int CacheClimaMin { get; set; } = 10;

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Sem a barra final num caminho, o HttpClient trocaria o último segmento pela rota.
        if (UrlBase is not null && (!UrlBase.IsAbsoluteUri || !UrlBase.AbsolutePath.EndsWith('/')))
        {
            yield return new ValidationResult(
                "A UrlBase da Open-Meteo deve ser uma URL absoluta (com barra no fim do caminho, se houver).",
                [nameof(UrlBase)]);
        }
    }
}
