using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Persistencia.Configuracoes;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Cadastro;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Testes.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace HelmCare.Testes.Configuracao;

/// <summary>
/// Testes do <see cref="TratadorExcecoes"/> para a 1.15: 401, 403 e o 409 da corrida pelos índices
/// únicos de CPF, CNH, telefone e e-mail, com o mesmo texto da conferência do serviço.
/// </summary>
[TestClass]
public sealed class TratadorExcecoesIdentidadeTests
{
    /// <summary>
    /// Garante o 401 e o 403 em ProblemDetails, com a mensagem da exceção.
    /// </summary>
    /// <param name="caso">Exceção lançada.</param>
    /// <param name="status">Status esperado.</param>
    /// <param name="titulo">Título esperado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("401", 401, ProblemasPadrao.TituloNaoAutenticado)]
    [DataRow("403", 403, ProblemasPadrao.TituloAcessoNegado)]
    public async Task TryHandleAsync_NaoAutenticadoOuAcessoNegado_401E403(string caso, int status, string titulo)
    {
        // Arrange
        var problemas = new FakeProblemDetailsService();
        var tratador = new TratadorExcecoes(problemas, NullLogger<TratadorExcecoes>.Instance);
        var contexto = new DefaultHttpContext();
        Exception excecao = caso == "401"
            ? new NaoAutenticadoException(AutorizacaoRecursos.MensagemSemToken)
            : new AcessoNegadoException(AutorizacaoRecursos.MensagemOutroDono);

        // Act
        await tratador.TryHandleAsync(contexto, excecao, CancellationToken.None);

        // Assert
        Assert.AreEqual(status, contexto.Response.StatusCode);
        Assert.AreEqual(titulo, problemas.Escrito?.Title);
        Assert.AreEqual(excecao.Message, problemas.Escrito?.Detail);
    }

    /// <summary>
    /// Garante o 409 da corrida pelos índices únicos do cadastro, com o texto da conferência do serviço.
    /// </summary>
    /// <param name="indice">Índice violado.</param>
    /// <param name="mensagem">Mensagem esperada.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EntregadorConfiguracao.IndiceCpf, EntregadorServico.MensagemCpfRepetido)]
    [DataRow(EntregadorConfiguracao.IndiceCnh, EntregadorServico.MensagemCnhRepetida)]
    [DataRow(EntregadorConfiguracao.IndiceTelefone, EntregadorServico.MensagemTelefoneRepetido)]
    [DataRow(TratadorExcecoes.IndiceEmailIdentity, IContasIdentity.MensagemEmailRepetido)]
    public async Task TryHandleAsync_IndiceUnicoDoCadastro_409ComOTextoDoServico(string indice, string mensagem)
    {
        // Arrange
        var problemas = new FakeProblemDetailsService();
        var tratador = new TratadorExcecoes(problemas, NullLogger<TratadorExcecoes>.Instance);
        var contexto = new DefaultHttpContext();
        var excecao = new DbUpdateException(
            "detalhe interno",
            new PostgresException("detalhe interno", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: indice));

        // Act
        await tratador.TryHandleAsync(contexto, excecao, CancellationToken.None);

        // Assert
        Assert.AreEqual(StatusCodes.Status409Conflict, contexto.Response.StatusCode);
        Assert.AreEqual("Regra de negócio violada", problemas.Escrito?.Title);
        Assert.AreEqual(mensagem, problemas.Escrito?.Detail);
    }
}
