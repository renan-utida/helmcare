using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Dispositivos;
using HelmCare.Api.Servicos.Cadastro;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Testes.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="DispositivoServico"/> com repositórios e unidade de trabalho falsos.
/// </summary>
[TestClass]
public sealed class DispositivoServicoTests
{
    private readonly FakeDispositivoRepositorio _dispositivos = new();
    private readonly FakeEntregadorRepositorio _entregadores = new();
    private readonly FakeUnidadeDeTrabalho _unidade = new();
    private readonly DispositivoServico _servico;
    private readonly Entregador _entregador = new(
        "Entregador Simulado",
        "11144477735",
        "12345678900",
        "11912345678",
        new DateTime(2026, 11, 12, 20, 0, 0, DateTimeKind.Utc));

    /// <summary>
    /// Monta o serviço com os fakes e um entregador já cadastrado.
    /// </summary>
    public DispositivoServicoTests()
    {
        _entregadores.Semear(_entregador);
        _servico = new DispositivoServico(_dispositivos, _entregadores, _unidade);
    }

    /// <summary>
    /// Garante que o vínculo grava o capacete do entregador e devolve o QR token gravado.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task VincularAsync_Valido_GravaEDevolveOQrToken()
    {
        // Arrange
        VincularDispositivoRequisicao requisicao = Requisicao("HC-0001", _entregador.Id);

        // Act
        DispositivoResposta resposta = await _servico.VincularAsync(requisicao, CancellationToken.None);

        // Assert
        Dispositivo gravado = _dispositivos.Dispositivos.Single();
        Assert.AreEqual(gravado.Id, resposta.Id);
        Assert.AreEqual("HC-0001", resposta.Codigo);
        Assert.AreEqual(_entregador.Id, resposta.EntregadorId);
        Assert.AreEqual(gravado.QrToken, resposta.QrToken);
        Assert.AreEqual(1, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que a urlCofre é o caminho da página do Cofre com o QR token.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task VincularAsync_Valido_MontaUrlCofreComOQrToken()
    {
        // Arrange
        VincularDispositivoRequisicao requisicao = Requisicao("HC-0001", _entregador.Id);

        // Act
        DispositivoResposta resposta = await _servico.VincularAsync(requisicao, CancellationToken.None);

        // Assert
        Assert.AreEqual($"/cofre/{resposta.QrToken}", resposta.UrlCofre);
        Assert.IsFalse(string.IsNullOrEmpty(resposta.QrToken));
    }

    /// <summary>
    /// Garante o 404 para um entregador inexistente, sem gravar nada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task VincularAsync_EntregadorInexistente_LancaRecursoNaoEncontrado()
    {
        // Arrange
        VincularDispositivoRequisicao requisicao = Requisicao("HC-0001", Guid.NewGuid());

        // Act e Assert
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => _servico.VincularAsync(requisicao, CancellationToken.None));
        Assert.IsEmpty(_dispositivos.Dispositivos);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 409 quando o código já está vinculado a outro entregador.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task VincularAsync_CodigoRepetido_LancaRegraNegocio()
    {
        // Arrange
        _dispositivos.Semear(new Dispositivo("HC-0001", Guid.NewGuid()));
        VincularDispositivoRequisicao requisicao = Requisicao("HC-0001", _entregador.Id);

        // Act
        RegraNegocioException excecao = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.VincularAsync(requisicao, CancellationToken.None));

        // Assert
        Assert.Contains("HC-0001", excecao.Message);
        Assert.HasCount(1, _dispositivos.Dispositivos);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o 409 quando o entregador já tem um capacete.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task VincularAsync_EntregadorJaTemCapacete_LancaRegraNegocio()
    {
        // Arrange
        _dispositivos.Semear(new Dispositivo("HC-0002", _entregador.Id));
        VincularDispositivoRequisicao requisicao = Requisicao("HC-0001", _entregador.Id);

        // Act
        RegraNegocioException excecao = await Assert.ThrowsExactlyAsync<RegraNegocioException>(
            () => _servico.VincularAsync(requisicao, CancellationToken.None));

        // Assert
        Assert.Contains("já tem um capacete", excecao.Message);
        Assert.HasCount(1, _dispositivos.Dispositivos);
        Assert.AreEqual(0, _unidade.VezesSalvo);
    }

    private static VincularDispositivoRequisicao Requisicao(string codigo, Guid entregadorId)
    {
        return new VincularDispositivoRequisicao { Codigo = codigo, EntregadorId = entregadorId };
    }
}
