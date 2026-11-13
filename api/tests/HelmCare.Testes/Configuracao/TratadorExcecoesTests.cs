using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Persistencia.Configuracoes;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Comum;
using HelmCare.Testes.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace HelmCare.Testes.Configuracao;

/// <summary>
/// Testes do <see cref="TratadorExcecoes"/>: cada exceção vira o status certo, em ProblemDetails.
/// </summary>
[TestClass]
public sealed class TratadorExcecoesTests
{
    private const string MensagemInterna = "detalhe interno: tabela Entregadores, índice IX_Entregadores_Cpf";

    /// <summary>
    /// Garante o status de cada tipo de exceção e que o ProblemDetails leva o traceId.
    /// </summary>
    /// <param name="caso">Nome do caso, que escolhe a exceção.</param>
    /// <param name="statusEsperado">Status HTTP esperado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("validacao", StatusCodes.Status400BadRequest)]
    [DataRow("naoEncontrado", StatusCodes.Status404NotFound)]
    [DataRow("transicao", StatusCodes.Status409Conflict)]
    [DataRow("regra", StatusCodes.Status409Conflict)]
    [DataRow("concorrencia", StatusCodes.Status409Conflict)]
    [DataRow("unico", StatusCodes.Status409Conflict)]
    [DataRow("mensageria", StatusCodes.Status503ServiceUnavailable)]
    [DataRow("bancoOutro", StatusCodes.Status500InternalServerError)]
    [DataRow("inesperada", StatusCodes.Status500InternalServerError)]
    public async Task TryHandleAsync_CadaExcecao_RespondeOStatusCerto(string caso, int statusEsperado)
    {
        // Arrange
        (TratadorExcecoes tratador, FakeProblemDetailsService problemas) = Criar();
        var contexto = new DefaultHttpContext();

        // Act
        bool tratado = await tratador.TryHandleAsync(contexto, Excecao(caso), CancellationToken.None);

        // Assert
        Assert.IsTrue(tratado);
        Assert.AreEqual(statusEsperado, contexto.Response.StatusCode);
        Assert.IsNotNull(problemas.Escrito);
        Assert.AreEqual(statusEsperado, problemas.Escrito.Status);
        Assert.IsFalse(string.IsNullOrEmpty(problemas.Escrito.Title));
        Assert.IsTrue(problemas.Escrito.Extensions.ContainsKey("traceId"));
    }

    /// <summary>
    /// Garante que a mensagem escrita pelo domínio chega ao cliente no detail.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task TryHandleAsync_ExcecaoDoDominio_RepassaAMensagemNoDetail()
    {
        // Arrange
        (TratadorExcecoes tratador, FakeProblemDetailsService problemas) = Criar();

        // Act
        await tratador.TryHandleAsync(
            new DefaultHttpContext(),
            new ValidacaoDominioException("O campo cpf deve ter 11 dígitos."),
            CancellationToken.None);

        // Assert
        Assert.AreEqual("O campo cpf deve ter 11 dígitos.", problemas.Escrito?.Detail);
    }

    /// <summary>
    /// Garante que o 500, o 409 de índice único e o 503 não expõem a mensagem interna da exceção.
    /// </summary>
    /// <param name="caso">Nome do caso, que escolhe a exceção.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("inesperada")]
    [DataRow("bancoOutro")]
    [DataRow("unico")]
    [DataRow("mensageria")]
    public async Task TryHandleAsync_ErroInterno_NaoExpoeAMensagemInterna(string caso)
    {
        // Arrange
        (TratadorExcecoes tratador, FakeProblemDetailsService problemas) = Criar();

        // Act
        await tratador.TryHandleAsync(new DefaultHttpContext(), Excecao(caso), CancellationToken.None);

        // Assert
        ProblemDetails? problema = problemas.Escrito;
        Assert.IsNotNull(problema);
        Assert.DoesNotContain("tabela", problema.Detail ?? string.Empty);
        Assert.DoesNotContain("IX_", problema.Detail ?? string.Empty);
        Assert.DoesNotContain("tabela", problema.Title ?? string.Empty);
    }

    /// <summary>
    /// Garante que a mensageria indisponível vira 503 com o texto fixo em português para o cliente.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task TryHandleAsync_MensageriaIndisponivel_Responde503ComTextoFixo()
    {
        // Arrange
        (TratadorExcecoes tratador, FakeProblemDetailsService problemas) = Criar();
        var contexto = new DefaultHttpContext();

        // Act
        await tratador.TryHandleAsync(contexto, new MensageriaIndisponivelException(MensagemInterna), CancellationToken.None);

        // Assert
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, contexto.Response.StatusCode);
        Assert.AreEqual("O capacete não pôde ser avisado agora; tente de novo.", problemas.Escrito?.Detail);
    }

    /// <summary>
    /// Garante que a violação do índice de turno ativo (dois POST /turnos ao mesmo tempo) vira 409
    /// com a mesma mensagem da checagem do serviço.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task TryHandleAsync_IndiceDeTurnoAtivo_Responde409TurnoAtivo()
    {
        // Arrange
        (TratadorExcecoes tratador, FakeProblemDetailsService problemas) = Criar();
        var contexto = new DefaultHttpContext();
        var excecao = new DbUpdateException(
            MensagemInterna,
            new PostgresException(
                MensagemInterna,
                "ERROR",
                "ERROR",
                PostgresErrorCodes.UniqueViolation,
                constraintName: TurnoConfiguracao.IndiceTurnoAtivo));

        // Act
        await tratador.TryHandleAsync(contexto, excecao, CancellationToken.None);

        // Assert
        Assert.AreEqual(StatusCodes.Status409Conflict, contexto.Response.StatusCode);
        Assert.AreEqual("O entregador já tem um turno ativo.", problemas.Escrito?.Detail);
    }

    private static (TratadorExcecoes Tratador, FakeProblemDetailsService Problemas) Criar()
    {
        var problemas = new FakeProblemDetailsService();
        var tratador = new TratadorExcecoes(problemas, NullLogger<TratadorExcecoes>.Instance);
        return (tratador, problemas);
    }

    private static Exception Excecao(string caso)
    {
        return caso switch
        {
            "validacao" => new ValidacaoDominioException("O campo nome é obrigatório."),
            "naoEncontrado" => new RecursoNaoEncontradoException("Entregador não encontrado."),
            "transicao" => new TransicaoInvalidaException("A ocorrência já foi encerrada."),
            "regra" => new RegraNegocioException("O entregador já tem um capacete vinculado."),
            "mensageria" => new MensageriaIndisponivelException(MensagemInterna),
            "concorrencia" => new DbUpdateConcurrencyException(MensagemInterna),
            "unico" => new DbUpdateException(
                MensagemInterna,
                new PostgresException(MensagemInterna, "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation)),
            "bancoOutro" => new DbUpdateException(
                MensagemInterna,
                new PostgresException(MensagemInterna, "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation)),
            _ => new InvalidOperationException(MensagemInterna),
        };
    }
}
