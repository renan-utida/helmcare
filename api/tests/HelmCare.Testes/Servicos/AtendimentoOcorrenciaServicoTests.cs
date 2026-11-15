using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Ocorrencias;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Ocorrencias;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Dominio.ObjetosDeValor;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="AtendimentoOcorrenciaServico"/>: aceite (com a nova tentativa no conflito),
/// cancelamento e encerramento, e os comandos publicados sem bloquear.
/// </summary>
[TestClass]
public sealed class AtendimentoOcorrenciaServicoTests
{
    private const string Codigo = "HC-0001";

    private readonly FakeOcorrenciaRepositorio _ocorrencias = new();
    private readonly FakeVoluntarioRepositorio _voluntarios = new();
    private readonly FakeDispositivoRepositorio _dispositivos = new();
    private readonly FakeUnidadeDeTrabalho _unidade = new();
    private readonly FakePublicadorComandos _publicador = new();
    private readonly FakeLogger<PublicadorComandosOcorrencia> _logComandos = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(CenarioSos.Aceite));
    private readonly Guid _vitimaId = Guid.NewGuid();
    private readonly AtendimentoOcorrenciaServico _servico;

    /// <summary>
    /// Monta o serviço com o relógio no instante do aceite de referência (38 s depois do SOS) e o
    /// capacete HC-0001 vinculado à vítima.
    /// </summary>
    public AtendimentoOcorrenciaServicoTests()
    {
        _dispositivos.Semear(new Dispositivo(Codigo, _vitimaId));
        IOptions<OpcoesHelmCare> opcoes = Options.Create(new OpcoesHelmCare());
        var consulta = new OcorrenciaServico(_ocorrencias, _voluntarios, _relogio, opcoes);
        var comandos = new PublicadorComandosOcorrencia(_publicador, _dispositivos, _logComandos);
        _servico = new AtendimentoOcorrenciaServico(
            _ocorrencias,
            _voluntarios,
            _unidade,
            comandos,
            consulta,
            _relogio,
            opcoes,
            new FakeLogger<AtendimentoOcorrenciaServico>());
    }

    /// <summary>
    /// Garante o aceite: localização exata para quem aceitou, cronômetro parado igual ao indicador
    /// (25 s do capacete + 38 s do servidor = 63) e SOCORRO_A_CAMINHO publicado depois de gravar.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AceitarAsync_VoluntarioProximo_ExataCronometroParadoESocorroPublicado()
    {
        // Arrange
        Ocorrencia ocorrencia = SemearOcorrencia();
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoProxima);

        // Act
        OcorrenciaResposta resposta = await _servico.AceitarAsync(ocorrencia.Id, voluntario.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.SocorroACaminho, resposta.Estado);
        Assert.AreEqual(CenarioSos.Local.Latitude, resposta.Latitude);
        Assert.AreEqual(CenarioSos.Local.Longitude, resposta.Longitude);
        Assert.IsTrue(resposta.LocalizacaoExata);
        Assert.AreEqual(CenarioSos.Aceite, resposta.AceitaEm);
        Assert.AreEqual(63, resposta.TempoAteAceiteSegundos);
        Assert.IsTrue(resposta.Cronometro.Parado);
        Assert.AreEqual(resposta.TempoAteAceiteSegundos, resposta.Cronometro.SegundosDesdeQueda);
        Assert.AreEqual(1, _unidade.VezesSalvo);
        Assert.AreEqual(new ComandoPublicadoFake(Codigo, TipoComandoMqtt.SocorroACaminho), _publicador.Comandos.Single());
        Assert.AreEqual(voluntario.Id, ocorrencia.Aceites.Single().VoluntarioId);
    }

    /// <summary>
    /// Garante que, na camada 1, o voluntário fora do raio recebe 409 e nada muda nem é publicado.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AceitarAsync_Camada1ForaDoRaio_Lanca409SemMudar()
    {
        // Arrange
        Ocorrencia ocorrencia = SemearOcorrencia();
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoDistante);

        // Act
        await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.AceitarAsync(ocorrencia.Id, voluntario.Id, CancellationToken.None));

        // Assert
        AssertSemMudanca(ocorrencia, EstadoOcorrencia.SosCamada1);
    }

    /// <summary>
    /// Garante que, na camada 2, o voluntário de longe aceita.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AceitarAsync_Camada2VoluntarioDeLonge_Aceita()
    {
        // Arrange
        Ocorrencia ocorrencia = SemearOcorrencia();
        _relogio.SetUtcNow(new DateTimeOffset(CenarioSos.Sos.AddSeconds(70)));
        ocorrencia.EscalarParaCamada2(CenarioSos.Sos.AddSeconds(60), CenarioSos.TempoAceiteCamada1);
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoDistante);

        // Act
        OcorrenciaResposta resposta = await _servico.AceitarAsync(ocorrencia.Id, voluntario.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.SocorroACaminho, resposta.Estado);
        Assert.AreEqual(2, resposta.Camada);
        Assert.AreEqual(1, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 409 sem mudança para o voluntário indisponível e para a própria vítima.
    /// </summary>
    /// <param name="caso">Quem tenta aceitar.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("indisponivel")]
    [DataRow("vitima")]
    public async Task AceitarAsync_NaoPodeAtender_Lanca409SemMudar(string caso)
    {
        // Arrange
        Ocorrencia ocorrencia = SemearOcorrencia();
        Voluntario voluntario;
        if (caso == "vitima")
        {
            voluntario = SemearVoluntario(CenarioSos.PosicaoProxima, _vitimaId);
        }
        else
        {
            voluntario = new Voluntario(Guid.NewGuid(), TipoVoluntario.Entregador);
            _voluntarios.Semear(voluntario);
        }

        // Act
        await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.AceitarAsync(ocorrencia.Id, voluntario.Id, CancellationToken.None));

        // Assert
        AssertSemMudanca(ocorrencia, EstadoOcorrencia.SosCamada1);
    }

    /// <summary>
    /// Garante que o segundo aceite (a ocorrência já está em SOCORRO_A_CAMINHO) dá 409 e mantém o primeiro.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AceitarAsync_SegundoAceite_Lanca409()
    {
        // Arrange
        Ocorrencia ocorrencia = SemearOcorrencia();
        Voluntario primeiro = SemearVoluntario(CenarioSos.PosicaoProxima);
        Voluntario segundo = SemearVoluntario(CenarioSos.PosicaoProxima);
        await _servico.AceitarAsync(ocorrencia.Id, primeiro.Id, CancellationToken.None);

        // Act
        await Assert.ThrowsExactlyAsync<TransicaoInvalidaException>(
            () => _servico.AceitarAsync(ocorrencia.Id, segundo.Id, CancellationToken.None));

        // Assert
        Assert.AreEqual(primeiro.Id, ocorrencia.Aceites.Single().VoluntarioId);
        Assert.AreEqual(1, _unidade.VezesSalvo);
        Assert.HasCount(1, _publicador.Comandos);
    }

    /// <summary>
    /// Garante que o broker fora (ou qualquer falha da publicação) não bloqueia: 201, aceite gravado e aviso no log.
    /// </summary>
    /// <param name="falha">Tipo de falha da publicação.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("broker")]
    [DataRow("outra")]
    public async Task AceitarAsync_PublicadorFalhando_AceitaGravaEAvisa(string falha)
    {
        // Arrange
        Ocorrencia ocorrencia = SemearOcorrencia();
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoProxima);
        _publicador.Indisponivel = falha == "broker";
        _publicador.FalhaInesperada = falha == "outra";

        // Act
        OcorrenciaResposta resposta = await _servico.AceitarAsync(ocorrencia.Id, voluntario.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.SocorroACaminho, resposta.Estado);
        Assert.AreEqual(1, _unidade.VezesSalvo);
        Assert.IsEmpty(_publicador.Comandos);
        RegistroLogFake aviso = _logComandos.Registros.Single();
        Assert.AreEqual(LogLevel.Warning, aviso.Nivel);
        Assert.Contains("SOCORRO_A_CAMINHO", aviso.Mensagem);
        Assert.Contains(ocorrencia.Id.ToString(), aviso.Mensagem);
    }

    /// <summary>
    /// Garante a nova tentativa quando o conflito veio do escalonamento: descarta, relê a ocorrência
    /// (agora na camada 2, ainda aguardando) e aceita.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AceitarAsync_ConflitoDoEscalonamento_RefazEAceita()
    {
        // Arrange: a primeira leitura vê a camada 1; a releitura vê o que o escalonamento gravou.
        Guid id = Guid.NewGuid();
        Ocorrencia lida = CenarioSos.AbrirOcorrencia(_vitimaId);
        Ocorrencia relida = CenarioSos.AbrirOcorrencia(_vitimaId);
        relida.EscalarParaCamada2(CenarioSos.Sos.AddSeconds(60), CenarioSos.TempoAceiteCamada1);
        _ocorrencias.SemearVersoes(id, lida, relida);
        _relogio.SetUtcNow(new DateTimeOffset(CenarioSos.Sos.AddSeconds(60)));
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoProxima);
        _unidade.FalhaNaChamada = chamada => chamada == 1 ? new DbUpdateConcurrencyException("xmin trocado") : null;

        // Act
        OcorrenciaResposta resposta = await _servico.AceitarAsync(id, voluntario.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.SocorroACaminho, resposta.Estado);
        Assert.AreEqual(2, resposta.Camada);
        Assert.AreEqual(EstadoOcorrencia.SocorroACaminho, relida.Estado);
        Assert.AreEqual(1, _unidade.VezesDescartado);
        Assert.AreEqual(1, _unidade.VezesSalvo);
        Assert.HasCount(1, _publicador.Comandos);
    }

    /// <summary>
    /// Garante o 409 quando o conflito veio de outro aceite: a releitura já não aguarda aceite, e nada é publicado.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AceitarAsync_ConflitoPorqueOutroAceitou_Lanca409()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        _ocorrencias.SemearVersoes(id, CenarioSos.AbrirOcorrencia(_vitimaId), CenarioSos.OcorrenciaEm(EstadoOcorrencia.SocorroACaminho));
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoProxima);
        _unidade.FalhaNaChamada = chamada => chamada == 1 ? new DbUpdateConcurrencyException("xmin trocado") : null;

        // Act
        RegraNegocioException excecao = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.AceitarAsync(id, voluntario.Id, CancellationToken.None));

        // Assert
        Assert.Contains("outro voluntário aceitou antes", excecao.Message);
        Assert.AreEqual(1, _unidade.VezesDescartado);
        Assert.AreEqual(0, _unidade.VezesSalvo);
        Assert.IsEmpty(_publicador.Comandos);
    }

    /// <summary>
    /// Garante o 404 para ocorrência ou voluntário inexistente.
    /// </summary>
    /// <param name="ocorrenciaExiste">Se a ocorrência existe.</param>
    /// <param name="mensagem">Mensagem esperada.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(false, "Ocorrência não encontrada.")]
    [DataRow(true, "Voluntário não encontrado.")]
    public async Task AceitarAsync_Inexistente_Lanca404(bool ocorrenciaExiste, string mensagem)
    {
        // Arrange
        Guid ocorrenciaId = ocorrenciaExiste ? SemearOcorrencia().Id : Guid.NewGuid();

        // Act
        RecursoNaoEncontradoException excecao = await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.AceitarAsync(ocorrenciaId, Guid.NewGuid(), CancellationToken.None));

        // Assert
        Assert.AreEqual(mensagem, excecao.Message);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que cancelar em SOS_CAMADA_1 dá 200 com a aproximada, grava e publica ENCERRAR.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CancelarAsync_Camada1_CanceladaAproximadaEEncerrarPublicado()
    {
        // Arrange
        Ocorrencia ocorrencia = SemearOcorrencia();

        // Act
        OcorrenciaResposta resposta = await _servico.CancelarAsync(ocorrencia.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.Cancelada, resposta.Estado);
        Assert.AreEqual(CenarioSos.Aceite, resposta.EncerradaEm);
        Assert.AreEqual(-23.56, resposta.Latitude);
        Assert.IsFalse(resposta.LocalizacaoExata);
        Assert.IsTrue(resposta.Cronometro.Parado);
        Assert.AreEqual(1, _unidade.VezesSalvo);
        Assert.AreEqual(new ComandoPublicadoFake(Codigo, TipoComandoMqtt.Encerrar), _publicador.Comandos.Single());
    }

    /// <summary>
    /// Garante que cancelar depois do aceite dá 409 sem mudar nada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CancelarAsync_SocorroACaminho_Lanca409()
    {
        // Arrange
        Ocorrencia ocorrencia = await SemearAceitaAsync();

        // Act
        await Assert.ThrowsExactlyAsync<TransicaoInvalidaException>(
            () => _servico.CancelarAsync(ocorrencia.Id, CancellationToken.None));

        // Assert
        Assert.AreEqual(EstadoOcorrencia.SocorroACaminho, ocorrencia.Estado);
        Assert.IsNull(ocorrencia.EncerradaEm);
        Assert.AreEqual(1, _unidade.VezesSalvo);
        Assert.HasCount(1, _publicador.Comandos);
    }

    /// <summary>
    /// Garante que encerrar sem aceite deixa o indicador nulo e publica ENCERRAR.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EncerrarAsync_SemAceite_IndicadorNulo()
    {
        // Arrange
        Ocorrencia ocorrencia = SemearOcorrencia();

        // Act
        OcorrenciaResposta resposta = await _servico.EncerrarAsync(ocorrencia.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.Encerrada, resposta.Estado);
        Assert.IsNull(resposta.TempoAteAceiteSegundos);
        Assert.IsNull(resposta.AceitaEm);
        Assert.AreEqual(new ComandoPublicadoFake(Codigo, TipoComandoMqtt.Encerrar), _publicador.Comandos.Single());
    }

    /// <summary>
    /// Garante que encerrar depois do aceite dá 200 e mantém o indicador do aceite.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EncerrarAsync_DepoisDoAceite_EncerradaComIndicador()
    {
        // Arrange
        Ocorrencia ocorrencia = await SemearAceitaAsync();
        _relogio.Advance(TimeSpan.FromMinutes(10));

        // Act
        OcorrenciaResposta resposta = await _servico.EncerrarAsync(ocorrencia.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.Encerrada, resposta.Estado);
        Assert.AreEqual(63, resposta.TempoAteAceiteSegundos);
        Assert.AreEqual(63, resposta.Cronometro.SegundosDesdeQueda);
        Assert.AreEqual(TipoComandoMqtt.Encerrar, _publicador.Comandos[^1].Tipo);
    }

    /// <summary>
    /// Garante que encerrar ou cancelar uma ocorrência final dá 409 sem gravar nem publicar.
    /// </summary>
    /// <param name="estado">Estado final.</param>
    /// <param name="acao">Ação tentada.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoOcorrencia.Encerrada, "encerrar")]
    [DataRow(EstadoOcorrencia.Encerrada, "cancelar")]
    [DataRow(EstadoOcorrencia.Cancelada, "encerrar")]
    [DataRow(EstadoOcorrencia.Cancelada, "cancelar")]
    public async Task FinalizarAsync_OcorrenciaFinal_Lanca409(EstadoOcorrencia estado, string acao)
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(estado);
        _ocorrencias.Semear(ocorrencia);
        DateTime? encerradaEm = ocorrencia.EncerradaEm;

        // Act
        await Assert.ThrowsExactlyAsync<TransicaoInvalidaException>(() => acao == "encerrar"
            ? _servico.EncerrarAsync(ocorrencia.Id, CancellationToken.None)
            : _servico.CancelarAsync(ocorrencia.Id, CancellationToken.None));

        // Assert
        Assert.AreEqual(estado, ocorrencia.Estado);
        Assert.AreEqual(encerradaEm, ocorrencia.EncerradaEm);
        Assert.AreEqual(0, _unidade.VezesSalvo);
        Assert.IsEmpty(_publicador.Comandos);
    }

    /// <summary>
    /// Garante que o broker fora não bloqueia o encerrar: 200, gravado e aviso no log.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EncerrarAsync_PublicadorFalhando_EncerraEAvisa()
    {
        // Arrange
        Ocorrencia ocorrencia = SemearOcorrencia();
        _publicador.Indisponivel = true;

        // Act
        OcorrenciaResposta resposta = await _servico.EncerrarAsync(ocorrencia.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.Encerrada, resposta.Estado);
        Assert.AreEqual(1, _unidade.VezesSalvo);
        Assert.AreEqual(LogLevel.Warning, _logComandos.Registros.Single().Nivel);
    }

    /// <summary>
    /// Garante o 404 para ocorrência inexistente no cancelar e no encerrar.
    /// </summary>
    /// <param name="acao">Ação tentada.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("encerrar")]
    [DataRow("cancelar")]
    public async Task FinalizarAsync_Inexistente_Lanca404(string acao)
    {
        // Act e Assert
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(() => acao == "encerrar"
            ? _servico.EncerrarAsync(Guid.NewGuid(), CancellationToken.None)
            : _servico.CancelarAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private void AssertSemMudanca(Ocorrencia ocorrencia, EstadoOcorrencia estado)
    {
        Assert.AreEqual(estado, ocorrencia.Estado);
        Assert.IsEmpty(ocorrencia.Aceites);
        Assert.IsNull(ocorrencia.AceitaEm);
        Assert.AreEqual(0, _unidade.VezesSalvo);
        Assert.IsEmpty(_publicador.Comandos);
    }

    private Ocorrencia SemearOcorrencia()
    {
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia(_vitimaId);
        _ocorrencias.Semear(ocorrencia);
        return ocorrencia;
    }

    private async Task<Ocorrencia> SemearAceitaAsync()
    {
        Ocorrencia ocorrencia = SemearOcorrencia();
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoProxima);
        await _servico.AceitarAsync(ocorrencia.Id, voluntario.Id, CancellationToken.None);
        return ocorrencia;
    }

    private Voluntario SemearVoluntario(Coordenada posicao, Guid? entregadorId = null)
    {
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(posicao, entregadorId);
        _voluntarios.Semear(voluntario);
        return voluntario;
    }
}
