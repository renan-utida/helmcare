using System;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes do registro do impacto e dos resultados vindos dos eventos MQTT (seção 6.2).
/// </summary>
[TestClass]
public sealed class EventoImpactoTests
{
    /// <summary>
    /// Garante que, com pilotando e presença falsos, o impacto já nasce IGNORADO.
    /// </summary>
    [TestMethod]
    public void Registrar_SemPilotagemNemPresenca_NasceIgnorado()
    {
        // Arrange e Act
        var evento = EventoImpacto.Registrar(Guid.NewGuid(), CenarioSos.Queda, 3.1, false, false, false);

        // Assert
        Assert.AreEqual(ResultadoImpacto.Ignorado, evento.Resultado);
        Assert.IsFalse(evento.EstaEmAberto);
        Assert.IsFalse(evento.OcorridoEmEstimado);
    }

    /// <summary>
    /// Garante que basta pilotando ou presença verdadeiro para o impacto nascer em aberto.
    /// </summary>
    /// <param name="pilotandoAntes">Se estava pilotando.</param>
    /// <param name="presencaAntes">Se o capacete estava na cabeça.</param>
    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void Registrar_ComPilotagemOuPresenca_NasceEmAberto(bool pilotandoAntes, bool presencaAntes)
    {
        // Arrange e Act
        var evento = EventoImpacto.Registrar(Guid.NewGuid(), CenarioSos.Queda, 4.8, true, pilotandoAntes, presencaAntes);

        // Assert
        Assert.IsNull(evento.Resultado);
        Assert.IsTrue(evento.EstaEmAberto);
        Assert.AreEqual(CenarioSos.Queda, evento.OcorridoEm);
        Assert.AreEqual(4.8, evento.PicoG);
        Assert.IsTrue(evento.OrientacaoAnormal);
    }

    /// <summary>
    /// Garante que pico zero, negativo, NaN ou infinito é rejeitado.
    /// </summary>
    /// <param name="picoG">Pico inválido.</param>
    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void Registrar_PicoGInvalido_LancaValidacao(double picoG)
    {
        // Arrange, Act e Assert
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => EventoImpacto.Registrar(Guid.NewGuid(), CenarioSos.Queda, picoG, false, true, true));

