using HelmCare.Api.Contratos.Validacao;
using HelmCare.Dominio.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Contratos;

/// <summary>
/// Testes do <see cref="NomeDoContratoAttribute{TEnum}"/> com o <see cref="TipoVoluntario"/>.
/// </summary>
[TestClass]
public sealed class NomeDoContratoAttributeTests
{
    /// <summary>
    /// Garante que só os nomes exatos do contrato passam (e o nulo, que fica para o Required).
    /// </summary>
    /// <param name="valor">Texto recebido.</param>
    /// <param name="valido">Se deve passar.</param>
    [TestMethod]
    [DataRow("ENTREGADOR", true)]
    [DataRow("VIGIA_MOTO", true)]
    [DataRow(null, true)]
    [DataRow("entregador", false)]
    [DataRow("VigiaMoto", false)]
    [DataRow("", false)]
    [DataRow("MOTOBOY", false)]
    public void IsValid_TipoVoluntario_SoNomesDoContrato(string? valor, bool valido)
    {
        // Arrange
        var atributo = new NomeDoContratoAttribute<TipoVoluntario>();

        // Act
        bool resultado = atributo.IsValid(valor);

        // Assert
        Assert.AreEqual(valido, resultado);
    }
}
