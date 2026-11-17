using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Identidade;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="AutenticacaoEntregadorServico"/>: login, renovação do token e sessão, com contas
/// e tokens falsos (sem Identity, banco ou rede).
/// </summary>
[TestClass]
public sealed class AutenticacaoEntregadorServicoTests
{
    private const string Email = "entregador@exemplo.com";
    private const string Senha = "Senha123";

    private readonly FakeContasIdentity _contas = new();
    private readonly FakeEmissorTokens _tokens = new();
    private readonly FakeEntregadorRepositorio _entregadores = new();
    private readonly FakeVoluntarioRepositorio _voluntarios = new();
    private readonly FakeDispositivoRepositorio _dispositivos = new();
    private readonly FakeTurnoRepositorio _turnos = new();
    private readonly FakeOcorrenciaRepositorio _ocorrencias = new();
    private readonly FakeLogger<AutenticacaoEntregadorServico> _logger = new();
    private readonly Entregador _entregador = CenarioSos.NovoEntregador();
    private readonly string _usuarioId;

    /// <summary>
    /// Semeia um entregador com conta (papel ENTREGADOR) e nada mais: sem voluntário, capacete,
    /// turno ou ocorrência.
    /// </summary>
    public AutenticacaoEntregadorServicoTests()
    {
        _usuarioId = _contas.Semear(Email, Senha, Papeis.Entregador);
        _entregador.VincularUsuario(_usuarioId);
        _entregadores.Semear(_entregador);
    }

    /// <summary>
    /// Garante o login com os campos opcionais nulos: tokens, Bearer, id e nome do entregador.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EntrarAsync_SemVinculos_DevolveTokensECamposNulos()
    {
        // Act
        LoginEntregadorResposta resposta = await Servico(FakeUsuarioAtual.Anonimo()).EntrarAsync(Login(Email, Senha), CancellationToken.None);

        // Assert
        Assert.AreEqual("Bearer", resposta.TokenType);
        Assert.AreEqual($"acesso-{_usuarioId}", resposta.AccessToken);
        Assert.AreEqual($"renovacao-{_usuarioId}", resposta.RefreshToken);
        Assert.AreEqual(FakeEmissorTokens.ValidadeSegundos, resposta.ExpiresIn);
        Assert.AreEqual(_entregador.Id, resposta.EntregadorId);
        Assert.AreEqual(_entregador.Nome, resposta.Nome);
        Assert.IsNull(resposta.VoluntarioId);
        Assert.IsNull(resposta.CodigoDispositivo);
        Assert.IsNull(resposta.TurnoAtivoId);
        Assert.IsNull(resposta.OcorrenciaAtivaId);
    }

    /// <summary>
    /// Garante o login com os campos preenchidos: voluntário, capacete, turno ativo e a ocorrência da
    /// vítima ainda aberta. O e-mail é comparado sem diferenciar maiúsculas.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EntrarAsync_ComVinculos_DevolveOsIdsDaSessao()
    {
        // Arrange
        var voluntario = new Voluntario(_entregador.Id, TipoVoluntario.Entregador);
        _voluntarios.Semear(voluntario);
        _dispositivos.Semear(new Dispositivo("HC-0001", _entregador.Id));
        var turno = new Turno(_entregador.Id, CenarioSos.Queda.AddHours(-1), CenarioSos.Queda.AddHours(3), protecaoAtiva: true);
        _turnos.Semear(turno);
        Ocorrencia ativa = CenarioSos.AbrirOcorrencia(_entregador.Id);
        _ocorrencias.Semear(ativa);

        // Act
        LoginEntregadorResposta resposta = await Servico(FakeUsuarioAtual.Anonimo())
            .EntrarAsync(Login("ENTREGADOR@Exemplo.com", Senha), CancellationToken.None);

        // Assert
        Assert.AreEqual(voluntario.Id, resposta.VoluntarioId);
        Assert.AreEqual("HC-0001", resposta.CodigoDispositivo);
        Assert.AreEqual(turno.Id, resposta.TurnoAtivoId);
        Assert.AreEqual(ativa.Id, resposta.OcorrenciaAtivaId);
    }

