using HelmCare.Dominio.Comum;
using HelmCare.Dominio.ObjetosDeValor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes da validação, da distância e da aproximação de <see cref="Coordenada"/>.
/// </summary>
[TestClass]
public sealed class CoordenadaTests
{
    private const double Tolerancia = 1e-9;

    /// <summary>
    /// Garante que os limites geográficos exatos são aceitos.
    /// </summary>
    /// <param name="latitude">Latitude no limite.</param>
    /// <param name="longitude">Longitude no limite.</param>
    [TestMethod]
    [DataRow(-90.0, -180.0)]
    [DataRow(90.0, 180.0)]
    [DataRow(0.0, 0.0)]
    [DataRow(-23.5641, -46.6525)]
    public void Construtor_DentroDosLimites_GuardaOsValores(double latitude, double longitude)
    {
        // Arrange: a entrada vem do DataRow.

        // Act
        var coordenada = new Coordenada(latitude, longitude);

        // Assert
        Assert.AreEqual(latitude, coordenada.Latitude);
        Assert.AreEqual(longitude, coordenada.Longitude);
    }

    /// <summary>
    /// Garante que valores fora dos limites, ou NaN, são rejeitados.
    /// </summary>
    /// <param name="latitude">Latitude testada.</param>
    /// <param name="longitude">Longitude testada.</param>
    [TestMethod]
    [DataRow(-90.0001, 0.0)]
    [DataRow(90.0001, 0.0)]
    [DataRow(0.0, -180.0001)]
    [DataRow(0.0, 180.0001)]
    [DataRow(double.NaN, 0.0)]
    [DataRow(0.0, double.NaN)]
    public void Construtor_ForaDosLimites_LancaValidacao(double latitude, double longitude)
    {
        // Arrange: a entrada vem do DataRow.

        // Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(() => new Coordenada(latitude, longitude));
    }

    /// <summary>
    /// Garante a distância de Haversine entre dois pontos conhecidos de São Paulo.
    /// </summary>
    [TestMethod]
    public void DistanciaKmAte_PontosConhecidos_DevolveDistanciaEsperada()
    {
        // Arrange
        var paulista = new Coordenada(-23.5641, -46.6525);
        var se = new Coordenada(-23.5505, -46.6333);

        // Act
        double distancia = paulista.DistanciaKmAte(se);

        // Assert
        Assert.AreEqual(2.4732, distancia, 0.001);
    }

    /// <summary>
    /// Garante que a distância de um ponto até ele mesmo é zero.
    /// </summary>
    [TestMethod]
    public void DistanciaKmAte_MesmoPonto_DevolveZero()
    {
        // Arrange
        var ponto = new Coordenada(-23.5641, -46.6525);

        // Act
        double distancia = ponto.DistanciaKmAte(ponto);

        // Assert
        Assert.AreEqual(0, distancia, Tolerancia);
    }

    /// <summary>
    /// Garante que a localização aproximada arredonda para 2 casas decimais.
    /// </summary>
    [TestMethod]
    public void Aproximada_CoordenadaExata_ArredondaParaDuasCasas()
    {
        // Arrange
        var exata = new Coordenada(-23.5641, -46.6525);

        // Act
        Coordenada aproximada = exata.Aproximada();

        // Assert
        Assert.AreEqual(-23.56, aproximada.Latitude, Tolerancia);
        Assert.AreEqual(-46.65, aproximada.Longitude, Tolerancia);
    }
}
