using System;
using HelmCare.Dominio.Comum;

namespace HelmCare.Dominio.ObjetosDeValor;

/// <summary>
/// Posição geográfica em graus decimais, validada na criação.
/// As entidades guardam <c>Latitude</c> e <c>Longitude</c> em propriedades separadas,
/// com os nomes do contrato; esta estrutura é usada nos parâmetros dos métodos para
/// garantir que só coordenadas válidas entram no domínio.
/// </summary>
public readonly record struct Coordenada
{
    /// <summary>
    /// Raio médio da Terra em quilômetros (valor da IUGG), usado no cálculo de distância.
    /// </summary>
    public const double RaioTerraKm = 6371.0088;

    /// <summary>
    /// Cria uma coordenada validando os limites geográficos.
    /// </summary>
    /// <param name="latitude">Latitude em graus, de -90 a 90.</param>
    /// <param name="longitude">Longitude em graus, de -180 a 180.</param>
    /// <exception cref="ValidacaoDominioException">Quando algum valor está fora do limite ou não é número.</exception>
    public Coordenada(double latitude, double longitude)
    {
        // A comparação negada também barra NaN, que dá falso em qualquer comparação.
        if (!(latitude >= -90 && latitude <= 90))
        {
            throw new ValidacaoDominioException("O campo latitude deve estar entre -90 e 90.");
        }

        if (!(longitude >= -180 && longitude <= 180))
        {
            throw new ValidacaoDominioException("O campo longitude deve estar entre -180 e 180.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Latitude em graus decimais (negativa no hemisfério sul).
    /// </summary>
    public double Latitude { get; }

    /// <summary>
    /// Longitude em graus decimais (negativa a oeste de Greenwich).
    /// </summary>
    public double Longitude { get; }

    /// <summary>
    /// Distância sobre a superfície da Terra até outra coordenada.
    /// Usada para escolher os voluntários da camada 1 (até <c>RAIO_CAMADA_1_KM</c>).
    /// </summary>
    /// <param name="outra">Coordenada de destino.</param>
    /// <returns>A distância em quilômetros.</returns>
    public double DistanciaKmAte(Coordenada outra)
    {
        // Fórmula de Haversine, que trata a Terra como esfera (erro abaixo de 0,5%):
        //   a = sen²(Δφ/2) + cos φ1 · cos φ2 · sen²(Δλ/2)
        //   d = 2 · R · asen(√a)
        // φ é a latitude e λ a longitude, ambas em radianos.
        double latitude1 = GrausParaRadianos(Latitude);
        double latitude2 = GrausParaRadianos(outra.Latitude);
        double deltaLatitude = latitude2 - latitude1;
        double deltaLongitude = GrausParaRadianos(outra.Longitude - Longitude);

        double senoMeiaLatitude = Math.Sin(deltaLatitude / 2);
        double senoMeiaLongitude = Math.Sin(deltaLongitude / 2);
        double a = (senoMeiaLatitude * senoMeiaLatitude)
            + (Math.Cos(latitude1) * Math.Cos(latitude2) * senoMeiaLongitude * senoMeiaLongitude);

        // Math.Min protege o Asin de um "a" um pouco acima de 1 por erro de arredondamento.
        return 2 * RaioTerraKm * Math.Asin(Math.Sqrt(Math.Min(1, a)));
    }

    /// <summary>
    /// Coordenada arredondada para 2 casas decimais (cerca de 1 km de precisão).
    /// É a localização mostrada aos voluntários antes do aceite; a exata só vai para o
    /// contato de emergência e para o voluntário que aceitou (seção 9 do contrato).
    /// </summary>
    /// <returns>A coordenada aproximada.</returns>
    public Coordenada Aproximada()
    {
        return new Coordenada(
            Math.Round(Latitude, 2, MidpointRounding.AwayFromZero),
            Math.Round(Longitude, 2, MidpointRounding.AwayFromZero));
    }

    private static double GrausParaRadianos(double graus)
    {
        return graus * Math.PI / 180;
    }
}