    /// <summary>
    /// Garante que uma ocorrência já terminada não vira <c>ocorrenciaAtivaId</c>.
    /// </summary>
    /// <param name="estado">Estado final.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoOcorrencia.Encerrada)]
    [DataRow(EstadoOcorrencia.Cancelada)]
    public async Task EntrarAsync_OcorrenciaTerminada_OcorrenciaAtivaNula(EstadoOcorrencia estado)
    {
        // Arrange
        Ocorrencia terminada = CenarioSos.AbrirOcorrencia(_entregador.Id);
        if (estado == EstadoOcorrencia.Encerrada)
        {
            terminada.Encerrar(CenarioSos.Aceite);
        }
        else
        {
            terminada.Cancelar(CenarioSos.Aceite);
        }

        _ocorrencias.Semear(terminada);

        // Act
        LoginEntregadorResposta resposta = await Servico(FakeUsuarioAtual.Anonimo()).EntrarAsync(Login(Email, Senha), CancellationToken.None);

        // Assert
        Assert.IsNull(resposta.OcorrenciaAtivaId);
    }

    /// <summary>
    /// Garante o mesmo 401, com o mesmo texto e sem token, para senha errada, e-mail desconhecido, conta
    /// bloqueada (mesmo com a senha certa) e conta de socorrista.
    /// </summary>
    /// <param name="caso">Situação do login.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("senhaErrada")]
    [DataRow("emailDesconhecido")]
    [DataRow("bloqueada")]
    [DataRow("socorrista")]
    public async Task EntrarAsync_Recusado_MesmoTextoNosQuatroCasos(string caso)
    {
        // Arrange
        _contas.Semear("bloqueado@exemplo.com", Senha, Papeis.Entregador, bloqueada: true);
        _contas.Semear("socorrista@exemplo.com", Senha, Papeis.Socorrista);
        LoginRequisicao login = caso switch
        {
            "senhaErrada" => Login(Email, "Errada123"),
            "emailDesconhecido" => Login("ninguem@exemplo.com", Senha),
            "bloqueada" => Login("bloqueado@exemplo.com", Senha),
            _ => Login("socorrista@exemplo.com", Senha),
        };

        // Act
        NaoAutenticadoException excecao = await Assert.ThrowsExactlyAsync<NaoAutenticadoException>(
            () => Servico(FakeUsuarioAtual.Anonimo()).EntrarAsync(login, CancellationToken.None));

        // Assert
        Assert.AreEqual(AutenticacaoEntregadorServico.MensagemLoginInvalido, excecao.Message);
    }