        Assert.Contains("picoG", excecao.Message);
    }

    /// <summary>
    /// Garante que o impacto estimado pelo SOS fica 25 s antes do SOS e marcado como estimado.
    /// </summary>
    [TestMethod]
    public void RegistrarEstimadoPorSos_SosComAntecedencia_EstimaOcorridoEm()
    {
        // Arrange e Act
        var evento = EventoImpacto.RegistrarEstimadoPorSos(
            Guid.NewGuid(), CenarioSos.Sos, CenarioSos.AntecedenciaSos, 4.8, true, false, false);

        // Assert
        Assert.AreEqual(CenarioSos.Queda, evento.OcorridoEm);
        Assert.IsTrue(evento.OcorridoEmEstimado);
        Assert.IsTrue(evento.EstaEmAberto, "O SOS prova a queda: não aplica a regra do IGNORADO.");
    }

    /// <summary>
    /// Garante que antecedência zero é rejeitada.
    /// </summary>
    [TestMethod]
    public void RegistrarEstimadoPorSos_AntecedenciaZero_LancaValidacao()
    {
        // Arrange, Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(
            () => EventoImpacto.RegistrarEstimadoPorSos(Guid.NewGuid(), CenarioSos.Sos, TimeSpan.Zero, 4.8, true, true, true));
    }

    /// <summary>
    /// Garante que a contagem guarda o fim como ts mais 15 s.
    /// </summary>
    [TestMethod]
    public void IniciarContagem_EventoEmAberto_PreencheContagemTerminaEm()
    {
        // Arrange
        EventoImpacto evento = CenarioSos.NovoEventoEmAberto();
        DateTime inicioContagem = CenarioSos.Queda.AddSeconds(10);

        // Act
        evento.IniciarContagem(inicioContagem, CenarioSos.Contagem);

        // Assert
        Assert.AreEqual(inicioContagem.AddSeconds(15), evento.ContagemTerminaEm);
        Assert.IsTrue(evento.EstaEmAberto);
    }

    /// <summary>
    /// Garante que a contagem só pode ser iniciada uma vez e que a segunda chamada não muda o fim.
    /// </summary>
    [TestMethod]
    public void IniciarContagem_SegundaVez_LancaTransicaoInvalida()
    {
        // Arrange
        EventoImpacto evento = CenarioSos.NovoEventoEmAberto();
        DateTime inicioContagem = CenarioSos.Queda.AddSeconds(10);
        evento.IniciarContagem(inicioContagem, CenarioSos.Contagem);

        // Act
        Assert.ThrowsExactly<TransicaoInvalidaException>(
            () => evento.IniciarContagem(inicioContagem.AddSeconds(5), CenarioSos.Contagem));

        // Assert
        Assert.AreEqual(inicioContagem.AddSeconds(15), evento.ContagemTerminaEm);
    }

    /// <summary>
    /// Garante que a contagem não pode começar antes do impacto.
    /// </summary>
    [TestMethod]
    public void IniciarContagem_AntesDoImpacto_LancaValidacao()
    {
        // Arrange
        EventoImpacto evento = CenarioSos.NovoEventoEmAberto();

        // Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(
            () => evento.IniciarContagem(CenarioSos.Queda.AddSeconds(-1), CenarioSos.Contagem));
        Assert.IsNull(evento.ContagemTerminaEm);
    }

    /// <summary>
    /// Garante que FALSO_POSITIVO e CANCELADO definem o resultado, mesmo sem contagem iniciada (QoS 0).
    /// </summary>
    /// <param name="cancelado">Verdadeiro para CANCELADO, falso para FALSO_POSITIVO.</param>
    /// <param name="esperado">Resultado esperado.</param>
    [TestMethod]
    [DataRow(false, ResultadoImpacto.FalsoPositivo)]
    [DataRow(true, ResultadoImpacto.CanceladoPeloEntregador)]
    public void MarcarResultado_EventoEmAberto_DefineResultado(bool cancelado, ResultadoImpacto esperado)
    {
        // Arrange
        EventoImpacto evento = CenarioSos.NovoEventoEmAberto();

        // Act
        if (cancelado)
        {
            evento.MarcarCancelado();
        }
        else
        {
            evento.MarcarFalsoPositivo();
        }

        // Assert
        Assert.AreEqual(esperado, evento.Resultado);
        Assert.IsFalse(evento.EstaEmAberto);
    }

    /// <summary>
    /// Garante que, com um resultado final, nenhum dos dois métodos troca o resultado.
    /// </summary>
    /// <param name="primeiroCancelado">Qual resultado é definido primeiro.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MarcarResultado_ResultadoJaFinal_LancaTransicaoInvalidaSemAlterar(bool primeiroCancelado)
    {
        // Arrange
        EventoImpacto evento = CenarioSos.NovoEventoEmAberto();

        if (primeiroCancelado)
        {
            evento.MarcarCancelado();
        }
        else
        {
            evento.MarcarFalsoPositivo();
        }

        ResultadoImpacto? antes = evento.Resultado;

        // Act e Assert
        Assert.ThrowsExactly<TransicaoInvalidaException>(evento.MarcarFalsoPositivo);
        Assert.ThrowsExactly<TransicaoInvalidaException>(evento.MarcarCancelado);
        Assert.ThrowsExactly<TransicaoInvalidaException>(
            () => evento.IniciarContagem(CenarioSos.Queda.AddSeconds(10), CenarioSos.Contagem));
        Assert.AreEqual(antes, evento.Resultado);
        Assert.IsNull(evento.ContagemTerminaEm);
    }

    /// <summary>
    /// Garante que um impacto IGNORADO não aceita resultado depois.
    /// </summary>
    [TestMethod]
    public void MarcarFalsoPositivo_EventoIgnorado_LancaTransicaoInvalida()
    {
        // Arrange
        var evento = EventoImpacto.Registrar(Guid.NewGuid(), CenarioSos.Queda, 3.1, false, false, false);

        // Act e Assert
        Assert.ThrowsExactly<TransicaoInvalidaException>(evento.MarcarFalsoPositivo);
        Assert.AreEqual(ResultadoImpacto.Ignorado, evento.Resultado);
    }
}
