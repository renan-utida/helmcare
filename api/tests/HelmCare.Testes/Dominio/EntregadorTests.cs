using System;
using System.Linq;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes do cadastro do <see cref="Entregador"/>, da ficha médica e dos contatos. Dados simulados.
/// </summary>
[TestClass]
public sealed class EntregadorTests
{
    private static readonly DateTime _agora = new(2026, 11, 12, 20, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Garante que CPF, CNH e telefone são guardados só com dígitos e o nome sem espaços nas pontas.
    /// </summary>
    [TestMethod]
    public void Construtor_DocumentosComMascara_GuardaSoDigitos()
    {
        // Arrange e Act
        var entregador = new Entregador(" João Teste ", "123.456.789-12", "0123 4567 890", "(11) 98765-4321", _agora);

        // Assert
        Assert.AreEqual("João Teste", entregador.Nome);
        Assert.AreEqual("12345678912", entregador.Cpf);
        Assert.AreEqual("01234567890", entregador.Cnh);
        Assert.AreEqual("11987654321", entregador.Telefone);
        Assert.AreEqual(_agora, entregador.CriadoEm);
        Assert.IsNull(entregador.FichaMedica);
        Assert.IsEmpty(entregador.Contatos);
    }

    /// <summary>
    /// Garante que nome vazio é rejeitado.
    /// </summary>
    [TestMethod]
    public void Construtor_NomeVazio_LancaValidacao()
    {
        // Arrange, Act e Assert
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => new Entregador("  ", "12345678912", "01234567890", "11987654321", _agora));

        Assert.Contains("nome", excecao.Message);
    }

    /// <summary>
    /// Garante que CPF com 10 dígitos é rejeitado.
    /// </summary>
    [TestMethod]
    public void Construtor_CpfComDezDigitos_LancaValidacao()
    {
        // Arrange, Act e Assert
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => new Entregador("João Teste", "123.456.789-1", "01234567890", "11987654321", _agora));

        Assert.Contains("cpf", excecao.Message);
    }

    /// <summary>
    /// Garante que a segunda chamada atualiza a mesma ficha em vez de criar outra.
    /// </summary>
    [TestMethod]
    public void DefinirFichaMedica_DuasVezes_AtualizaAMesmaInstancia()
    {
        // Arrange
        Entregador entregador = CriarEntregador();
        FichaMedica primeira = entregador.DefinirFichaMedica("O+", "Dipirona", null, null);

        // Act
        FichaMedica segunda = entregador.DefinirFichaMedica("AB-", null, "Diabetes tipo 1", "Insulina");

        // Assert
        Assert.AreSame(primeira, segunda);
        Assert.AreSame(segunda, entregador.FichaMedica);
        Assert.AreEqual(entregador.Id, segunda.EntregadorId);
        Assert.AreEqual("AB-", segunda.TipoSanguineo);
        Assert.AreEqual(string.Empty, segunda.Alergias);
        Assert.AreEqual("Diabetes tipo 1", segunda.CondicoesCriticas);
        Assert.AreEqual("Insulina", segunda.Medicamentos);
    }

    /// <summary>
    /// Garante que tipos sanguíneos fora da lista são rejeitados e não alteram a ficha existente.
    /// </summary>
    /// <param name="tipo">Tipo sanguíneo inválido.</param>
    [TestMethod]
    [DataRow("C+")]
    [DataRow("O")]
    [DataRow("o+")]
    [DataRow("")]
    public void DefinirFichaMedica_TipoSanguineoInvalido_LancaValidacaoSemAlterar(string tipo)
    {
        // Arrange
        Entregador entregador = CriarEntregador();
        FichaMedica ficha = entregador.DefinirFichaMedica("A+", null, null, null);

        // Act
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => entregador.DefinirFichaMedica(tipo, "Látex", null, null));

        // Assert
        Assert.Contains("tipoSanguineo", excecao.Message);
        Assert.AreEqual("A+", ficha.TipoSanguineo);
        Assert.AreEqual(string.Empty, ficha.Alergias);
    }

    /// <summary>
    /// Garante que o contato é criado com os dados normalizados e entra na coleção do entregador.
    /// </summary>
    [TestMethod]
    public void AdicionarContato_DadosValidos_AdicionaNaColecao()
    {
        // Arrange
        Entregador entregador = CriarEntregador();

        // Act
        ContatoEmergencia contato = entregador.AdicionarContato(" Ana Teste ", "(11) 3456-7890", "Mãe");

        // Assert
        Assert.HasCount(1, entregador.Contatos);
        Assert.AreSame(contato, entregador.Contatos.Single());
        Assert.AreEqual(entregador.Id, contato.EntregadorId);
        Assert.AreEqual("Ana Teste", contato.Nome);
        Assert.AreEqual("1134567890", contato.Telefone);
        Assert.AreEqual("Mãe", contato.Parentesco);
    }

    private static Entregador CriarEntregador()
    {
        return new Entregador("João Teste", "12345678912", "01234567890", "11987654321", _agora);
    }
}
