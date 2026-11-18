using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Contratos.Socorristas;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Cadastro;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Identidade;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Testes.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do login do socorrista (<see cref="AutenticacaoSocorristaServico"/>), da renovação recusada
/// para o socorrista e do cadastro de desenvolvimento (<see cref="SocorristaServico"/>).
/// </summary>
[TestClass]
public sealed class SocorristasTests
{
    private const string Email = "socorrista@exemplo.com";
    private const string Senha = "Senha123";

    private readonly FakeContasIdentity _contas = new();
    private readonly FakeEmissorTokens _tokens = new();
    private readonly FakeSocorristaRepositorio _socorristas = new();
    private readonly FakeUnidadeDeTrabalho _unidade = new();
    private readonly FakeLogger<AutenticacaoSocorristaServico> _logger = new();
    private readonly Socorrista _socorrista = new("Socorrista Simulado", "COREN-SP 000001", "SAMU Simulado");
    private readonly string _usuarioId;

    /// <summary>
    /// Semeia um socorrista verificado com conta.
    /// </summary>
    public SocorristasTests()
    {
        _usuarioId = _contas.Semear(Email, Senha, Papeis.Socorrista);
        _socorrista.Verificar();
        _socorrista.VincularUsuario(_usuarioId);
        _socorristas.Semear(_socorrista);
    }

    /// <summary>
    /// Garante o login com sucesso: tokens e os campos do socorrista, e o log só com o id.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EntrarAsync_Valido_DevolveTokensEPerfil()
    {
        // Act
        LoginSocorristaResposta resposta = await Login().EntrarAsync(new LoginRequisicao { Email = Email, Senha = Senha }, CancellationToken.None);

        // Assert
        Assert.AreEqual("Bearer", resposta.TokenType);
        Assert.AreEqual($"acesso-{_usuarioId}", resposta.AccessToken);
        Assert.AreEqual(_socorrista.Id, resposta.SocorristaId);
        Assert.AreEqual("SAMU Simulado", resposta.Organizacao);
        Assert.IsTrue(resposta.Verificado);
        Assert.AreEqual($"Login do socorrista {_socorrista.Id}: sucesso.", _logger.Registros.Single().Mensagem);
    }

    /// <summary>
    /// Garante o mesmo 401, com o mesmo texto do login do entregador, para senha errada, conta bloqueada,
    /// conta de entregador e e-mail desconhecido (que também passa pela verificação de senha das contas).
    /// </summary>
    /// <param name="caso">Situação.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("senhaErrada")]
    [DataRow("bloqueada")]
    [DataRow("entregador")]
    [DataRow("emailDesconhecido")]
    public async Task EntrarAsync_Recusado_MesmoTexto(string caso)
    {
        // Arrange
        _contas.Semear("bloqueado@exemplo.com", Senha, Papeis.Socorrista, bloqueada: true);
        _contas.Semear("entregador@exemplo.com", Senha, Papeis.Entregador);
        LoginRequisicao login = caso switch
        {
            "senhaErrada" => new LoginRequisicao { Email = Email, Senha = "Errada123" },
            "bloqueada" => new LoginRequisicao { Email = "bloqueado@exemplo.com", Senha = Senha },
            "entregador" => new LoginRequisicao { Email = "entregador@exemplo.com", Senha = Senha },
            _ => new LoginRequisicao { Email = "ninguem@exemplo.com", Senha = Senha },
        };

        // Act
        NaoAutenticadoException excecao = await Assert.ThrowsExactlyAsync<NaoAutenticadoException>(
            () => Login().EntrarAsync(login, CancellationToken.None));

        // Assert
        Assert.AreEqual(AutenticacaoEntregadorServico.MensagemLoginInvalido, excecao.Message);
        Assert.AreEqual(1, _contas.Verificacoes, "Toda tentativa passa pela mesma verificação de senha (com hash no e-mail desconhecido).");
    }