    /// <summary>
    /// Garante o log do login: só o id do perfil e o resultado, nunca o e-mail ou a senha; e-mail
    /// desconhecido não deixa nada do e-mail.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EntrarAsync_Log_SoIdEResultado()
    {
        // Arrange
        AutenticacaoEntregadorServico servico = Servico(FakeUsuarioAtual.Anonimo());

        // Act
        await servico.EntrarAsync(Login(Email, Senha), CancellationToken.None);
        await Assert.ThrowsExactlyAsync<NaoAutenticadoException>(
            () => servico.EntrarAsync(Login(Email, "Errada123"), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NaoAutenticadoException>(
            () => servico.EntrarAsync(Login("desconhecido@exemplo.com", "Errada123"), CancellationToken.None));

        // Assert
        string[] mensagens = _logger.Registros.Select(r => r.Mensagem).ToArray();
        Assert.AreEqual($"Login do entregador {_entregador.Id}: sucesso.", mensagens[0]);
        Assert.AreEqual($"Login recusado: falha (entregador {_entregador.Id}).", mensagens[1]);
        Assert.AreEqual("Login recusado: falha (entregador (null)).", mensagens[2]);
        Assert.IsFalse(mensagens.Any(m => m.Contains("exemplo", StringComparison.Ordinal) || m.Contains("Senha", StringComparison.Ordinal)));
        Assert.AreEqual(LogLevel.Warning, _logger.Registros[1].Nivel);
    }

    /// <summary>
    /// Garante a renovação válida: tokens novos e a sessão atualizada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task RenovarAsync_TokenValido_DevolveOMesmoCorpoDoLogin()
    {
        // Arrange
        AutenticacaoEntregadorServico servico = Servico(FakeUsuarioAtual.Anonimo());
        LoginEntregadorResposta login = await servico.EntrarAsync(Login(Email, Senha), CancellationToken.None);
        _dispositivos.Semear(new Dispositivo("HC-0001", _entregador.Id));

        // Act
        LoginEntregadorResposta renovado = await servico.RenovarAsync(
            new RenovarTokenRequisicao { RefreshToken = login.RefreshToken },
            CancellationToken.None);

        // Assert
        Assert.AreEqual(_entregador.Id, renovado.EntregadorId);
        Assert.AreEqual("HC-0001", renovado.CodigoDispositivo);
        Assert.IsFalse(string.IsNullOrEmpty(renovado.AccessToken));
    }

    /// <summary>
    /// Garante o 401 da renovação com token desconhecido, vencido (revogado) ou de uma conta sem perfil de entregador.
    /// </summary>
    /// <param name="caso">Situação do token.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("desconhecido")]
    [DataRow("vencido")]
    [DataRow("socorrista")]
    public async Task RenovarAsync_TokenInvalido_Lanca401(string caso)
    {
        // Arrange
        string socorrista = _contas.Semear("socorrista@exemplo.com", Senha, Papeis.Socorrista);
        TokensEmitidos doSocorrista = await _tokens.EmitirAsync(socorrista, CancellationToken.None);
        TokensEmitidos doEntregador = await _tokens.EmitirAsync(_usuarioId, CancellationToken.None);
        _tokens.Revogar(doEntregador.RefreshToken);
        string token = caso switch
        {
            "desconhecido" => "qualquer-coisa",
            "vencido" => doEntregador.RefreshToken,
            _ => doSocorrista.RefreshToken,
        };

        // Act
        NaoAutenticadoException excecao = await Assert.ThrowsExactlyAsync<NaoAutenticadoException>(
            () => Servico(FakeUsuarioAtual.Anonimo()).RenovarAsync(new RenovarTokenRequisicao { RefreshToken = token }, CancellationToken.None));

        // Assert
        Assert.AreEqual(AutenticacaoEntregadorServico.MensagemRenovacaoInvalida, excecao.Message);
    }

    /// <summary>
    /// Garante a sessão do token: o corpo do login sem os campos do token.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterSessaoAsync_TokenDeEntregador_DevolveASessao()
    {
        // Arrange
        Ocorrencia ativa = CenarioSos.AbrirOcorrencia(_entregador.Id);
        _ocorrencias.Semear(ativa);

        // Act
        SessaoEntregadorResposta sessao = await Servico(FakeUsuarioAtual.Entregador(_entregador.Id)).ObterSessaoAsync(CancellationToken.None);

        // Assert
        Assert.AreEqual(_entregador.Id, sessao.EntregadorId);
        Assert.AreEqual(_entregador.Nome, sessao.Nome);
        Assert.AreEqual(ativa.Id, sessao.OcorrenciaAtivaId);
        Assert.IsNull(sessao.TurnoAtivoId);
    }

    /// <summary>
    /// Garante a sessão sem token (401) e com token de socorrista (403).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterSessaoAsync_SemTokenOuSocorrista_401E403()
    {
        // Act e Assert
        await Assert.ThrowsExactlyAsync<NaoAutenticadoException>(
            () => Servico(FakeUsuarioAtual.Anonimo()).ObterSessaoAsync(CancellationToken.None));
        await Assert.ThrowsExactlyAsync<AcessoNegadoException>(
            () => Servico(FakeUsuarioAtual.Socorrista()).ObterSessaoAsync(CancellationToken.None));
    }

    private static LoginRequisicao Login(string email, string senha)
    {
        return new LoginRequisicao { Email = email, Senha = senha };
    }

    private AutenticacaoEntregadorServico Servico(FakeUsuarioAtual usuario)
    {
        return new AutenticacaoEntregadorServico(
            _contas,
            _tokens,
            _entregadores,
            _voluntarios,
            _dispositivos,
            _turnos,
            _ocorrencias,
            new AutorizacaoRecursos(usuario, _voluntarios),
            _logger);
    }
}
