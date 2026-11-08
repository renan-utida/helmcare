using System;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes da abertura, do risco e do encerramento do <see cref="Turno"/>.
/// </summary>
[TestClass]
public sealed class TurnoTests
{
    private static readonly DateTime _inicio = new(2026, 11, 12, 20, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Garante que o fim previsto igual ou anterior ao início é rejeitado.
    /// </summary>
    /// <param name="minutosDepois">Diferença do fim previsto para o início.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-30)]
    public void Construtor_FimPrevistoNaoPosteriorAoInicio_LancaValidacao(int minutosDepois)
    {
        // Arrange
        DateTime fimPrevisto = _inicio.AddMinutes(minutosDepois);

        // Act
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => new Turno(Guid.NewGuid(), _inicio, fimPrevisto));

        // Assert
        Assert.Contains("fimPrevisto", excecao.Message);
    }

    /// <summary>
    /// Garante que o turno nasce ativo, com proteção ligada e sem risco.
    /// </summary>
    [TestMethod]
    public void Construtor_DadosValidos_ComecaAtivoComProtecao()
    {
        // Arrange e Act
        Turno turno = CriarTurno();

        // Assert
        Assert.IsTrue(turno.EstaAtivo);
        Assert.IsTrue(turno.ProtecaoAtiva);
        Assert.IsNull(turno.Fim);
        Assert.IsNull(turno.IndiceRisco);
    }

    /// <summary>
    /// Garante que a faixa incoerente com o índice é rejeitada e nada é gravado.
    /// </summary>
    [TestMethod]
    public void AplicarRisco_FaixaIncoerente_LancaValidacaoSemAlterar()
    {
        // Arrange
        Turno turno = CriarTurno();

        // Act
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => turno.AplicarRisco(72, FaixaRisco.Baixo, 80, 70, 50, "Chuva forte no horário de pico."));

        // Assert
        Assert.Contains("faixaRisco", excecao.Message);
        Assert.IsNull(turno.IndiceRisco);
        Assert.IsNull(turno.FaixaRisco);
        Assert.IsNull(turno.Explicacao);
    }

    /// <summary>
    /// Garante que um componente fora de 0 a 100 é rejeitado.
    /// </summary>
    [TestMethod]
    public void AplicarRisco_ComponenteForaDaFaixa_LancaValidacao()
    {
        // Arrange
        Turno turno = CriarTurno();

        // Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(
            () => turno.AplicarRisco(72, FaixaRisco.Alto, 101, 70, 50, "Chuva forte."));
    }

    /// <summary>
    /// Garante que o risco válido preenche o índice, a faixa, os componentes e a explicação.
    /// </summary>
    [TestMethod]
    public void AplicarRisco_DadosValidos_PreencheTudo()
    {
        // Arrange
        Turno turno = CriarTurno();

        // Act
        turno.AplicarRisco(72, FaixaRisco.Alto, 80, 70, 50, "  Chuva forte no horário de pico.  ");

        // Assert
        Assert.AreEqual(72, turno.IndiceRisco);
        Assert.AreEqual(FaixaRisco.Alto, turno.FaixaRisco);
        Assert.AreEqual(80, turno.ComponenteSinistro);
        Assert.AreEqual(70, turno.ComponenteChuva);
        Assert.AreEqual(50, turno.ComponenteCalor);
        Assert.AreEqual("Chuva forte no horário de pico.", turno.Explicacao);
    }

    /// <summary>
    /// Garante que encerrar preenche o fim e desliga a proteção.
    /// </summary>
    [TestMethod]
    public void Encerrar_TurnoAtivo_PreencheFimEDesligaProtecao()
    {
        // Arrange
        Turno turno = CriarTurno();
        DateTime agora = _inicio.AddHours(2);

        // Act
        turno.Encerrar(agora);

        // Assert
        Assert.AreEqual(agora, turno.Fim);
        Assert.IsFalse(turno.ProtecaoAtiva);
        Assert.IsFalse(turno.EstaAtivo);
    }

    /// <summary>
    /// Garante que encerrar um turno já encerrado é transição inválida e não muda o fim.
    /// </summary>
    [TestMethod]
    public void Encerrar_DuasVezes_LancaTransicaoInvalida()
    {
        // Arrange
        Turno turno = CriarTurno();
        DateTime primeiroFim = _inicio.AddHours(2);
        turno.Encerrar(primeiroFim);

        // Act
        Assert.ThrowsExactly<TransicaoInvalidaException>(() => turno.Encerrar(_inicio.AddHours(3)));

        // Assert
        Assert.AreEqual(primeiroFim, turno.Fim);
    }

    /// <summary>
    /// Garante que o encerramento antes do início é rejeitado.
    /// </summary>
    [TestMethod]
    public void Encerrar_AntesDoInicio_LancaValidacao()
    {
        // Arrange
        Turno turno = CriarTurno();

        // Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(() => turno.Encerrar(_inicio.AddSeconds(-1)));
        Assert.IsTrue(turno.EstaAtivo);
    }

    private static Turno CriarTurno()
    {
        return new Turno(Guid.NewGuid(), _inicio, _inicio.AddHours(4));
    }
}
