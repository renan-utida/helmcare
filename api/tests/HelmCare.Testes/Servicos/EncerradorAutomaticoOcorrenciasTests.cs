using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Servicos.Ocorrencias;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="EncerradorAutomaticoOcorrencias"/> e da regra de domínio do encerramento
/// automático, com o <see cref="FakeTimeProvider"/>: 119 min não encerra, 120 min encerra (inclusivo).
/// </summary>
[TestClass]
public sealed class EncerradorAutomaticoOcorrenciasTests
{
    private const string Codigo = "HC-0001";

    private static readonly TimeSpan _tempo = TimeSpan.FromMinutes(120);

    private readonly FakeOcorrenciaRepositorio _ocorrencias = new();
    private readonly FakeDispositivoRepositorio _dispositivos = new();
    private readonly FakeUnidadeDeTrabalho _unidade = new();
    private readonly FakePublicadorComandos _publicador = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(CenarioSos.Sos));
    private readonly Guid _vitimaId = Guid.NewGuid();

    /// <summary>
    /// Vincula o capacete à vítima, para o ENCERRAR ter para onde ir.
    /// </summary>
    public EncerradorAutomaticoOcorrenciasTests()
    {
        _dispositivos.Semear(new Dispositivo(Codigo, _vitimaId));
    }

    /// <summary>
    /// Garante que, com 119 min, nada é encerrado nem publicado.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EncerrarVencidasAsync_119Minutos_NaoEncerra()
    {
        // Arrange
        Ocorrencia ocorrencia = Semear();
        _relogio.Advance(TimeSpan.FromMinutes(119));

        // Act
        int encerradas = await Encerrador().EncerrarVencidasAsync(CancellationToken.None);

        // Assert
        Assert.AreEqual(0, encerradas);
        Assert.AreEqual(EstadoOcorrencia.SosCamada1, ocorrencia.Estado);
        Assert.IsEmpty(_publicador.Comandos);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que, com 120 min exatos, a ocorrência sem aceite vai para ENCERRADA, com encerradaEm,
    /// encerradaAutomaticamente verdadeiro, indicador vazio e o ENCERRAR publicado.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EncerrarVencidasAsync_120MinutosSemAceite_EncerraEPublica()
    {
        // Arrange
        Ocorrencia ocorrencia = Semear();
        _relogio.Advance(_tempo);

        // Act
        int encerradas = await Encerrador().EncerrarVencidasAsync(CancellationToken.None);

        // Assert
        Assert.AreEqual(1, encerradas);
        Assert.AreEqual(EstadoOcorrencia.Encerrada, ocorrencia.Estado);
        Assert.IsTrue(ocorrencia.EncerradaAutomaticamente);
        Assert.AreEqual(CenarioSos.Sos + _tempo, ocorrencia.EncerradaEm);
        Assert.IsNull(ocorrencia.TempoAteAceiteSegundos);
        Assert.AreEqual(new ComandoPublicadoFake(Codigo, TipoComandoMqtt.Encerrar), _publicador.Comandos.Single());
        Assert.AreEqual(1, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que a ocorrência com aceite (SOCORRO_A_CAMINHO) também encerra, mantendo o indicador.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EncerrarVencidasAsync_ComAceite_MantemOIndicador()
    {
        // Arrange
        Ocorrencia ocorrencia = Semear();
        ocorrencia.Aceitar(
            CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima),
            CenarioSos.Aceite,
            CenarioSos.RaioCamada1Km,
            CenarioSos.TrechoCapacetePadrao);
        int? indicador = ocorrencia.TempoAteAceiteSegundos;
        _relogio.Advance(TimeSpan.FromMinutes(121));

        // Act
        await Encerrador().EncerrarVencidasAsync(CancellationToken.None);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.Encerrada, ocorrencia.Estado);
        Assert.IsTrue(ocorrencia.EncerradaAutomaticamente);
        Assert.IsNotNull(indicador);
        Assert.AreEqual(indicador, ocorrencia.TempoAteAceiteSegundos);
    }

    /// <summary>
    /// Garante que uma ocorrência já encerrada ou cancelada fica como está.
    /// </summary>
    /// <param name="estado">Estado final.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoOcorrencia.Encerrada)]
    [DataRow(EstadoOcorrencia.Cancelada)]
    public async Task EncerrarVencidasAsync_JaTerminada_FicaComoEsta(EstadoOcorrencia estado)
    {
        // Arrange
        Ocorrencia ocorrencia = Semear();
        DateTime fim = CenarioSos.Sos.AddMinutes(5);
        if (estado == EstadoOcorrencia.Encerrada)
        {
            ocorrencia.Encerrar(fim);
        }
        else
        {
            ocorrencia.Cancelar(fim);
        }

        _relogio.Advance(TimeSpan.FromHours(5));

        // Act
        int encerradas = await Encerrador().EncerrarVencidasAsync(CancellationToken.None);

        // Assert
        Assert.AreEqual(0, encerradas);
        Assert.AreEqual(estado, ocorrencia.Estado);
        Assert.AreEqual(fim, ocorrencia.EncerradaEm);
        Assert.IsFalse(ocorrencia.EncerradaAutomaticamente);
        Assert.IsEmpty(_publicador.Comandos);
    }

    /// <summary>
    /// Garante que um conflito de concorrência deixa aquela ocorrência para a próxima rodada, sem
    /// publicar, e não impede a seguinte.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EncerrarVencidasAsync_Conflito_SegueParaAProxima()
    {
        // Arrange
        Semear();
        Semear();
        _unidade.FalhaNaChamada = chamada => chamada == 1 ? new DbUpdateConcurrencyException("conflito") : null;
        _relogio.Advance(_tempo);

        // Act
        int encerradas = await Encerrador().EncerrarVencidasAsync(CancellationToken.None);

        // Assert
        Assert.AreEqual(1, encerradas);
        Assert.HasCount(1, _publicador.Comandos);
    }

    /// <summary>
    /// Garante a regra do domínio: antes do tempo, o encerramento automático é recusado sem mudar nada;
    /// numa ocorrência já terminada, é transição inválida.
    /// </summary>
    [TestMethod]
    public void EncerrarAutomaticamente_ForaDaRegra_Lanca()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        Ocorrencia terminada = CenarioSos.AbrirOcorrencia();
        terminada.Cancelar(CenarioSos.Aceite);

        // Act e Assert
        Assert.ThrowsExactly<RegraNegocioException>(
            () => ocorrencia.EncerrarAutomaticamente(CenarioSos.Sos.AddMinutes(119), _tempo));
        Assert.AreEqual(EstadoOcorrencia.SosCamada1, ocorrencia.Estado);
        Assert.ThrowsExactly<TransicaoInvalidaException>(
            () => terminada.EncerrarAutomaticamente(CenarioSos.Sos.AddMinutes(200), _tempo));
        Assert.IsFalse(ocorrencia.DeveEncerrarAutomaticamente(CenarioSos.Sos.AddMinutes(119), _tempo));
        Assert.IsTrue(ocorrencia.DeveEncerrarAutomaticamente(CenarioSos.Sos.AddMinutes(120), _tempo));
        Assert.IsFalse(terminada.DeveEncerrarAutomaticamente(CenarioSos.Sos.AddMinutes(200), _tempo));
    }

    private Ocorrencia Semear()
    {
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia(_vitimaId);
        _ocorrencias.Semear(ocorrencia);
        return ocorrencia;
    }

    private EncerradorAutomaticoOcorrencias Encerrador()
    {
        var comandos = new PublicadorComandosOcorrencia(_publicador, _dispositivos, new FakeLogger<PublicadorComandosOcorrencia>());

        return new EncerradorAutomaticoOcorrencias(
            _ocorrencias,
            _unidade,
            comandos,
            _relogio,
            Options.Create(new OpcoesHelmCare()),
            new FakeLogger<EncerradorAutomaticoOcorrencias>());
    }
}
