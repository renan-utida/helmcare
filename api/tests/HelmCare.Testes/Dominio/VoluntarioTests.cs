using System;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Dominio.ObjetosDeValor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes da disponibilidade e da posição do <see cref="Voluntario"/>.
/// </summary>
[TestClass]
public sealed class VoluntarioTests
{
    /// <summary>
    /// Garante que o voluntário nasce indisponível e sem posição.
    /// </summary>
    [TestMethod]
    public void Construtor_DadosValidos_ComecaIndisponivelSemPosicao()
    {
        // Arrange e Act
        var voluntario = new Voluntario(Guid.NewGuid(), TipoVoluntario.VigiaMoto);

        // Assert
        Assert.IsFalse(voluntario.Disponivel);
        Assert.IsNull(voluntario.Latitude);
        Assert.IsNull(voluntario.Longitude);
    }

    /// <summary>
    /// Garante que ficar disponível sem posição é rejeitado.
    /// </summary>
    [TestMethod]
    public void AtualizarDisponibilidade_DisponivelSemPosicao_LancaValidacao()
    {
        // Arrange
        var voluntario = new Voluntario(Guid.NewGuid(), TipoVoluntario.Entregador);

        // Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(() => voluntario.AtualizarDisponibilidade(true, null));
        Assert.IsFalse(voluntario.Disponivel);
    }

    /// <summary>
    /// Garante que ficar disponível guarda a posição exata.
    /// </summary>
    [TestMethod]
    public void AtualizarDisponibilidade_DisponivelComPosicao_GuardaPosicao()
    {
        // Arrange
        var voluntario = new Voluntario(Guid.NewGuid(), TipoVoluntario.Entregador);

        // Act
        voluntario.AtualizarDisponibilidade(true, new Coordenada(-23.5505, -46.6333));

        // Assert
        Assert.IsTrue(voluntario.Disponivel);
        Assert.AreEqual(-23.5505, voluntario.Latitude);
        Assert.AreEqual(-46.6333, voluntario.Longitude);
    }

    /// <summary>
    /// Garante que ficar indisponível apaga a posição, mesmo que uma seja enviada.
    /// </summary>
    [TestMethod]
    public void AtualizarDisponibilidade_Indisponivel_ApagaPosicao()
    {
        // Arrange
        var voluntario = new Voluntario(Guid.NewGuid(), TipoVoluntario.Entregador);
        var posicao = new Coordenada(-23.5505, -46.6333);
        voluntario.AtualizarDisponibilidade(true, posicao);

        // Act
        voluntario.AtualizarDisponibilidade(false, posicao);

        // Assert
        Assert.IsFalse(voluntario.Disponivel);
        Assert.IsNull(voluntario.Latitude);
        Assert.IsNull(voluntario.Longitude);
    }
}