    /// <summary>
    /// Garante que o socorrista não tem renovação: o refresh dele no POST /entregadores/renovar-token dá 401.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task RenovarAsync_RefreshDeSocorrista_Lanca401()
    {
        // Arrange
        LoginSocorristaResposta login = await Login().EntrarAsync(new LoginRequisicao { Email = Email, Senha = Senha }, CancellationToken.None);
        var voluntarios = new FakeVoluntarioRepositorio();
        var renovacao = new AutenticacaoEntregadorServico(
            _contas,
            _tokens,
            new FakeEntregadorRepositorio(),
            voluntarios,
            new FakeDispositivoRepositorio(),
            new FakeTurnoRepositorio(),
            new FakeOcorrenciaRepositorio(),
            new AutorizacaoRecursos(FakeUsuarioAtual.Anonimo(), voluntarios),
            new FakeLogger<AutenticacaoEntregadorServico>());

        // Act e Assert
        await Assert.ThrowsExactlyAsync<NaoAutenticadoException>(
            () => renovacao.RenovarAsync(new RenovarTokenRequisicao { RefreshToken = login.RefreshToken }, CancellationToken.None));
    }

    /// <summary>
    /// Garante o cadastro de desenvolvimento: socorrista e conta (papel SOCORRISTA) na mesma transação,
    /// verificado conforme o pedido e o e-mail mascarado.
    /// </summary>
    /// <param name="verificado">Se nasce verificado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CriarAsync_Valido_CriaSocorristaEContaNaMesmaTransacao(bool verificado)
    {
        // Act
        SocorristaResposta resposta = await Cadastro().CriarAsync(Requisicao("novo@exemplo.com", verificado), CancellationToken.None);

        // Assert
        Socorrista criado = _socorristas.Socorristas.Single(s => s.Id == resposta.SocorristaId);
        ContaFake conta = _contas.Contas.Single(c => c.Email == "novo@exemplo.com");
        Assert.AreEqual(Papeis.Socorrista, conta.Papel);
        Assert.AreEqual(conta.UsuarioId, criado.UsuarioId);
        Assert.AreEqual(verificado, resposta.Verificado);
        Assert.AreEqual("n***@exemplo.com", resposta.Email);
        Assert.AreEqual(1, _unidade.TransacoesConfirmadas);
    }

    /// <summary>
    /// Garante o 409 do e-mail repetido, sem abrir transação, e a transação desfeita quando a gravação falha.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CriarAsync_EmailRepetidoOuFalha_409OuDesfeita()
    {
        // Act e Assert: e-mail repetido (sem diferenciar maiúsculas).
        RegraNegocioException repetido = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => Cadastro().CriarAsync(Requisicao("SOCORRISTA@exemplo.com", true), CancellationToken.None));
        Assert.AreEqual(IContasIdentity.MensagemEmailRepetido, repetido.Message);
        Assert.AreEqual(0, _unidade.TransacoesConfirmadas + _unidade.TransacoesDesfeitas);

        // Act e Assert: falha na gravação do socorrista desfaz a transação.
        _unidade.FalhaAoSalvar = () => new InvalidOperationException("banco fora");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => Cadastro().CriarAsync(Requisicao("outro@exemplo.com", true), CancellationToken.None));
        Assert.AreEqual(1, _unidade.TransacoesDesfeitas);
        Assert.AreEqual(0, _unidade.TransacoesConfirmadas);
    }

    private static CriarSocorristaRequisicao Requisicao(string email, bool verificado)
    {
        return new CriarSocorristaRequisicao
        {
            Nome = "Socorrista Novo",
            RegistroProfissional = "COREN-SP 000009",
            Organizacao = "SAMU Simulado",
            Verificado = verificado,
            Email = email,
            Senha = Senha,
        };
    }

    private AutenticacaoSocorristaServico Login()
    {
        return new AutenticacaoSocorristaServico(_contas, _tokens, _socorristas, _logger);
    }

    private SocorristaServico Cadastro()
    {
        return new SocorristaServico(_socorristas, _unidade, _contas);
    }
}
