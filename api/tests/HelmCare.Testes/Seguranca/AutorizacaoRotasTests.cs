using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Dispositivos;
using HelmCare.Api.Contratos.Turnos;
using HelmCare.Api.Contratos.Voluntarios;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Cadastro;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Ocorrencias;
using HelmCare.Api.Servicos.Risco;
using HelmCare.Api.Servicos.Turnos;
using HelmCare.Api.Servicos.Voluntarios;
using HelmCare.Dominio.Entidades;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Seguranca;

/// <summary>
/// Testes do quadro "quem pode o quê" da seção 7 do contrato (1.15), com os serviços reais e o
/// <see cref="AutorizacaoRecursos"/> real. Em cada grupo: o dono passa, outro entregador recebe 403, o
/// papel errado (socorrista) recebe 403, o recurso inexistente dá 404 (antes do 403) e sem token, 401.
/// </summary>
/// <remarks>
/// Cenário: A é o dono (com o voluntário VA, perto do local da queda); B é outro entregador, também
/// voluntário (VB); C é a vítima de uma ocorrência em <c>SOS_CAMADA_1</c>. D é um entregador sem perfil
/// de voluntário.
/// </remarks>
[TestClass]
public sealed class AutorizacaoRotasTests
{
    private readonly FakeEntregadorRepositorio _entregadores = new();
    private readonly FakeVoluntarioRepositorio _voluntarios = new();
    private readonly FakeDispositivoRepositorio _dispositivos = new();
    private readonly FakeTurnoRepositorio _turnos = new();
    private readonly FakeOcorrenciaRepositorio _ocorrencias = new();
    private readonly FakeUnidadeDeTrabalho _unidade = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(CenarioSos.Aceite));
    private readonly Entregador _a = Entregador("11144477735", "11111111111", "11911111111");
    private readonly Entregador _b = Entregador("52998224725", "22222222222", "11922222222");
    private readonly Entregador _c = Entregador("39053344705", "33333333333", "11933333333");
    private readonly Entregador _d = Entregador("15350946056", "44444444444", "11944444444");
    private readonly Voluntario _va;
    private readonly Voluntario _vb;
    private readonly Turno _turnoDeA;
    private readonly Ocorrencia _ocorrenciaDeC;

    /// <summary>
    /// Monta o cenário comum a todos os grupos.
    /// </summary>
    public AutorizacaoRotasTests()
    {
        foreach (Entregador entregador in new[] { _a, _b, _c, _d })
        {
            _entregadores.Semear(entregador);
        }

        _va = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima, _a.Id);
        _vb = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima, _b.Id);
        _voluntarios.Semear(_va);
        _voluntarios.Semear(_vb);

        _turnoDeA = new Turno(_a.Id, CenarioSos.Queda.AddHours(-1), CenarioSos.Queda.AddHours(3), protecaoAtiva: false);
        _turnos.Semear(_turnoDeA);

        _ocorrenciaDeC = CenarioSos.AbrirOcorrencia(_c.Id);
        _ocorrencias.Semear(_ocorrenciaDeC);
    }

    /// <summary>
    /// Rotas do próprio entregador (<c>GET /entregadores/{id}</c>, ficha, telefone e contatos).
    /// </summary>
    /// <param name="quem">Quem pede.</param>
    /// <param name="esperado">Status esperado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("dono", 200)]
    [DataRow("outro", 403)]
    [DataRow("socorrista", 403)]
    [DataRow("inexistente", 404)]
    [DataRow("anonimo", 401)]
    public async Task RotasDoEntregador_Quadro(string quem, int esperado)
    {
        // Arrange
        EntregadorServico servico = new(_entregadores, _unidade, new FakeContasIdentity(), Autorizacao(quem), _relogio);
        Guid id = quem == "inexistente" ? Guid.NewGuid() : _a.Id;

        // Act
        int status = await StatusAsync(() => servico.ObterAsync(id, CancellationToken.None));

        // Assert
        Assert.AreEqual(esperado, status);
    }

    /// <summary>
    /// Rotas com o <c>entregadorId</c> no corpo (<c>POST /dispositivos</c>, <c>/voluntarios</c> e <c>/turnos</c>).
    /// </summary>
    /// <param name="rota">Rota testada.</param>
    /// <param name="quem">Quem pede.</param>
    /// <param name="esperado">Status esperado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("dispositivos", "dono", 200)]
    [DataRow("dispositivos", "outro", 403)]
    [DataRow("dispositivos", "socorrista", 403)]
    [DataRow("dispositivos", "inexistente", 404)]
    [DataRow("voluntarios", "dono", 200)]
    [DataRow("voluntarios", "outro", 403)]
    [DataRow("voluntarios", "socorrista", 403)]
    [DataRow("voluntarios", "inexistente", 404)]
    [DataRow("turnos", "dono", 200)]
    [DataRow("turnos", "outro", 403)]
    [DataRow("turnos", "socorrista", 403)]
    [DataRow("turnos", "inexistente", 404)]
    public async Task RotasComEntregadorNoCorpo_Quadro(string rota, string quem, int esperado)
    {
        // Arrange: D (sem voluntário, capacete nem turno) é o dono; o "outro" é o B.
        Guid entregadorId = quem == "inexistente" ? Guid.NewGuid() : _d.Id;
        IAutorizacaoRecursos autorizacao = Autorizacao(quem, dono: _d.Id);

        Func<Task> acao = rota switch
        {
            "dispositivos" => () => new DispositivoServico(_dispositivos, _entregadores, _unidade, autorizacao)
                .VincularAsync(new VincularDispositivoRequisicao { Codigo = "HC-0009", EntregadorId = entregadorId }, CancellationToken.None),
            "voluntarios" => () => new VoluntarioServico(_voluntarios, _entregadores, _unidade, autorizacao, new FakeLogger<VoluntarioServico>())
                .CadastrarAsync(new CadastrarVoluntarioRequisicao { EntregadorId = entregadorId, Tipo = "ENTREGADOR" }, CancellationToken.None),
            _ => () => Turnos(autorizacao).IniciarAsync(
                new IniciarTurnoRequisicao
                {
                    EntregadorId = entregadorId,
                    Inicio = CenarioSos.Queda,
                    FimPrevisto = CenarioSos.Queda.AddHours(4),
                    ProtecaoAtiva = false,
                },
                CancellationToken.None),
        };

        // Act
        int status = await StatusAsync(acao);

        // Assert
        Assert.AreEqual(esperado, status);
    }

    /// <summary>
    /// Rotas do turno (<c>/turnos/{id}/...</c>): só o dono do turno.
    /// </summary>
    /// <param name="quem">Quem pede.</param>
    /// <param name="esperado">Status esperado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("dono", 200)]
    [DataRow("outro", 403)]
    [DataRow("socorrista", 403)]
    [DataRow("inexistente", 404)]
    [DataRow("anonimo", 401)]
    public async Task RotasDoTurno_Quadro(string quem, int esperado)
    {
        // Arrange
        TurnoServico servico = Turnos(Autorizacao(quem));
        Guid id = quem == "inexistente" ? Guid.NewGuid() : _turnoDeA.Id;

        // Act
        int status = await StatusAsync(() => servico.ObterAlertaAsync(id, CancellationToken.None));

        // Assert
        Assert.AreEqual(esperado, status);
    }

    /// <summary>
    /// Rotas do voluntário (disponibilidade, chamados próximos e aceite): o <c>voluntarioId</c> precisa
    /// ser o do token.
    /// </summary>
    /// <param name="rota">Rota testada.</param>
    /// <param name="quem">Quem pede.</param>
    /// <param name="esperado">Status esperado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("disponibilidade", "dono", 200)]
    [DataRow("disponibilidade", "outro", 403)]
    [DataRow("disponibilidade", "socorrista", 403)]
    [DataRow("disponibilidade", "inexistente", 404)]
    [DataRow("proximas", "dono", 200)]
    [DataRow("proximas", "outro", 403)]
    [DataRow("proximas", "socorrista", 403)]
    [DataRow("proximas", "inexistente", 404)]
    [DataRow("aceite", "dono", 200)]
    [DataRow("aceite", "outro", 403)]
    [DataRow("aceite", "socorrista", 403)]
    [DataRow("aceite", "inexistente", 404)]
    public async Task RotasDoVoluntario_Quadro(string rota, string quem, int esperado)
    {
        // Arrange
        IAutorizacaoRecursos autorizacao = Autorizacao(quem);
        Guid voluntarioId = quem == "inexistente" ? Guid.NewGuid() : _va.Id;

        Func<Task> acao = rota switch
        {
            "disponibilidade" => () => new VoluntarioServico(_voluntarios, _entregadores, _unidade, autorizacao, new FakeLogger<VoluntarioServico>())
                .AtualizarDisponibilidadeAsync(voluntarioId, new DisponibilidadeVoluntarioRequisicao { Disponivel = false }, CancellationToken.None),
            "proximas" => () => Consulta(autorizacao).ListarProximasAsync(voluntarioId, CancellationToken.None),
            _ => () => Atendimento(autorizacao).AceitarAsync(_ocorrenciaDeC.Id, voluntarioId, CancellationToken.None),
        };

        // Act
        int status = await StatusAsync(acao);

        // Assert
        Assert.AreEqual(esperado, status);
    }

    /// <summary>
    /// <c>GET /ocorrencias/{id}?voluntarioId=</c>: a vítima ou qualquer voluntário; o voluntarioId, se
    /// vier, precisa ser o do token.
    /// </summary>
    /// <param name="quem">Quem pede.</param>
    /// <param name="voluntario">Voluntário informado na consulta (<c>va</c>, <c>vb</c> ou vazio).</param>
    /// <param name="esperado">Status esperado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("vitima", "", 200)]
    [DataRow("dono", "", 200)]
    [DataRow("dono", "va", 200)]
    [DataRow("dono", "vb", 403)]
    [DataRow("semVoluntario", "", 403)]
    [DataRow("socorrista", "", 403)]
    [DataRow("inexistente", "", 404)]
    [DataRow("anonimo", "", 401)]
    public async Task LeituraDaOcorrencia_Quadro(string quem, string voluntario, int esperado)
    {
        // Arrange
        OcorrenciaServico servico = Consulta(Autorizacao(quem));
        Guid id = quem == "inexistente" ? Guid.NewGuid() : _ocorrenciaDeC.Id;
        Guid? voluntarioId = voluntario switch
        {
            "va" => _va.Id,
            "vb" => _vb.Id,
            _ => null,
        };

        // Act
        int status = await StatusAsync(() => servico.ObterAsync(id, voluntarioId, CancellationToken.None));

        // Assert
        Assert.AreEqual(esperado, status);
    }

    /// <summary>
    /// <c>POST /ocorrencias/{id}/cancelar</c>: só a vítima.
    /// </summary>
    /// <param name="quem">Quem pede.</param>
    /// <param name="esperado">Status esperado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("vitima", 200)]
    [DataRow("dono", 403)]
    [DataRow("socorrista", 403)]
    [DataRow("inexistente", 404)]
    public async Task Cancelar_Quadro(string quem, int esperado)
    {
        // Arrange
        Guid id = quem == "inexistente" ? Guid.NewGuid() : _ocorrenciaDeC.Id;

        // Act
        int status = await StatusAsync(() => Atendimento(Autorizacao(quem)).CancelarAsync(id, CancellationToken.None));

        // Assert
        Assert.AreEqual(esperado, status);
    }

    /// <summary>
    /// <c>POST /ocorrencias/{id}/encerrar</c>: a vítima ou o voluntário que aceitou (A, pelo VA).
    /// </summary>
    /// <param name="quem">Quem pede.</param>
    /// <param name="esperado">Status esperado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("vitima", 200)]
    [DataRow("dono", 200)]
    [DataRow("outro", 403)]
    [DataRow("socorrista", 403)]
    [DataRow("inexistente", 404)]
    public async Task Encerrar_Quadro(string quem, int esperado)
    {
        // Arrange: o VA (de A) aceitou a ocorrência de C.
        _ocorrenciaDeC.Aceitar(_va, CenarioSos.Aceite, CenarioSos.RaioCamada1Km, CenarioSos.TrechoCapacetePadrao);
        Guid id = quem == "inexistente" ? Guid.NewGuid() : _ocorrenciaDeC.Id;

        // Act
        int status = await StatusAsync(() => Atendimento(Autorizacao(quem)).EncerrarAsync(id, CancellationToken.None));

        // Assert
        Assert.AreEqual(esperado, status);
    }

    // Converte o resultado no status que o TratadorExcecoes responderia.
    private static async Task<int> StatusAsync(Func<Task> acao)
    {
        try
        {
            await acao();
            return 200;
        }
        catch (NaoAutenticadoException)
        {
            return 401;
        }
        catch (AcessoNegadoException)
        {
            return 403;
        }
        catch (RecursoNaoEncontradoException)
        {
            return 404;
        }
    }

    private static Entregador Entregador(string cpf, string cnh, string telefone)
    {
        return new Entregador("Entregador Simulado", cpf, cnh, telefone, CenarioSos.Queda);
    }

    // "dono" é A (ou o dono informado); "inexistente" usa o mesmo token do dono, com o recurso trocado.
    private AutorizacaoRecursos Autorizacao(string quem, Guid? dono = null)
    {
        FakeUsuarioAtual usuario = quem switch
        {
            "dono" or "inexistente" => FakeUsuarioAtual.Entregador(dono ?? _a.Id),
            "outro" => FakeUsuarioAtual.Entregador(_b.Id),
            "vitima" => FakeUsuarioAtual.Entregador(_c.Id),
            "semVoluntario" => FakeUsuarioAtual.Entregador(_d.Id),
            "socorrista" => FakeUsuarioAtual.Socorrista(),
            _ => FakeUsuarioAtual.Anonimo(),
        };

        return new AutorizacaoRecursos(usuario, _voluntarios);
    }

    private TurnoServico Turnos(IAutorizacaoRecursos autorizacao)
    {
        return new TurnoServico(
            _turnos,
            _entregadores,
            _dispositivos,
            _ocorrencias,
            new PrevisaoTempoSimulada(),
            new ServicoRiscoSimulado(),
            new FakePublicadorComandos(),
            _unidade,
            autorizacao,
            _relogio);
    }

    private OcorrenciaServico Consulta(IAutorizacaoRecursos autorizacao)
    {
        return new OcorrenciaServico(_ocorrencias, _voluntarios, autorizacao, _relogio, Options.Create(new OpcoesHelmCare()));
    }

    private AtendimentoOcorrenciaServico Atendimento(IAutorizacaoRecursos autorizacao)
    {
        var comandos = new PublicadorComandosOcorrencia(new FakePublicadorComandos(), _dispositivos, new FakeLogger<PublicadorComandosOcorrencia>());

        return new AtendimentoOcorrenciaServico(
            _ocorrencias,
            _voluntarios,
            _unidade,
            comandos,
            Consulta(autorizacao),
            autorizacao,
            _relogio,
            Options.Create(new OpcoesHelmCare()),
            new FakeLogger<AtendimentoOcorrenciaServico>());
    }
}
