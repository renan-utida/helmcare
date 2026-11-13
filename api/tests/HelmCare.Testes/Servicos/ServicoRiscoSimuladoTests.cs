using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Servicos.Risco;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="ServicoRiscoSimulado"/> (tabela simulada de sinistros por hora e fórmula da seção 8).
/// </summary>
[TestClass]
public sealed class ServicoRiscoSimuladoTests
{
    private readonly ServicoRiscoSimulado _servico = new();

    /// <summary>
    /// Garante que os componentes e o índice batem com a calculadora, para o turno do exemplo do
    /// contrato (20:00Z a 00:00Z, ou 17h a 21h em São Paulo) e a previsão simulada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_TurnoDoExemplo_CoerenteComACalculadora()
    {
        // Arrange
        DateTime inicio = Utc(2026, 11, 12, 20, 0);
        DateTime fim = Utc(2026, 11, 13, 0, 0);
        PrevisaoTurno previsao = PrevisaoTempoSimulada.Exemplo;

        // Act
        ResultadoRisco resultado = await _servico.CalcularAsync(inicio, fim, previsao, CancellationToken.None);

        // Assert
        CalculoIndiceRisco esperado = CalculadoraIndiceRisco.Calcular(
            ServicoRiscoSimulado.SinistroDaHora(18),
            previsao.ChuvaMmHora,
            IndiceCalor.CalcularC(previsao.TemperaturaC, previsao.UmidadePct));
        Assert.AreEqual(80, resultado.Sinistro);
        Assert.AreEqual(70, resultado.Chuva);
        Assert.AreEqual(0, resultado.Calor);
        Assert.AreEqual(esperado.IndiceRisco, resultado.IndiceRisco);
        Assert.AreEqual(58, resultado.IndiceRisco);
        Assert.AreEqual(FaixaRisco.Alto, resultado.FaixaRisco);
    }

    /// <summary>
    /// Garante que o sinistro do turno é o maior valor entre as horas locais que ele cobre,
    /// inclusive a hora em que o fim cai quando ele não é exato.
    /// </summary>
    /// <param name="inicioHoraUtc">Hora UTC do início (minuto 0, a não ser no caso das 10:30).</param>
    /// <param name="inicioMinuto">Minuto do início.</param>
    /// <param name="duracaoMinutos">Duração do turno.</param>
    /// <param name="sinistroEsperado">Maior valor esperado.</param>
    /// <param name="horaLocalEsperada">Hora local do maior valor.</param>
    [TestMethod]
    [DataRow(9, 0, 180, 65, 8)]
    [DataRow(10, 30, 40, 65, 8)]
    [DataRow(3, 0, 60, 20, 0)]
    [DataRow(18, 0, 360, 80, 18)]
    public void MaiorSinistroDoTurno_HorasDoTurno_DevolveOMaior(
        int inicioHoraUtc,
        int inicioMinuto,
        int duracaoMinutos,
        int sinistroEsperado,
        int horaLocalEsperada)
    {
        // Arrange
        DateTime inicio = Utc(2026, 11, 12, inicioHoraUtc, inicioMinuto);

        // Act
        (int valor, int horaLocal) = ServicoRiscoSimulado.MaiorSinistroDoTurno(inicio, inicio.AddMinutes(duracaoMinutos));

        // Assert
        Assert.AreEqual(sinistroEsperado, valor);
        Assert.AreEqual(horaLocalEsperada, horaLocal);
    }

    /// <summary>
    /// Garante que a explicação cita o que mais pesou e separa o histórico de sinistros da exposição à chuva.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_Explicacao_CitaOQueMaisPesouEADiferencaDaChuva()
    {
        // Arrange
        DateTime inicio = Utc(2026, 11, 12, 20, 0);

        // Act
        ResultadoRisco resultado = await _servico.CalcularAsync(
            inicio, inicio.AddHours(4), PrevisaoTempoSimulada.Exemplo, CancellationToken.None);

        // Assert
        Assert.StartsWith("Risco alto (58)", resultado.Explicacao);
        Assert.Contains("o que mais pesou foi o histórico de sinistros graves", resultado.Explicacao);
        Assert.Contains("pico às 18h", resultado.Explicacao);
        Assert.Contains("já considera o efeito da chuva nos acidentes", resultado.Explicacao);
        Assert.Contains("7,0 mm/h", resultado.Explicacao);
        Assert.Contains("exposição direta", resultado.Explicacao);
        Assert.IsLessThanOrEqualTo(500, resultado.Explicacao.Length);
    }

    /// <summary>
    /// Garante que, com chuva forte num horário calmo, a explicação diz que a chuva foi o que mais pesou.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_ChuvaForteDeMadrugada_ChuvaMaisPesou()
    {
        // Arrange: 6h UTC é 3h em São Paulo (sinistro 10); 20 mm/h dá chuva 100.
        DateTime inicio = Utc(2026, 11, 12, 6, 0);
        var previsao = new PrevisaoTurno(ChuvaMmHora: 20, TemperaturaC: 24, UmidadePct: 85);

        // Act
        ResultadoRisco resultado = await _servico.CalcularAsync(inicio, inicio.AddHours(1), previsao, CancellationToken.None);

        // Assert
        Assert.AreEqual(10, resultado.Sinistro);
        Assert.AreEqual(100, resultado.Chuva);
        Assert.Contains("o que mais pesou foi a chuva prevista", resultado.Explicacao);
    }

    private static DateTime Utc(int ano, int mes, int dia, int hora, int minuto)
    {
        return new DateTime(ano, mes, dia, hora, minuto, 0, DateTimeKind.Utc);
    }
}
