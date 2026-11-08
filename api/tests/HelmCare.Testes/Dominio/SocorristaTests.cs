using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes da verificação e do vínculo com o Identity do <see cref="Socorrista"/>. Dados simulados.
/// </summary>
[TestClass]
public sealed class SocorristaTests
{
    /// <summary>
    /// Garante que o socorrista nasce não verificado e passa a verificado.
    /// </summary>
    [TestMethod]
    public void Verificar_SocorristaNovo_PassaAVerificado()
    {
        // Arrange
        Socorrista socorrista = CriarSocorrista();
        bool antes = socorrista.Verificado;

        // Act
        socorrista.Verificar();

        // Assert
        Assert.IsFalse(antes);
        Assert.IsTrue(socorrista.Verificado);
    }

    /// <summary>
    /// Garante que vincular o mesmo usuário duas vezes não muda nada.
    /// </summary>
    [TestMethod]
    public void VincularUsuario_MesmoUsuarioDuasVezes_MantemVinculo()
    {
        // Arrange
        Socorrista socorrista = CriarSocorrista();
        socorrista.VincularUsuario("usuario-1");

        // Act
        socorrista.VincularUsuario(" usuario-1 ");

        // Assert
        Assert.AreEqual("usuario-1", socorrista.UsuarioId);
    }

    /// <summary>
    /// Garante que trocar o usuário vinculado é regra de negócio violada e mantém o original.
    /// </summary>
    [TestMethod]
    public void VincularUsuario_OutroUsuario_LancaRegraNegocio()
    {
        // Arrange
        Socorrista socorrista = CriarSocorrista();
        socorrista.VincularUsuario("usuario-1");

        // Act
        Assert.ThrowsExactly<RegraNegocioException>(() => socorrista.VincularUsuario("usuario-2"));

        // Assert
        Assert.AreEqual("usuario-1", socorrista.UsuarioId);
    }

    /// <summary>
    /// Garante que o id do usuário vazio ou só com espaços é rejeitado.
    /// </summary>
    /// <param name="usuarioId">Id inválido.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void VincularUsuario_Vazio_LancaValidacao(string usuarioId)
    {
        // Arrange
        Socorrista socorrista = CriarSocorrista();

        // Act
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => socorrista.VincularUsuario(usuarioId));

        // Assert
        Assert.Contains("usuarioId", excecao.Message);
        Assert.IsNull(socorrista.UsuarioId);
    }

    private static Socorrista CriarSocorrista()
    {
        return new Socorrista("Socorrista Teste", "COREN-SP 000000", "SAMU Teste");
    }
}
