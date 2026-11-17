using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Cadastro;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do cadastro com a conta (1.15) e dos pedidos do Mobile no <see cref="EntregadorServico"/>:
/// e-mail, CNH e telefone repetidos, a transação, a conta dos dados antigos, a ficha, os contatos e o
/// telefone. A autorização é a real, com o token do entregador dono.
/// </summary>
[TestClass]
public sealed class CadastroContaTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 16, 20, 0, 0, TimeSpan.Zero);

    private readonly FakeEntregadorRepositorio _entregadores = new();
    private readonly FakeVoluntarioRepositorio _voluntarios = new();
    private readonly FakeUnidadeDeTrabalho _unidade = new();
    private readonly FakeContasIdentity _contas = new();

    /// <summary>
    /// Garante o cadastro com a conta: papel ENTREGADOR, entregador vinculado à conta, transação
    /// confirmada e o e-mail mascarado na resposta.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CriarAsync_Valido_CriaContaEEntregadorNaMesmaTransacao()
    {
        // Act
        EntregadorResposta resposta = await Servico(FakeUsuarioAtual.Anonimo()).CriarAsync(Requisicao(), CancellationToken.None);

        // Assert
        ContaFake conta = _contas.Contas.Single();
        Entregador gravado = _entregadores.Entregadores.Single();
        Assert.AreEqual(Papeis.Entregador, conta.Papel);
        Assert.AreEqual(conta.UsuarioId, gravado.UsuarioId);
        Assert.AreEqual("e***@exemplo.com", resposta.Email);
        Assert.AreEqual(1, _unidade.TransacoesConfirmadas);
        Assert.AreEqual(0, _unidade.TransacoesDesfeitas);
    }

    /// <summary>
    /// Garante que, se a gravação do entregador falhar depois da conta, a transação é desfeita (nada
    /// fica gravado no banco) e o erro sobe.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CriarAsync_SegundaGravacaoFalha_DesfazATransacao()
    {
        // Arrange
        _unidade.FalhaAoSalvar = () => new InvalidOperationException("banco fora");

        // Act
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => Servico(FakeUsuarioAtual.Anonimo()).CriarAsync(Requisicao(), CancellationToken.None));

        // Assert
        Assert.AreEqual(0, _unidade.TransacoesConfirmadas);
        Assert.AreEqual(1, _unidade.TransacoesDesfeitas);
    }

    /// <summary>
    /// Garante o 409 de e-mail repetido sem diferenciar maiúsculas, sem abrir transação.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CriarAsync_EmailRepetido_Lanca409SemGravar()
    {
        // Arrange
        _contas.Semear("ENTREGADOR@exemplo.com", "Senha123", Papeis.Entregador);

        // Act
        RegraNegocioException excecao = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => Servico(FakeUsuarioAtual.Anonimo()).CriarAsync(Requisicao(), CancellationToken.None));

        // Assert
        Assert.AreEqual(IContasIdentity.MensagemEmailRepetido, excecao.Message);
        Assert.IsEmpty(_entregadores.Entregadores);
        Assert.AreEqual(0, _unidade.TransacoesConfirmadas + _unidade.TransacoesDesfeitas);
    }

    /// <summary>
    /// Garante o 409 de CNH e de telefone repetidos, como o do CPF, comparando só os dígitos.
    /// </summary>
    /// <param name="campo">Campo repetido.</param>
    /// <param name="mensagem">Mensagem esperada.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("cnh", EntregadorServico.MensagemCnhRepetida)]
    [DataRow("telefone", EntregadorServico.MensagemTelefoneRepetido)]
    public async Task CriarAsync_CnhOuTelefoneRepetido_Lanca409(string campo, string mensagem)
    {
        // Arrange
        _entregadores.Semear(new Entregador("Outro Simulado", "52998224725", "12345678900", "11912345678", _agora.UtcDateTime));
        CriarEntregadorRequisicao requisicao = campo == "cnh"
            ? Requisicao(cnh: "123.456.789-00")
            : Requisicao(telefone: "(11) 91234-5678");

        // Act
        RegraNegocioException excecao = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => Servico(FakeUsuarioAtual.Anonimo()).CriarAsync(requisicao, CancellationToken.None));

        // Assert
        Assert.AreEqual(mensagem, excecao.Message);
        Assert.HasCount(1, _entregadores.Entregadores);
    }

    /// <summary>
    /// Garante a conta de um entregador anterior à 1.15 (rota de desenvolvimento): vincula a conta.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CriarContaAsync_EntregadorSemConta_VinculaAConta()
    {
        // Arrange
        Entregador antigo = Semear();

        // Act
        EntregadorResposta resposta = await Servico(FakeUsuarioAtual.Anonimo())
            .CriarContaAsync(antigo.Id, new ContaRequisicao { Email = "antigo@exemplo.com", Senha = "Senha123" }, CancellationToken.None);

        // Assert
        Assert.AreEqual(_contas.Contas.Single().UsuarioId, antigo.UsuarioId);
        Assert.AreEqual("a***@exemplo.com", resposta.Email);
        Assert.AreEqual(1, _unidade.TransacoesConfirmadas);
    }

    /// <summary>
    /// Garante os erros da rota de desenvolvimento: entregador inexistente 404 e entregador que já tem conta 409.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CriarContaAsync_InexistenteOuJaTemConta_404E409()
    {
        // Arrange
        Entregador comConta = Semear();
        comConta.VincularUsuario(_contas.Semear("ja@exemplo.com", "Senha123", Papeis.Entregador));
        var conta = new ContaRequisicao { Email = "novo@exemplo.com", Senha = "Senha123" };

        // Act e Assert
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => Servico(FakeUsuarioAtual.Anonimo()).CriarContaAsync(Guid.NewGuid(), conta, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => Servico(FakeUsuarioAtual.Anonimo()).CriarContaAsync(comConta.Id, conta, CancellationToken.None));
    }

    /// <summary>
    /// Garante o GET da ficha: o mesmo corpo do PUT para o dono; sem ficha, 404.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterFichaMedicaAsync_ComESemFicha_200E404()
    {
        // Arrange
        Entregador entregador = Semear();
        EntregadorServico servico = Servico(FakeUsuarioAtual.Entregador(entregador.Id));

        // Act e Assert: sem ficha
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => servico.ObterFichaMedicaAsync(entregador.Id, CancellationToken.None));

        // Act e Assert: com ficha
        FichaMedicaResposta gravada = await servico.DefinirFichaMedicaAsync(
            entregador.Id,
            new FichaMedicaRequisicao { TipoSanguineo = "O+", Alergias = "Dipirona" },
            CancellationToken.None);
        FichaMedicaResposta lida = await servico.ObterFichaMedicaAsync(entregador.Id, CancellationToken.None);
        Assert.AreEqual(gravada.TipoSanguineo, lida.TipoSanguineo);
        Assert.AreEqual("Dipirona", lida.Alergias);
    }

    /// <summary>
    /// Garante a edição do contato: mesmo corpo do POST, telefone mascarado na resposta.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EditarContatoAsync_DoDono_SubstituiOsDados()
    {
        // Arrange
        Entregador entregador = Semear();
        ContatoEmergencia contato = entregador.AdicionarContato("Contato Simulado", "1131234567", "Mãe");

        // Act
        ContatoResposta resposta = await Servico(FakeUsuarioAtual.Entregador(entregador.Id)).EditarContatoAsync(
            entregador.Id,
            contato.Id,
            new ContatoRequisicao { Nome = "Contato Editado", Telefone = "(11) 98888-7777", Parentesco = "Irmã" },
            CancellationToken.None);

        // Assert
        Assert.AreEqual("Contato Editado", contato.Nome);
        Assert.AreEqual("11988887777", contato.Telefone);
        Assert.AreEqual("(11) *****-7777", resposta.Telefone);
        Assert.AreEqual(1, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 404 para o contato inexistente ou de outro entregador, sem gravar.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task EditarERemoverContato_DeOutroEntregador_404()
    {
        // Arrange
        Entregador dono = Semear();
        Entregador outro = Semear("52998224725", "98765432100", "11977776666");
        ContatoEmergencia doOutro = outro.AdicionarContato("Contato Simulado", "1131234567", "Pai");
        EntregadorServico servico = Servico(FakeUsuarioAtual.Entregador(dono.Id));
        var dados = new ContatoRequisicao { Nome = "X", Telefone = "1131234567", Parentesco = "Pai" };

        // Act e Assert
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => servico.EditarContatoAsync(dono.Id, doOutro.Id, dados, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => servico.RemoverContatoAsync(dono.Id, doOutro.Id, CancellationToken.None));
        Assert.HasCount(1, outro.Contatos);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante a exclusão do contato, inclusive do último.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task RemoverContatoAsync_UltimoContato_Permitido()
    {
        // Arrange
        Entregador entregador = Semear();
        ContatoEmergencia unico = entregador.AdicionarContato("Contato Simulado", "1131234567", "Mãe");

        // Act
        await Servico(FakeUsuarioAtual.Entregador(entregador.Id)).RemoverContatoAsync(entregador.Id, unico.Id, CancellationToken.None);

        // Assert
        Assert.IsEmpty(entregador.Contatos);
        Assert.AreEqual(1, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante a troca de telefone: grava só os dígitos e responde o corpo do GET (mascarado); o
    /// próprio telefone de novo não conta como repetido.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task TrocarTelefoneAsync_Valido_GravaEDevolveOGet()
    {
        // Arrange
        Entregador entregador = Semear();
        EntregadorServico servico = Servico(FakeUsuarioAtual.Entregador(entregador.Id));

        // Act
        EntregadorResposta resposta = await servico.TrocarTelefoneAsync(
            entregador.Id,
            new TelefoneRequisicao { Telefone = "(11) 98765-4321" },
            CancellationToken.None);
        await servico.TrocarTelefoneAsync(entregador.Id, new TelefoneRequisicao { Telefone = "11987654321" }, CancellationToken.None);

        // Assert
        Assert.AreEqual("11987654321", entregador.Telefone);
        Assert.AreEqual("(11) *****-4321", resposta.Telefone);
        Assert.AreEqual(entregador.Id, resposta.Id);
        Assert.AreEqual(2, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 409 do telefone já usado por outro entregador, sem gravar.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task TrocarTelefoneAsync_TelefoneDeOutro_Lanca409()
    {
        // Arrange
        Entregador entregador = Semear();
        Semear("52998224725", "98765432100", "11977776666");

        // Act
        RegraNegocioException excecao = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => Servico(FakeUsuarioAtual.Entregador(entregador.Id)).TrocarTelefoneAsync(
                entregador.Id,
                new TelefoneRequisicao { Telefone = "(11) 97777-6666" },
                CancellationToken.None));

        // Assert
        Assert.AreEqual(EntregadorServico.MensagemTelefoneRepetido, excecao.Message);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    private static CriarEntregadorRequisicao Requisicao(string cnh = "11122233344", string telefone = "(11) 95555-4444")
    {
        return new CriarEntregadorRequisicao
        {
            Nome = "Entregador Simulado",
            Cpf = "111.444.777-35",
            Cnh = cnh,
            Telefone = telefone,
            Email = "entregador@exemplo.com",
            Senha = "Senha123",
        };
    }

    private Entregador Semear(string cpf = "11144477735", string cnh = "12345678900", string telefone = "11912345678")
    {
        var entregador = new Entregador("Entregador Simulado", cpf, cnh, telefone, _agora.UtcDateTime);
        _entregadores.Semear(entregador);
        return entregador;
    }

    private EntregadorServico Servico(FakeUsuarioAtual usuario)
    {
        return new EntregadorServico(
            _entregadores,
            _unidade,
            _contas,
            new AutorizacaoRecursos(usuario, _voluntarios),
            new FakeTimeProvider(_agora));
    }
}
