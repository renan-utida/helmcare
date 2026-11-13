using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Ocorrencias;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Ocorrencias;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="OcorrenciaServico"/>: localização exata só para quem aceitou e o cronômetro.
/// </summary>
[TestClass]
public sealed class OcorrenciaServicoTests
{
    private readonly FakeOcorrenciaRepositorio _ocorrencias = new();
    private readonly FakeVoluntarioRepositorio _voluntarios = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(CenarioSos.Sos.AddSeconds(20)));
    private readonly OcorrenciaServico _servico;

    /// <summary>
    /// Monta o serviço com o relógio 20 s depois da abertura da ocorrência de referência.
    /// </summary>
    public OcorrenciaServicoTests()
    {
        _servico = new OcorrenciaServico(_ocorrencias, _voluntarios, _relogio, Options.Create(new OpcoesHelmCare()));
    }

    /// <summary>
    /// Garante que, sem voluntarioId, a localização vem aproximada (2 casas) e o cronômetro corre.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_SemVoluntario_AproximadaECronometroCorrendo()
    {
        // Arrange
        Ocorrencia ocorrencia = Semear(CenarioSos.AbrirOcorrencia());

        // Act
        OcorrenciaResposta resposta = await _servico.ObterAsync(ocorrencia.Id, null, CancellationToken.None);

        // Assert
        Assert.AreEqual(-23.56, resposta.Latitude);
        Assert.AreEqual(-46.65, resposta.Longitude);
        Assert.IsFalse(resposta.LocalizacaoExata);
        Assert.AreEqual(EstadoOcorrencia.SosCamada1, resposta.Estado);
        Assert.AreEqual(1, resposta.Camada);
        Assert.IsFalse(resposta.Cronometro.Parado);
        Assert.AreEqual(45, resposta.Cronometro.SegundosDesdeQueda);
        Assert.AreEqual(120, resposta.Cronometro.MetaSegundos);
        Assert.IsNull(resposta.TempoAteAceiteSegundos);

        // O relógio anda e o cronômetro acompanha.
        _relogio.Advance(TimeSpan.FromSeconds(10));
        OcorrenciaResposta depois = await _servico.ObterAsync(ocorrencia.Id, null, CancellationToken.None);
        Assert.AreEqual(55, depois.Cronometro.SegundosDesdeQueda);
    }

    /// <summary>
    /// Garante a localização exata só para o voluntário que aceitou, e o cronômetro parado com o
    /// mesmo valor do tempoAteAceiteSegundos.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_VoluntarioQueAceitou_ExataECronometroParado()
    {
        // Arrange
        (Ocorrencia ocorrencia, Voluntario quemAceitou) = SemearAceita();
        _relogio.Advance(TimeSpan.FromMinutes(30));

        // Act
        OcorrenciaResposta resposta = await _servico.ObterAsync(ocorrencia.Id, quemAceitou.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(CenarioSos.Local.Latitude, resposta.Latitude);
        Assert.AreEqual(CenarioSos.Local.Longitude, resposta.Longitude);
        Assert.IsTrue(resposta.LocalizacaoExata);
        Assert.IsTrue(resposta.Cronometro.Parado);
        Assert.AreEqual(63, resposta.TempoAteAceiteSegundos);
        Assert.AreEqual(resposta.TempoAteAceiteSegundos, resposta.Cronometro.SegundosDesdeQueda);
        Assert.AreEqual(CenarioSos.Aceite, resposta.AceitaEm);
    }

    /// <summary>
    /// Garante que outro voluntário, mesmo depois do aceite, recebe a localização aproximada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_OutroVoluntario_Aproximada()
    {
        // Arrange
        (Ocorrencia ocorrencia, _) = SemearAceita();
        Voluntario outro = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima);
        _voluntarios.Semear(outro);

        // Act
        OcorrenciaResposta resposta = await _servico.ObterAsync(ocorrencia.Id, outro.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(-23.56, resposta.Latitude);
        Assert.AreEqual(-46.65, resposta.Longitude);
        Assert.IsFalse(resposta.LocalizacaoExata);
    }

    /// <summary>
    /// Garante que, encerrada sem aceite, o cronômetro para no encerramento.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_EncerradaSemAceite_CronometroParadoNoEncerramento()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        ocorrencia.Encerrar(CenarioSos.Sos.AddSeconds(58));
        Semear(ocorrencia);
        _relogio.Advance(TimeSpan.FromHours(1));

        // Act
        OcorrenciaResposta resposta = await _servico.ObterAsync(ocorrencia.Id, null, CancellationToken.None);

        // Assert
        Assert.IsTrue(resposta.Cronometro.Parado);
        Assert.AreEqual(83, resposta.Cronometro.SegundosDesdeQueda);
        Assert.IsNull(resposta.TempoAteAceiteSegundos);
    }

    /// <summary>
    /// Garante que a queda no relógio atrasado do capacete (3 h antes do servidor) não infla o cronômetro.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_RelogioDoCapaceteAtrasado_CronometroNaoInfla()
    {
        // Arrange: queda e contagem 3 h antes no relógio do capacete; o servidor abre às 21:16:02.
        var impacto = EventoImpacto.Registrar(Guid.NewGuid(), CenarioSos.Queda.AddHours(-3), 4.8, true, true, true);
        impacto.IniciarContagem(CenarioSos.Queda.AddHours(-3).AddSeconds(10), CenarioSos.Contagem);
        Ocorrencia ocorrencia = Semear(CenarioSos.AbrirOcorrencia(evento: impacto));

        // Act
        OcorrenciaResposta resposta = await _servico.ObterAsync(ocorrencia.Id, null, CancellationToken.None);

        // Assert: 25 s no capacete mais 20 s no servidor.
        Assert.AreEqual(45, resposta.Cronometro.SegundosDesdeQueda);
    }

    /// <summary>
    /// Garante o 404 para ocorrência inexistente e para voluntarioId informado e inexistente.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_OcorrenciaOuVoluntarioInexistente_Lanca404()
    {
        // Arrange
        Ocorrencia ocorrencia = Semear(CenarioSos.AbrirOcorrencia());

        // Act
        RecursoNaoEncontradoException semOcorrencia = await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.ObterAsync(Guid.NewGuid(), null, CancellationToken.None));
        RecursoNaoEncontradoException semVoluntario = await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.ObterAsync(ocorrencia.Id, Guid.NewGuid(), CancellationToken.None));

        // Assert
        Assert.Contains("Ocorrência", semOcorrencia.Message);
        Assert.Contains("Voluntário", semVoluntario.Message);
    }

    private Ocorrencia Semear(Ocorrencia ocorrencia)
    {
        _ocorrencias.Semear(ocorrencia);
        return ocorrencia;
    }

    private (Ocorrencia Ocorrencia, Voluntario QuemAceitou) SemearAceita()
    {
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima);
        ocorrencia.Aceitar(voluntario, CenarioSos.Aceite, CenarioSos.RaioCamada1Km, CenarioSos.TrechoCapacetePadrao);
        _voluntarios.Semear(voluntario);
        return (Semear(ocorrencia), voluntario);
    }
}
