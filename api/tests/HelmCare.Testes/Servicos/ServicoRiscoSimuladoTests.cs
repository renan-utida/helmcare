using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Servicos.Risco;
using HelmCare.Dominio.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="ServicoRiscoSimulado"/>: tabela por dia e hora, média ponderada das horas
/// do turno e explicação iguais às do Serviço de Risco (seções 8 e 8.1 do contrato).
/// </summary>
[TestClass]
public sealed class ServicoRiscoSimuladoTests
{
    private readonly ServicoRiscoSimulado _servico = new();

    /// <summary>
    /// Garante o exemplo de referência do contrato (quinta, 17h às 21h local, 7 mm/h, 24 °C e 85%):
    /// 84, 70, 0, índice 60, ALTO e a mesma explicação do serviço.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_ExemploDeReferencia_IgualAoServico()
    {
        // Act
        ResultadoRisco resultado = await _servico.CalcularAsync(
            Utc(2026, 11, 12, 20, 0), Utc(2026, 11, 13, 0, 0), PrevisaoTempoSimulada.Exemplo, CancellationToken.None);

        // Assert
        Assert.AreEqual(
            new ResultadoRisco(
                60,
                FaixaRisco.Alto,
                84,
                70,
                0,
                "Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros graves (pico às 19h)."),
            resultado);
    }

    /// <summary>
    /// Garante as horas do exemplo (17h a 20h de quinta, 3600 s cada) e que o fim exatamente na hora
    /// cheia não inclui a hora seguinte.
    /// </summary>
    [TestMethod]
    public void HorasDoTurno_ExemploDeReferencia_QuatroHorasInteiras()
    {
        // Act
        IReadOnlyList<HoraTurnoSimulada> horas = ServicoRiscoSimulado.HorasDoTurno(Utc(2026, 11, 12, 20, 0), Utc(2026, 11, 13, 0, 0));

        // Assert
        Assert.HasCount(4, horas);
        Assert.AreEqual("17 18 19 20", string.Join(' ', horas.Select(h => h.HoraLocal)));
        Assert.IsTrue(horas is [{ DiaLocal: DayOfWeek.Thursday, SegundosNoTurno: 3600 }, _, _, { DiaLocal: DayOfWeek.Thursday, SegundosNoTurno: 3600 }]);
        Assert.AreEqual(87.50, horas[2].Sinistro);
    }

    /// <summary>
    /// Garante a hora parcial: quinta das 17h30 às 19h local pesa 1800 s na hora 17 e 3600 s na
    /// hora 18, e dá sinistro 85 ((83,38 × 1800 + 85,18 × 3600) ÷ 5400 = 84,58).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_HoraParcial_PesaOsSegundosDentroDoTurno()
    {
        // Arrange
        DateTime inicio = Utc(2026, 11, 12, 20, 30);
        DateTime fim = Utc(2026, 11, 12, 22, 0);

        // Act
        IReadOnlyList<HoraTurnoSimulada> horas = ServicoRiscoSimulado.HorasDoTurno(inicio, fim);
        ResultadoRisco resultado = await _servico.CalcularAsync(inicio, fim, PrevisaoTempoSimulada.Exemplo, CancellationToken.None);

        // Assert
        Assert.HasCount(2, horas);
        Assert.AreEqual(new HoraTurnoSimulada(Utc(2026, 11, 12, 20, 0), DayOfWeek.Thursday, 17, 1800, 83.38), horas[0]);
        Assert.AreEqual(new HoraTurnoSimulada(Utc(2026, 11, 12, 21, 0), DayOfWeek.Thursday, 18, 3600, 85.18), horas[1]);
        Assert.AreEqual(85, resultado.Sinistro);
        Assert.Contains("(pico às 18h)", resultado.Explicacao);
    }

    /// <summary>
    /// Garante que um turno que vira o dia local usa a linha do dia seguinte depois da meia-noite.
    /// </summary>
    [TestMethod]
    public void HorasDoTurno_ViraODia_UsaALinhaDoDiaSeguinte()
    {
        // Act: 02:30Z a 04:00Z de 13/11 é quinta 23h30 até sexta 1h em São Paulo.
        IReadOnlyList<HoraTurnoSimulada> horas = ServicoRiscoSimulado.HorasDoTurno(Utc(2026, 11, 13, 2, 30), Utc(2026, 11, 13, 4, 0));

        // Assert
        Assert.AreEqual(new HoraTurnoSimulada(Utc(2026, 11, 13, 2, 0), DayOfWeek.Thursday, 23, 1800, 53.64), horas[0]);
        Assert.AreEqual(new HoraTurnoSimulada(Utc(2026, 11, 13, 3, 0), DayOfWeek.Friday, 0, 3600, 42.10), horas[1]);
    }

