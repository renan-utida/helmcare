using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Servicos.Ocorrencias;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="EscalonadorOcorrencias"/>: quem escala, quando (60 s exatos já escalam) e
/// o conflito de concorrência isolado numa ocorrência só.
/// </summary>
[TestClass]
public sealed class EscalonadorOcorrenciasTests
{
    private readonly FakeOcorrenciaRepositorio _ocorrencias = new();
    private readonly FakeUnidadeDeTrabalho _unidade = new();
    private readonly FakeLogger<EscalonadorOcorrencias> _logger = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(CenarioSos.Sos));

    /// <summary>
    /// Garante a fronteira dos 60 s: 59 s não escala; 60 s exatos e 61 s escalam, gravam e registram o id.
    /// </summary>
    /// <param name="segundos">Segundos desde a abertura.</param>
    /// <param name="escala">Se deve escalar.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(59, false)]
    [DataRow(60, true)]
    [DataRow(61, true)]
    public async Task EscalarVencidasAsync_TempoDesdeAAbertura_EscalaAPartirDos60s(int segundos, bool escala)
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        _ocorrencias.Semear(ocorrencia);
        _relogio.Advance(TimeSpan.FromSeconds(segundos));

        // Act
        int escaladas = await Escalonador().EscalarVencidasAsync(CancellationToken.None);

        // Assert
        Assert.AreEqual(escala ? 1 : 0, escaladas);
        Assert.AreEqual(escala ? EstadoOcorrencia.SosCamada2 : EstadoOcorrencia.SosCamada1, ocorrencia.Estado);
        Assert.AreEqual(escala ? 2 : 1, ocorrencia.Camada);
        Assert.AreEqual(escala ? 1 : 0, _unidade.VezesSalvo);
        Assert.AreEqual(escala ? 1 : 0, _logger.Registros.Count(r => r.Nivel == LogLevel.Information && r.Mensagem.Contains(ocorrencia.Id.ToString(), StringComparison.Ordinal)));
    }

    /// <summary>
    /// Garante que a consulta recebe agora menos TEMPO_ACEITE_CAMADA_1_S como limite do criadaEm.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EscalarVencidasAsync_Consulta_UsaAgoraMenos60s()
    {
        // Arrange
        _relogio.Advance(TimeSpan.FromSeconds(90));

        // Act
        await Escalonador().EscalarVencidasAsync(CancellationToken.None);

        // Assert
        Assert.IsTrue(_ocorrencias.ConsultasDeVencidas.TryRead(out DateTime limite));
        Assert.AreEqual(CenarioSos.Sos.AddSeconds(30), limite);
    }

    /// <summary>
    /// Garante que ocorrências já na camada 2 ou fora da espera não mudam, mesmo muito depois dos 60 s.
    /// </summary>
    /// <param name="estado">Estado da ocorrência.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoOcorrencia.SosCamada2)]
    [DataRow(EstadoOcorrencia.SocorroACaminho)]
    [DataRow(EstadoOcorrencia.Encerrada)]
    [DataRow(EstadoOcorrencia.Cancelada)]
    public async Task EscalarVencidasAsync_ForaDaCamada1_NaoMuda(EstadoOcorrencia estado)
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(estado);
        int camada = ocorrencia.Camada;
        _ocorrencias.Semear(ocorrencia);
        _relogio.Advance(TimeSpan.FromHours(1));

        // Act
        int escaladas = await Escalonador().EscalarVencidasAsync(CancellationToken.None);

        // Assert
        Assert.AreEqual(0, escaladas);
        Assert.AreEqual(estado, ocorrencia.Estado);
        Assert.AreEqual(camada, ocorrencia.Camada);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que um conflito de concorrência (aceite ao mesmo tempo) na segunda ocorrência fica em
    /// Debug e não impede a primeira nem a terceira de escalarem.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EscalarVencidasAsync_ConflitoNumaOcorrencia_AsOutrasEscalam()
    {
        // Arrange
        Ocorrencia primeira = Abrir(CenarioSos.Sos.AddSeconds(-2));
        Ocorrencia segunda = Abrir(CenarioSos.Sos.AddSeconds(-1));
        Ocorrencia terceira = Abrir(CenarioSos.Sos);
        _unidade.FalhaNaChamada = chamada => chamada == 2 ? new DbUpdateConcurrencyException("aceite ao mesmo tempo") : null;
        _relogio.Advance(TimeSpan.FromSeconds(60));

        // Act
        int escaladas = await Escalonador().EscalarVencidasAsync(CancellationToken.None);

        // Assert
        Assert.AreEqual(2, escaladas);
        Assert.AreEqual(2, _unidade.VezesSalvo);
        Assert.AreEqual(
            string.Join(' ', primeira.Id, terceira.Id),
            string.Join(' ', _logger.Registros.Where(r => r.Nivel == LogLevel.Information).Select(r => Id(r.Mensagem))));
        RegistroLogFake conflito = _logger.Registros.Single(r => r.Nivel == LogLevel.Debug);
        Assert.Contains(segunda.Id.ToString(), conflito.Mensagem);
    }

    private static string Id(string mensagem)
    {
        return mensagem.Split(' ')[1];
    }

    private Ocorrencia Abrir(DateTime criadaEm)
    {
        var ocorrencia = Ocorrencia.Abrir(Guid.NewGuid(), CenarioSos.NovoEventoEmAberto(), CenarioSos.Local, criadaEm);
        _ocorrencias.Semear(ocorrencia);
        return ocorrencia;
    }

    private EscalonadorOcorrencias Escalonador()
    {
        return new EscalonadorOcorrencias(_ocorrencias, _unidade, _relogio, Options.Create(new OpcoesHelmCare()), _logger);
    }
}
