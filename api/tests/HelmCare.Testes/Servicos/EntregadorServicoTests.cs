using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Entregadores;
using HelmCare.Api.Servicos.Cadastro;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="EntregadorServico"/> com repositório e unidade de trabalho falsos.
/// </summary>
[TestClass]
public sealed class EntregadorServicoTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);

    private readonly FakeEntregadorRepositorio _repositorio = new();
    private readonly FakeUnidadeDeTrabalho _unidade = new();
    private readonly EntregadorServico _servico;

    /// <summary>
    /// Monta o serviço com os fakes e o relógio parado em <see cref="_agora"/>.
    /// </summary>
    public EntregadorServicoTests()
    {
        _servico = new EntregadorServico(_repositorio, _unidade, new FakeTimeProvider(_agora));
    }

    /// <summary>
    /// Garante que o cadastro grava o entregador normalizado, com o criadoEm do relógio, e devolve a PII mascarada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CriarAsync_DadosValidos_GravaEDevolveMascarado()
    {
        // Arrange
        CriarEntregadorRequisicao requisicao = Requisicao("111.444.777-35");

        // Act
        EntregadorResposta resposta = await _servico.CriarAsync(requisicao, CancellationToken.None);

        // Assert
        Entregador gravado = _repositorio.Entregadores.Single();
        Assert.AreEqual(gravado.Id, resposta.Id);
        Assert.AreEqual("11144477735", gravado.Cpf);
        Assert.AreEqual(_agora.UtcDateTime, gravado.CriadoEm);
        Assert.AreEqual(1, _unidade.VezesSalvo);
        Assert.AreEqual("***.***.***-35", resposta.Cpf);
        Assert.AreEqual("*******8900", resposta.Cnh);
        Assert.AreEqual("(11) *****-5678", resposta.Telefone);
        Assert.AreEqual(_agora.UtcDateTime, resposta.CriadoEm);
        Assert.IsFalse(resposta.PossuiFichaMedica);
        Assert.IsEmpty(resposta.Contatos);
    }

    /// <summary>
    /// Garante que um CPF já cadastrado, mesmo com outra máscara, é recusado sem gravar nada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CriarAsync_CpfRepetido_LancaRegraNegocioSemGravar()
    {
        // Arrange
        _repositorio.Semear(Entregador());
        CriarEntregadorRequisicao requisicao = Requisicao("111.444.777-35");

        // Act
        RegraNegocioException excecao = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.CriarAsync(requisicao, CancellationToken.None));

        // Assert
        Assert.Contains("CPF", excecao.Message);
        Assert.DoesNotContain("11144477735", excecao.Message);
        Assert.HasCount(1, _repositorio.Entregadores);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 404 para um entregador inexistente.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_Inexistente_LancaRecursoNaoEncontrado()
    {
        // Arrange
        Guid id = Guid.NewGuid();

        // Act e Assert
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.ObterAsync(id, CancellationToken.None));
    }

    /// <summary>
    /// Garante que, sem ficha, a resposta diz possuiFichaMedica falso.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_SemFicha_PossuiFichaMedicaFalso()
    {
        // Arrange
        Entregador entregador = Entregador();
        _repositorio.Semear(entregador);

        // Act
        EntregadorResposta resposta = await _servico.ObterAsync(entregador.Id, CancellationToken.None);

        // Assert
        Assert.IsFalse(resposta.PossuiFichaMedica);
    }

    /// <summary>
    /// Garante que, com ficha, a resposta diz possuiFichaMedica verdadeiro e o JSON não leva nenhum dado de saúde.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_ComFicha_PossuiFichaMedicaSemDadosDeSaude()
    {
        // Arrange
        Entregador entregador = Entregador();
        entregador.DefinirFichaMedica("AB-", "Dipirona", "Epilepsia", "Carbamazepina");
        _repositorio.Semear(entregador);

        // Act
        EntregadorResposta resposta = await _servico.ObterAsync(entregador.Id, CancellationToken.None);
        string json = JsonSerializer.Serialize(resposta, JsonSerializerOptions.Web);

        // Assert
        Assert.IsTrue(resposta.PossuiFichaMedica);
        Assert.DoesNotContain("tipoSanguineo", json);
        Assert.DoesNotContain("AB-", json);
        Assert.DoesNotContain("Dipirona", json);
        Assert.DoesNotContain("Epilepsia", json);
        Assert.DoesNotContain("Carbamazepina", json);
    }

    /// <summary>
    /// Garante que o primeiro PUT cria a ficha e o segundo atualiza a mesma, sem criar outra.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task DefinirFichaMedicaAsync_DuasVezes_CriaEDepoisAtualizaAMesmaFicha()
    {
        // Arrange
        Entregador entregador = Entregador();
        _repositorio.Semear(entregador);
        var primeira = new FichaMedicaRequisicao { TipoSanguineo = "O+", Alergias = "Dipirona" };
        var segunda = new FichaMedicaRequisicao { TipoSanguineo = "A-", Medicamentos = "Insulina" };

        // Act
        FichaMedicaResposta criada = await _servico.DefinirFichaMedicaAsync(entregador.Id, primeira, CancellationToken.None);
        Guid idDaFicha = entregador.FichaMedica!.Id;
        FichaMedicaResposta atualizada = await _servico.DefinirFichaMedicaAsync(entregador.Id, segunda, CancellationToken.None);

        // Assert
        Assert.AreEqual("O+", criada.TipoSanguineo);
        Assert.AreEqual("Dipirona", criada.Alergias);
        Assert.AreEqual("A-", atualizada.TipoSanguineo);
        Assert.AreEqual(string.Empty, atualizada.Alergias);
        Assert.AreEqual("Insulina", atualizada.Medicamentos);
        Assert.AreEqual(idDaFicha, entregador.FichaMedica.Id);
        Assert.AreEqual("A-", entregador.FichaMedica.TipoSanguineo);
        Assert.AreEqual(2, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 404 ao definir a ficha de um entregador inexistente.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task DefinirFichaMedicaAsync_EntregadorInexistente_LancaRecursoNaoEncontrado()
    {
        // Arrange
        var requisicao = new FichaMedicaRequisicao { TipoSanguineo = "O+" };

        // Act e Assert
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.DefinirFichaMedicaAsync(Guid.NewGuid(), requisicao, CancellationToken.None));
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que o contato entra no entregador e volta com o telefone mascarado.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AdicionarContatoAsync_Valido_AdicionaEMascaraOTelefone()
    {
        // Arrange
        Entregador entregador = Entregador();
        _repositorio.Semear(entregador);
        var requisicao = new ContatoRequisicao
        {
            Nome = "Contato Simulado",
            Telefone = "(11) 3123-4567",
            Parentesco = "Mãe",
        };

        // Act
        ContatoResposta resposta = await _servico.AdicionarContatoAsync(entregador.Id, requisicao, CancellationToken.None);

        // Assert
        ContatoEmergencia contato = entregador.Contatos.Single();
        Assert.AreEqual(contato.Id, resposta.Id);
        Assert.AreEqual("1131234567", contato.Telefone);
        Assert.AreEqual("(11) ****-4567", resposta.Telefone);
        Assert.AreEqual("Contato Simulado", resposta.Nome);
        Assert.AreEqual("Mãe", resposta.Parentesco);
        Assert.AreEqual(1, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 404 ao adicionar contato a um entregador inexistente.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AdicionarContatoAsync_EntregadorInexistente_LancaRecursoNaoEncontrado()
    {
        // Arrange
        var requisicao = new ContatoRequisicao { Nome = "Contato Simulado", Telefone = "1131234567", Parentesco = "Mãe" };

        // Act e Assert
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.AdicionarContatoAsync(Guid.NewGuid(), requisicao, CancellationToken.None));
    }

    private static CriarEntregadorRequisicao Requisicao(string cpf)
    {
        return new CriarEntregadorRequisicao
        {
            Nome = "Entregador Simulado",
            Cpf = cpf,
            Cnh = "12345678900",
            Telefone = "(11) 91234-5678",
        };
    }

    private static Entregador Entregador()
    {
        return new Entregador("Entregador Simulado", "11144477735", "12345678900", "11912345678", _agora.UtcDateTime);
    }
}