    /// <summary>
    /// Garante a conversão do DayOfWeek do .NET (domingo = 0) para a linha da tabela (segunda = 0).
    /// </summary>
    /// <param name="dia">Dia da semana.</param>
    /// <param name="esperado">Valor da tabela às 0h.</param>
    [TestMethod]
    [DataRow(DayOfWeek.Monday, 62.26)]
    [DataRow(DayOfWeek.Thursday, 39.11)]
    [DataRow(DayOfWeek.Saturday, 77.65)]
    [DataRow(DayOfWeek.Sunday, 80.78)]
    public void SinistroDaHora_DiaDaSemana_UsaALinhaCerta(DayOfWeek dia, double esperado)
    {
        // Act e Assert
        Assert.AreEqual(esperado, ServicoRiscoSimulado.SinistroDaHora(dia, 0));
    }

    /// <summary>
    /// Garante que um turno de domingo usa a linha de domingo (18h: 89,78 dá 90; na quinta seria 85).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_TurnoDeDomingo_UsaALinhaDeDomingo()
    {
        // Act: 22/11/2026 é domingo; 21:00Z a 22:00Z é 18h em São Paulo.
        ResultadoRisco resultado = await _servico.CalcularAsync(
            Utc(2026, 11, 22, 21, 0), Utc(2026, 11, 22, 22, 0), PrevisaoTempoSimulada.Exemplo, CancellationToken.None);

        // Assert
        Assert.AreEqual(90, resultado.Sinistro);
    }

    /// <summary>
    /// Garante a frase do histórico moderado (segunda, 1h às 4h local: sinistro 44), sem chuva.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_HistoricoModerado_FraseModerada()
    {
        // Act: 16/11/2026 é segunda; 04:00Z a 07:00Z é 1h às 4h em São Paulo.
        ResultadoRisco resultado = await _servico.CalcularAsync(
            Utc(2026, 11, 16, 4, 0), Utc(2026, 11, 16, 7, 0), SemChuva(), CancellationToken.None);

        // Assert
        Assert.AreEqual(44, resultado.Sinistro);
        Assert.AreEqual("Risco baixo: o seu horário tem histórico moderado de sinistros graves.", resultado.Explicacao);
    }

    /// <summary>
    /// Garante a frase de poucos sinistros (terça, 2h às 5h local: sinistro 19), junto com a chuva.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_PoucosSinistros_FraseDePoucosSinistros()
    {
        // Act: 17/11/2026 é terça; 05:00Z a 08:00Z é 2h às 5h em São Paulo.
        ResultadoRisco resultado = await _servico.CalcularAsync(
            Utc(2026, 11, 17, 5, 0), Utc(2026, 11, 17, 8, 0), PrevisaoTempoSimulada.Exemplo, CancellationToken.None);

        // Assert
        Assert.AreEqual(19, resultado.Sinistro);
        Assert.AreEqual(
            "Risco moderado: chuva prevista no turno e o seu horário tem poucos sinistros graves no histórico.",
            resultado.Explicacao);
    }

    /// <summary>
    /// Garante que, sem chuva, a frase começa pelo histórico.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_SemChuva_ComecaPeloHistorico()
    {
        // Act
        ResultadoRisco resultado = await _servico.CalcularAsync(
            Utc(2026, 11, 12, 20, 0), Utc(2026, 11, 13, 0, 0), SemChuva(), CancellationToken.None);

        // Assert
        Assert.AreEqual(0, resultado.Chuva);
        Assert.AreEqual(
            "Risco moderado: o seu horário tem histórico de sinistros graves (pico às 19h).",
            resultado.Explicacao);
    }

    /// <summary>
    /// Garante a faixa "muito alto" com os três trechos (sexta, 19h às 21h local, 10 mm/h, 33,4 °C e 58%).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_MuitoAltoComOsTresTrechos_FraseCompleta()
    {
        // Arrange
        var previsao = new PrevisaoTurno(ChuvaMmHora: 10, TemperaturaC: 33.4, UmidadePct: 58);

        // Act
        ResultadoRisco resultado = await _servico.CalcularAsync(
            Utc(2026, 11, 13, 22, 0), Utc(2026, 11, 14, 0, 0), previsao, CancellationToken.None);

        // Assert
        Assert.AreEqual(FaixaRisco.MuitoAlto, resultado.FaixaRisco);
        Assert.AreEqual(
            "Risco muito alto: chuva prevista no turno, o seu horário tem histórico de sinistros graves (pico às 20h) "
            + "e calor previsto no turno (índice de calor de 39,8 °C).",
            resultado.Explicacao);
        Assert.DoesNotContain("já considera", resultado.Explicacao);
    }

    /// <summary>
    /// Garante a recusa de um turno com o fim antes do início.
    /// </summary>
    [TestMethod]
    public void HorasDoTurno_FimAntesDoInicio_LancaArgumentException()
    {
        // Act e Assert
        Assert.ThrowsExactly<ArgumentException>(
            () => ServicoRiscoSimulado.HorasDoTurno(Utc(2026, 11, 12, 20, 0), Utc(2026, 11, 12, 20, 0)));
    }

    private static PrevisaoTurno SemChuva()
    {
        return PrevisaoTempoSimulada.Exemplo with { ChuvaMmHora = 0 };
    }

    private static DateTime Utc(int ano, int mes, int dia, int hora, int minuto)
    {
        return new DateTime(ano, mes, dia, hora, minuto, 0, DateTimeKind.Utc);
    }
}
