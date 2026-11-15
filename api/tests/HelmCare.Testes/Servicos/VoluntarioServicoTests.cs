using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Voluntarios;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Voluntarios;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="VoluntarioServico"/>: cadastro (201, 404, 409) e disponibilidade (posição
/// obrigatória para ficar disponível e apagada ao ficar indisponível).
/// </summary>
[TestClass]
public sealed class VoluntarioServicoTests
{
    private readonly FakeVoluntarioRepositorio _voluntarios = new();
    private readonly FakeEntregadorRepositorio _entregadores = new();
    private readonly FakeUnidadeDeTrabalho _unidade = new();
    private readonly FakeLogger<VoluntarioServico> _logger = new();
    private readonly VoluntarioServico _servico;

    /// <summary>
    /// Monta o serviço com os fakes.
    /// </summary>
    public VoluntarioServicoTests()
    {
        _servico = new VoluntarioServico(_voluntarios, _entregadores, _unidade, _logger);
    }

    /// <summary>
    /// Garante que o cadastro devolve o voluntário indisponível e sem posição e grava uma vez.
    /// </summary>
    /// <param name="tipo">Tipo enviado.</param>
    /// <param name="esperado">Tipo gravado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("ENTREGADOR", TipoVoluntario.Entregador)]
    [DataRow("VIGIA_MOTO", TipoVoluntario.VigiaMoto)]
    public async Task CadastrarAsync_EntregadorExistente_IndisponivelSemPosicao(string tipo, TipoVoluntario esperado)
    {
        // Arrange
        Entregador entregador = SemearEntregador();

        // Act
        VoluntarioResposta resposta = await _servico.CadastrarAsync(Cadastro(entregador.Id, tipo), CancellationToken.None);

        // Assert
        Assert.AreEqual(entregador.Id, resposta.EntregadorId);
        Assert.AreEqual(esperado, resposta.Tipo);
        Assert.IsFalse(resposta.Disponivel);
        Assert.IsNull(resposta.Latitude);
        Assert.IsNull(resposta.Longitude);
        Assert.AreEqual(resposta.VoluntarioId, _voluntarios.Voluntarios.Single().Id);
        Assert.AreEqual(1, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 404 para entregador inexistente, sem gravar.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CadastrarAsync_EntregadorInexistente_Lanca404SemGravar()
    {
        // Act
        RecursoNaoEncontradoException excecao = await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.CadastrarAsync(Cadastro(Guid.NewGuid(), "ENTREGADOR"), CancellationToken.None));

        // Assert
        Assert.AreEqual("Entregador não encontrado.", excecao.Message);
        Assert.IsEmpty(_voluntarios.Voluntarios);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 409 para o entregador que já é voluntário, sem criar outro.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CadastrarAsync_JaVoluntario_Lanca409SemGravar()
    {
        // Arrange
        Entregador entregador = SemearEntregador();
        _voluntarios.Semear(new Voluntario(entregador.Id, TipoVoluntario.Entregador));

        // Act
        RegraNegocioException excecao = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.CadastrarAsync(Cadastro(entregador.Id, "VIGIA_MOTO"), CancellationToken.None));

        // Assert
        Assert.AreEqual(VoluntarioServico.MensagemJaVoluntario, excecao.Message);
        Assert.HasCount(1, _voluntarios.Voluntarios);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 400 para tipo fora do contrato (o nome é exato, com maiúsculas) ou ausente, e para
    /// o entregador ausente, sem gravar.
    /// </summary>
    /// <param name="tipo">Tipo enviado.</param>
    /// <param name="comEntregador">Se o entregadorId vai preenchido.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("entregador", true)]
    [DataRow("MOTOBOY", true)]
    [DataRow(null, true)]
    [DataRow("ENTREGADOR", false)]
    public async Task CadastrarAsync_DadosInvalidos_Lanca400SemGravar(string? tipo, bool comEntregador)
    {
        // Arrange
        Entregador entregador = SemearEntregador();
        var requisicao = new CadastrarVoluntarioRequisicao { EntregadorId = comEntregador ? entregador.Id : null, Tipo = tipo };

        // Act e Assert
        await Assert.ThrowsExactlyAsync<ValidacaoDominioException>(
            () => _servico.CadastrarAsync(requisicao, CancellationToken.None));
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que ficar disponível guarda a posição e que o log não leva as coordenadas.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AtualizarDisponibilidadeAsync_DisponivelComPosicao_GuardaAPosicao()
    {
        // Arrange
        Voluntario voluntario = SemearVoluntario();

        // Act
        VoluntarioResposta resposta = await _servico.AtualizarDisponibilidadeAsync(
            voluntario.Id,
            Disponibilidade(true, -23.5614, -46.6559),
            CancellationToken.None);

        // Assert
        Assert.IsTrue(resposta.Disponivel);
        Assert.AreEqual(-23.5614, resposta.Latitude);
        Assert.AreEqual(-46.6559, resposta.Longitude);
        Assert.IsTrue(voluntario.Disponivel);
        Assert.AreEqual(1, _unidade.VezesSalvo);
        Assert.IsFalse(_logger.Registros.Any(r => r.Mensagem.Contains("23.56", StringComparison.Ordinal) || r.Mensagem.Contains("23,56", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Garante o 400 para disponível sem as duas coordenadas, sem mudar o voluntário nem gravar.
    /// </summary>
    /// <param name="comLatitude">Se a latitude vai preenchida.</param>
    /// <param name="comLongitude">Se a longitude vai preenchida.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public async Task AtualizarDisponibilidadeAsync_DisponivelSemPosicao_Lanca400(bool comLatitude, bool comLongitude)
    {
        // Arrange
        Voluntario voluntario = SemearVoluntario();
        DisponibilidadeVoluntarioRequisicao requisicao = Disponibilidade(
            true,
            comLatitude ? -23.5614 : null,
            comLongitude ? -46.6559 : null);

        // Act
        ValidacaoDominioException excecao = await Assert.ThrowsExactlyAsync<ValidacaoDominioException>(
            () => _servico.AtualizarDisponibilidadeAsync(voluntario.Id, requisicao, CancellationToken.None));

        // Assert
        Assert.AreEqual("A posição é obrigatória para ficar disponível.", excecao.Message);
        Assert.IsFalse(voluntario.Disponivel);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que ficar indisponível apaga a posição, mesmo com as coordenadas na requisição.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AtualizarDisponibilidadeAsync_Indisponivel_ApagaAPosicaoMesmoComCoordenadas()
    {
        // Arrange
        Voluntario voluntario = SemearVoluntario();
        await _servico.AtualizarDisponibilidadeAsync(voluntario.Id, Disponibilidade(true, -23.5614, -46.6559), CancellationToken.None);

        // Act
        VoluntarioResposta resposta = await _servico.AtualizarDisponibilidadeAsync(
            voluntario.Id,
            Disponibilidade(false, -23.5560, -46.6620),
            CancellationToken.None);

        // Assert
        Assert.IsFalse(resposta.Disponivel);
        Assert.IsNull(resposta.Latitude);
        Assert.IsNull(resposta.Longitude);
        Assert.IsNull(voluntario.Latitude);
        Assert.AreEqual(2, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 404 para voluntário inexistente.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task AtualizarDisponibilidadeAsync_VoluntarioInexistente_Lanca404()
    {
        // Act
        RecursoNaoEncontradoException excecao = await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.AtualizarDisponibilidadeAsync(Guid.NewGuid(), Disponibilidade(true, -23.5614, -46.6559), CancellationToken.None));

        // Assert
        Assert.AreEqual("Voluntário não encontrado.", excecao.Message);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    private static CadastrarVoluntarioRequisicao Cadastro(Guid entregadorId, string tipo)
    {
        return new CadastrarVoluntarioRequisicao { EntregadorId = entregadorId, Tipo = tipo };
    }

    private static DisponibilidadeVoluntarioRequisicao Disponibilidade(bool disponivel, double? latitude, double? longitude)
    {
        return new DisponibilidadeVoluntarioRequisicao { Disponivel = disponivel, Latitude = latitude, Longitude = longitude };
    }

    private Entregador SemearEntregador()
    {
        Entregador entregador = CenarioSos.NovoEntregador();
        _entregadores.Semear(entregador);
        return entregador;
    }

    private Voluntario SemearVoluntario()
    {
        var voluntario = new Voluntario(SemearEntregador().Id, TipoVoluntario.Entregador);
        _voluntarios.Semear(voluntario);
        return voluntario;
    }
}
