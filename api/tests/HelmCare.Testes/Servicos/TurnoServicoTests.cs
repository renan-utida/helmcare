using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Turnos;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Risco;
using HelmCare.Api.Servicos.Turnos;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="TurnoServico"/> com repositórios, publicador e relógio falsos, e o risco
/// simulado de verdade (é uma conta pura, sem rede).
/// </summary>
[TestClass]
public sealed class TurnoServicoTests
{
    private static readonly DateTime Agora = new(2026, 11, 12, 22, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Inicio = new(2026, 11, 12, 20, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FimPrevisto = new(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeTurnoRepositorio _turnos = new();
    private readonly FakeEntregadorRepositorio _entregadores = new();
    private readonly FakeDispositivoRepositorio _dispositivos = new();
    private readonly FakeOcorrenciaRepositorio _ocorrencias = new();
    private readonly FakePublicadorComandos _publicador = new();
    private readonly FakeUnidadeDeTrabalho _unidade = new();
    private readonly TurnoServico _servico;
    private readonly Entregador _entregador = CenarioSos.NovoEntregador();
    private readonly Dispositivo _capacete;

    /// <summary>
    /// Monta o serviço com um entregador que tem o capacete HC-0001 e o relógio às 22:00Z.
    /// </summary>
    public TurnoServicoTests()
    {
        _entregadores.Semear(_entregador);
        _capacete = new Dispositivo("HC-0001", _entregador.Id);
        _dispositivos.Semear(_capacete);

        _servico = new TurnoServico(
            _turnos,
            _entregadores,
            _dispositivos,
            _ocorrencias,
            new PrevisaoTempoSimulada(),
            new ServicoRiscoSimulado(),
            _publicador,
            _unidade,
            new FakeTimeProvider(new DateTimeOffset(Agora)));
    }

    /// <summary>
    /// Garante que o início grava o turno com o risco, publica ARMAR e devolve o índice no formato do contrato.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task IniciarAsync_ComProtecao_GravaPublicaArmarEDevolveORisco()
    {
        // Act
        RiscoTurnoResposta resposta = await _servico.IniciarAsync(Requisicao(), CancellationToken.None);

        // Assert
        Turno gravado = _turnos.Turnos.Single();
        Assert.AreEqual(gravado.Id, resposta.TurnoId);
        Assert.AreEqual(58, resposta.IndiceRisco);
        Assert.AreEqual(FaixaRisco.Alto, resposta.FaixaRisco);
        Assert.AreEqual(80, resposta.Componentes.Sinistro);
        Assert.AreEqual(70, resposta.Componentes.Chuva);
        Assert.AreEqual(0, resposta.Componentes.Calor);
        Assert.AreEqual(gravado.Explicacao, resposta.Explicacao);
        Assert.AreEqual(58, gravado.IndiceRisco);
        Assert.IsTrue(gravado.ProtecaoAtiva);
        Assert.AreEqual(new ComandoPublicadoFake("HC-0001", TipoComandoMqtt.Armar), _publicador.Comandos.Single());
        Assert.AreEqual(1, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que, sem proteção, o turno abre sem publicar nada, com ou sem capacete.
    /// </summary>
    /// <param name="comCapacete">Se o entregador tem capacete.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task IniciarAsync_SemProtecao_GravaSemPublicar(bool comCapacete)
    {
        // Arrange
        Entregador entregador = comCapacete ? _entregador : SemearEntregadorSemCapacete();

        // Act
        await _servico.IniciarAsync(Requisicao(entregador.Id, protecaoAtiva: false), CancellationToken.None);

        // Assert
        Assert.IsFalse(_turnos.Turnos.Single().ProtecaoAtiva);
        Assert.IsEmpty(_publicador.Comandos);
        Assert.AreEqual(1, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante cada 400 do início, sem gravar nem publicar.
    /// </summary>
    /// <param name="caso">Qual dado está errado.</param>
    /// <param name="trechoDaMensagem">Parte esperada da mensagem.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("fimIgual", "fimPrevisto")]
    [DataRow("fimAntes", "fimPrevisto")]
    [DataRow("maisDe24h", "24 horas")]
    [DataRow("semEntregador", "entregadorId")]
    [DataRow("semInicio", "inicio")]
    [DataRow("semFim", "fimPrevisto")]
    public async Task IniciarAsync_DadosInvalidos_Lanca400SemGravar(string caso, string trechoDaMensagem)
    {
        // Arrange
        IniciarTurnoRequisicao requisicao = caso switch
        {
            "fimIgual" => Requisicao(fimPrevisto: Inicio),
            "fimAntes" => Requisicao(fimPrevisto: Inicio.AddMinutes(-1)),
            "maisDe24h" => Requisicao(fimPrevisto: Inicio.AddHours(24).AddMinutes(1)),
            "semEntregador" => new IniciarTurnoRequisicao { Inicio = Inicio, FimPrevisto = FimPrevisto },
            "semInicio" => new IniciarTurnoRequisicao { EntregadorId = _entregador.Id, FimPrevisto = FimPrevisto },
            _ => new IniciarTurnoRequisicao { EntregadorId = _entregador.Id, Inicio = Inicio },
        };

        // Act
        ValidacaoDominioException excecao = await Assert.ThrowsExactlyAsync<ValidacaoDominioException>(
            () => _servico.IniciarAsync(requisicao, CancellationToken.None));

        // Assert
        Assert.Contains(trechoDaMensagem, excecao.Message);
        AssertNenhumTurnoCriado();
    }

    /// <summary>
    /// Garante que as datas com fuso, ou sem fuso, são guardadas em UTC.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task IniciarAsync_DatasForaDeUtc_ConverteParaUtc()
    {
        // Arrange: o início com Kind Local (como o JSON entrega "-03:00") e o fim sem fuso.
        DateTime inicioLocal = Inicio.ToLocalTime();
        var fimSemFuso = DateTime.SpecifyKind(FimPrevisto, DateTimeKind.Unspecified);

        // Act
        await _servico.IniciarAsync(Requisicao(inicio: inicioLocal, fimPrevisto: fimSemFuso), CancellationToken.None);

        // Assert
        Turno gravado = _turnos.Turnos.Single();
        Assert.AreEqual(Inicio, gravado.Inicio);
        Assert.AreEqual(DateTimeKind.Utc, gravado.Inicio.Kind);
        Assert.AreEqual(FimPrevisto, gravado.FimPrevisto);
        Assert.AreEqual(DateTimeKind.Utc, gravado.FimPrevisto.Kind);
    }

    /// <summary>
    /// Garante o 404 para entregador inexistente.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task IniciarAsync_EntregadorInexistente_Lanca404()
    {
        // Act e Assert
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.IniciarAsync(Requisicao(Guid.NewGuid()), CancellationToken.None));
        AssertNenhumTurnoCriado();
    }

    /// <summary>
    /// Garante o 409 para um segundo turno ativo, sem criar nada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task IniciarAsync_TurnoAtivo_Lanca409SemCriar()
    {
        // Arrange
        Turno ativo = SemearTurno();

        // Act
        RegraNegocioException excecao = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.IniciarAsync(Requisicao(), CancellationToken.None));

        // Assert
        Assert.Contains("turno ativo", excecao.Message);
        Assert.AreSame(ativo, _turnos.Turnos.Single());
        Assert.IsEmpty(_publicador.Comandos);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 409 para proteção ativa sem capacete vinculado.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task IniciarAsync_ProtecaoSemCapacete_Lanca409()
    {
        // Arrange
        Entregador semCapacete = SemearEntregadorSemCapacete();

        // Act
        RegraNegocioException excecao = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.IniciarAsync(Requisicao(semCapacete.Id), CancellationToken.None));

        // Assert
        Assert.Contains("capacete", excecao.Message);
        AssertNenhumTurnoCriado();
    }

    /// <summary>
    /// Garante que, com o broker fora do ar, o início lança 503 e nada é gravado.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task IniciarAsync_BrokerForaDoAr_Lanca503SemGravar()
    {
        // Arrange
        _publicador.Indisponivel = true;

        // Act e Assert
        await Assert.ThrowsExactlyAsync<MensageriaIndisponivelException>(
            () => _servico.IniciarAsync(Requisicao(), CancellationToken.None));
        AssertNenhumTurnoCriado();
    }

    /// <summary>
    /// Garante que o GET do risco devolve o índice guardado, no mesmo formato do início.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterRiscoAsync_TurnoExistente_DevolveOIndiceGuardado()
    {
        // Arrange
        RiscoTurnoResposta inicio = await _servico.IniciarAsync(Requisicao(), CancellationToken.None);

        // Act
        RiscoTurnoResposta risco = await _servico.ObterRiscoAsync(inicio.TurnoId, CancellationToken.None);

        // Assert
        Assert.AreEqual(inicio.TurnoId, risco.TurnoId);
        Assert.AreEqual(inicio.IndiceRisco, risco.IndiceRisco);
        Assert.AreEqual(inicio.FaixaRisco, risco.FaixaRisco);
        Assert.AreEqual(inicio.Componentes.Sinistro, risco.Componentes.Sinistro);
        Assert.AreEqual(inicio.Explicacao, risco.Explicacao);
    }

    /// <summary>
    /// Garante o 404 do GET do risco.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterRiscoAsync_TurnoInexistente_Lanca404()
    {
        // Act e Assert
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.ObterRiscoAsync(Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>
    /// Garante que o encerramento publica DESARMAR e grava, nos estados sem alerta (CANCELADO e
    /// FALSO_POSITIVO já voltaram para ARMADO no capacete).
    /// </summary>
    /// <param name="estado">Último estado conhecido do capacete.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoDispositivo.Armado)]
    [DataRow(EstadoDispositivo.Cancelado)]
    [DataRow(EstadoDispositivo.FalsoPositivo)]
    [DataRow(EstadoDispositivo.Desarmado)]
    public async Task EncerrarAsync_SemAlerta_PublicaDesarmarEGrava(EstadoDispositivo estado)
    {
        // Arrange
        Turno turno = SemearTurno();
        _capacete.RegistrarEstado(estado, Inicio);

        // Act
        TurnoResposta resposta = await _servico.EncerrarAsync(turno.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(new ComandoPublicadoFake("HC-0001", TipoComandoMqtt.Desarmar), _publicador.Comandos.Single());
        Assert.AreEqual(1, _unidade.VezesSalvo);
        Assert.IsFalse(turno.EstaAtivo);
        Assert.AreEqual(Agora, resposta.Fim);
        Assert.IsFalse(resposta.ProtecaoAtiva);
        Assert.AreEqual(turno.Id, resposta.TurnoId);
        Assert.AreEqual(_entregador.Id, resposta.EntregadorId);
        Assert.AreEqual(65, resposta.IndiceRisco);
        Assert.AreEqual(80, resposta.Componentes.Sinistro);
    }

    /// <summary>
    /// Garante que o turno sem proteção encerra sem publicar nada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EncerrarAsync_SemProtecao_GravaSemPublicar()
    {
        // Arrange
        Turno turno = SemearTurno(protecaoAtiva: false);

        // Act
        await _servico.EncerrarAsync(turno.Id, CancellationToken.None);

        // Assert
        Assert.IsEmpty(_publicador.Comandos);
        Assert.AreEqual(1, _unidade.VezesSalvo);
        Assert.IsFalse(turno.EstaAtivo);
    }

    /// <summary>
    /// Garante o 409 com alerta de queda em andamento, sem publicar nem gravar.
    /// </summary>
    /// <param name="estado">Estado do capacete.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoDispositivo.ImpactoDetectado)]
    [DataRow(EstadoDispositivo.Observacao)]
    [DataRow(EstadoDispositivo.Contagem)]
    [DataRow(EstadoDispositivo.SosEnviado)]
    public async Task EncerrarAsync_AlertaEmAndamento_Lanca409SemPublicar(EstadoDispositivo estado)
    {
        // Arrange
        Turno turno = SemearTurno();
        _capacete.RegistrarEstado(estado, Inicio);

        // Act
        RegraNegocioException excecao = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.EncerrarAsync(turno.Id, CancellationToken.None));

        // Assert
        Assert.Contains(NomeContrato.Para(estado), excecao.Message);
        Assert.IsTrue(turno.EstaAtivo);
        AssertNadaGravadoNemPublicado();
    }

    /// <summary>
    /// Garante o 409 com ocorrência ativa do entregador, sem publicar nem gravar.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EncerrarAsync_OcorrenciaAtiva_Lanca409SemPublicar()
    {
        // Arrange
        Turno turno = SemearTurno();
        _ocorrencias.Semear(CenarioSos.AbrirOcorrencia(_entregador.Id));

        // Act
        RegraNegocioException excecao = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.EncerrarAsync(turno.Id, CancellationToken.None));

        // Assert
        Assert.Contains("ocorrência ativa", excecao.Message);
        Assert.IsTrue(turno.EstaAtivo);
        AssertNadaGravadoNemPublicado();
    }

    /// <summary>
    /// Garante que uma ocorrência já encerrada não impede o encerramento do turno.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EncerrarAsync_OcorrenciaEncerrada_Encerra()
    {
        // Arrange
        Turno turno = SemearTurno();
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia(_entregador.Id);
        ocorrencia.Encerrar(CenarioSos.Aceite);
        _ocorrencias.Semear(ocorrencia);

        // Act
        await _servico.EncerrarAsync(turno.Id, CancellationToken.None);

        // Assert
        Assert.IsFalse(turno.EstaAtivo);
        Assert.AreEqual(1, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que, com o broker fora do ar, o encerramento lança 503 e nada é gravado.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EncerrarAsync_BrokerForaDoAr_Lanca503SemGravar()
    {
        // Arrange
        Turno turno = SemearTurno();
        _publicador.Indisponivel = true;

        // Act e Assert
        await Assert.ThrowsExactlyAsync<MensageriaIndisponivelException>(
            () => _servico.EncerrarAsync(turno.Id, CancellationToken.None));
        Assert.AreEqual(0, _unidade.VezesSalvo);
        Assert.IsEmpty(_publicador.Comandos);
    }

    /// <summary>
    /// Garante o 409 do turno já encerrado e o 404 do turno inexistente.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EncerrarAsync_JaEncerradoOuInexistente_Lanca409E404()
    {
        // Arrange
        Turno turno = SemearTurno();
        turno.Encerrar(Agora);

        // Act e Assert
        await Assert.ThrowsExactlyAsync<TransicaoInvalidaException>(
            () => _servico.EncerrarAsync(turno.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.EncerrarAsync(Guid.NewGuid(), CancellationToken.None));
        AssertNadaGravadoNemPublicado();
    }

    /// <summary>
    /// Garante que o contagemTerminaEm só sai em CONTAGEM, mesmo quando a consulta o encontra.
    /// </summary>
    /// <param name="estado">Estado do capacete.</param>
    /// <param name="sai">Se o campo deve sair preenchido.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoDispositivo.Contagem, true)]
    [DataRow(EstadoDispositivo.Observacao, false)]
    [DataRow(EstadoDispositivo.SosEnviado, false)]
    [DataRow(EstadoDispositivo.Armado, false)]
    public async Task ObterAlertaAsync_ContagemTerminaEm_SoEmContagem(EstadoDispositivo estado, bool sai)
    {
        // Arrange
        Turno turno = SemearTurno();
        DateTime fimContagem = Agora.AddSeconds(10);
        _turnos.DefinirDadosAlerta(_entregador.Id, new DadosAlertaTurno(estado, 37.1, fimContagem, null));

        // Act
        AlertaTurnoResposta alerta = await _servico.ObterAlertaAsync(turno.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(estado, alerta.EstadoCapacete);
        Assert.AreEqual(sai ? fimContagem : null, alerta.ContagemTerminaEm);
        Assert.AreEqual(37.1, alerta.IndiceCalorC);
    }

    /// <summary>
    /// Garante que um contagemTerminaEm 3 horas antes da hora do servidor (relógio atrasado do Wokwi) continua sendo devolvido.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAlertaAsync_ContagemNoRelogioAtrasado_DevolveSemComparar()
    {
        // Arrange
        Turno turno = SemearTurno();
        DateTime atrasado = Agora.AddHours(-3);
        _turnos.DefinirDadosAlerta(_entregador.Id, new DadosAlertaTurno(EstadoDispositivo.Contagem, null, atrasado, null));

        // Act
        AlertaTurnoResposta alerta = await _servico.ObterAlertaAsync(turno.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(atrasado, alerta.ContagemTerminaEm);
        Assert.IsNull(alerta.IndiceCalorC);
    }

    /// <summary>
    /// Garante que o ocorrenciaId só sai quando a consulta encontra ocorrência ativa.
    /// </summary>
    /// <param name="comOcorrencia">Se há ocorrência ativa.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ObterAlertaAsync_OcorrenciaId_SoComOcorrenciaAtiva(bool comOcorrencia)
    {
        // Arrange
        Turno turno = SemearTurno();
        Guid? ocorrenciaId = comOcorrencia ? Guid.NewGuid() : null;
        _turnos.DefinirDadosAlerta(_entregador.Id, new DadosAlertaTurno(EstadoDispositivo.SosEnviado, 39.8, null, ocorrenciaId));

        // Act
        AlertaTurnoResposta alerta = await _servico.ObterAlertaAsync(turno.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(ocorrenciaId, alerta.OcorrenciaId);
    }

    /// <summary>
    /// Garante o 409 do alerta sem capacete e o 404 do turno inexistente.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAlertaAsync_SemCapaceteOuInexistente_Lanca409E404()
    {
        // Arrange: sem DefinirDadosAlerta, a consulta não encontra capacete.
        Turno turno = SemearTurno();

        // Act e Assert
        await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.ObterAlertaAsync(turno.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.ObterAlertaAsync(Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>
    /// Garante que o cancelamento do alerta publica CANCELAR em CONTAGEM, sem gravar nada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CancelarAlertaAsync_EmContagem_PublicaCancelar()
    {
        // Arrange
        Turno turno = SemearTurno();
        _capacete.RegistrarEstado(EstadoDispositivo.Contagem, Inicio);

        // Act
        await _servico.CancelarAlertaAsync(turno.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(new ComandoPublicadoFake("HC-0001", TipoComandoMqtt.Cancelar), _publicador.Comandos.Single());
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 409 fora de CONTAGEM, sem publicar.
    /// </summary>
    /// <param name="estado">Estado do capacete.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoDispositivo.Armado)]
    [DataRow(EstadoDispositivo.Observacao)]
    [DataRow(EstadoDispositivo.SosEnviado)]
    [DataRow(EstadoDispositivo.Cancelado)]
    public async Task CancelarAlertaAsync_ForaDaContagem_Lanca409SemPublicar(EstadoDispositivo estado)
    {
        // Arrange
        Turno turno = SemearTurno();
        _capacete.RegistrarEstado(estado, Inicio);

        // Act e Assert
        await Assert.ThrowsExactlyAsync<TransicaoInvalidaException>(
            () => _servico.CancelarAlertaAsync(turno.Id, CancellationToken.None));
        Assert.IsEmpty(_publicador.Comandos);
    }

    /// <summary>
    /// Garante o 409 do turno encerrado e o do entregador sem capacete, e o 404 do turno inexistente.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CancelarAlertaAsync_TurnoEncerradoSemCapaceteOuInexistente_Lanca409E404()
    {
        // Arrange
        _capacete.RegistrarEstado(EstadoDispositivo.Contagem, Inicio);
        Turno encerrado = SemearTurno();
        encerrado.Encerrar(Agora);
        Entregador semCapacete = SemearEntregadorSemCapacete();
        Turno doSemCapacete = SemearTurno(semCapacete.Id, protecaoAtiva: false);

        // Act e Assert
        await Assert.ThrowsExactlyAsync<TransicaoInvalidaException>(
            () => _servico.CancelarAlertaAsync(encerrado.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.CancelarAlertaAsync(doSemCapacete.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.CancelarAlertaAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.IsEmpty(_publicador.Comandos);
    }

    private IniciarTurnoRequisicao Requisicao(
        Guid? entregadorId = null,
        DateTime? inicio = null,
        DateTime? fimPrevisto = null,
        bool protecaoAtiva = true)
    {
        return new IniciarTurnoRequisicao
        {
            EntregadorId = entregadorId ?? _entregador.Id,
            Inicio = inicio ?? Inicio,
            FimPrevisto = fimPrevisto ?? FimPrevisto,
            ProtecaoAtiva = protecaoAtiva,
        };
    }

    private Turno SemearTurno(Guid? entregadorId = null, bool protecaoAtiva = true)
    {
        var turno = new Turno(entregadorId ?? _entregador.Id, Inicio, FimPrevisto, protecaoAtiva);
        turno.AplicarRisco(65, FaixaRisco.Alto, 80, 70, 30, "Risco alto: explicação simulada.");
        _turnos.Semear(turno);
        return turno;
    }

    private Entregador SemearEntregadorSemCapacete()
    {
        var entregador = new Entregador("Outro Entregador", "52998224725", "98765432100", "11998765432", Inicio);
        _entregadores.Semear(entregador);
        return entregador;
    }

    private void AssertNadaGravadoNemPublicado()
    {
        Assert.AreEqual(0, _unidade.VezesSalvo);
        Assert.IsEmpty(_publicador.Comandos);
    }

    private void AssertNenhumTurnoCriado()
    {
        AssertNadaGravadoNemPublicado();
        Assert.IsEmpty(_turnos.Turnos);
    }
}
