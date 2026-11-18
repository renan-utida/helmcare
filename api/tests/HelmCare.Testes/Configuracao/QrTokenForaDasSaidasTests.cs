using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Configuracao;

/// <summary>
/// Testes da seção 9 do contrato (1.16) para o <c>qrToken</c>, que é credencial: ele nunca sai num
/// ProblemDetails (campo <c>instance</c>), no log do tratador de exceções ou nos logs de requisição.
/// </summary>
[TestClass]
public sealed class QrTokenForaDasSaidasTests
{
    private const string QrToken = "kB4mZ2xQ9rT1vW6yA3cE8gJ5nP0sU7hL2dF4jK9mN1q";

    /// <summary>
    /// Garante a troca do qrToken pelo marcador só nos caminhos do Cofre que o levam.
    /// </summary>
    /// <param name="caminho">Caminho da requisição.</param>
    /// <param name="esperado">Caminho seguro.</param>
    [TestMethod]
    [DataRow("/cofre/" + QrToken, "/cofre/{qrToken}")]
    [DataRow("/api/v1/cofre/" + QrToken + "/acesso", "/api/v1/cofre/{qrToken}/acesso")]
    [DataRow("/cofre/assets/cofre.js", "/cofre/assets/cofre.js")]
    [DataRow("/api/v1/cofre/auditoria/3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90", "/api/v1/cofre/auditoria/3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90")]
    [DataRow("/api/v1/entregadores/sessao", "/api/v1/entregadores/sessao")]
    public void SemQrToken_Caminhos_TrocaSoOToken(string caminho, string esperado)
    {
        // Act
        string? seguro = CaminhoSeguro.SemQrToken(caminho);

        // Assert
        Assert.AreEqual(esperado, seguro);
    }

    /// <summary>
    /// Garante que os erros do acesso ao Cofre (404, 403 com motivo, 401) saem sem o qrToken no
    /// ProblemDetails e no log do tratador, e que o 403 da regra do socorrista traz o motivo.
    /// </summary>
    /// <param name="caso">Exceção lançada.</param>
    /// <param name="status">Status esperado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("404", 404)]
    [DataRow("expirado", 403)]
    [DataRow("401", 401)]
    public async Task TryHandleAsync_RotaDoCofre_SemQrTokenNaSaida(string caso, int status)
    {
        // Arrange
        var problemas = new FakeProblemDetailsService();
        var logger = new FakeLogger<TratadorExcecoes>();
        var tratador = new TratadorExcecoes(problemas, logger);
        var contexto = new DefaultHttpContext();
        contexto.Request.Path = "/api/v1/cofre/" + QrToken + "/acesso";
        Exception excecao = caso switch
        {
            "404" => new RecursoNaoEncontradoException("QR Code inválido ou inexistente."),
            "expirado" => new AcessoCofreNegadoException(MotivoNegacaoCofre.AcessoExpirado),
            _ => new NaoAutenticadoException("Faça o login."),
        };

        // Act
        await tratador.TryHandleAsync(contexto, excecao, CancellationToken.None);

        // Assert
        string saida = JsonSerializer.Serialize(problemas.Escrito) + string.Join("|", logger.Registros.Select(r => r.Mensagem));
        Assert.AreEqual(status, contexto.Response.StatusCode);
        Assert.AreEqual("/api/v1/cofre/{qrToken}/acesso", problemas.Escrito?.Instance);
        Assert.DoesNotContain(QrToken, saida);
        if (caso == "expirado")
        {
            Assert.AreEqual("ACESSO_EXPIRADO", problemas.Escrito?.Extensions["motivo"]?.ToString());
        }
    }

    /// <summary>
    /// Garante que o 401 só com status (do esquema bearer) na rota do Cofre também sai sem o qrToken.
    /// </summary>
    [TestMethod]
    public void Completar_SoStatusNoCofre_InstanceSemQrToken()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/v1/cofre/" + QrToken + "/acesso";
        var contexto = new ProblemDetailsContext { HttpContext = http, ProblemDetails = { Status = 401 } };

        // Act
        ProblemasPadrao.Completar(contexto);

        // Assert
        Assert.AreEqual("/api/v1/cofre/{qrToken}/acesso", contexto.ProblemDetails.Instance);
    }

    /// <summary>
    /// Garante a configuração de log que mantém o caminho fora do log: os logs de requisição do ASP.NET
    /// Core (que levam o caminho) ficam abaixo do nível registrado, e o console não registra os escopos
    /// (o escopo da requisição também leva o caminho).
    /// </summary>
    /// <param name="arquivo">Arquivo de configuração.</param>
    [TestMethod]
    [DataRow("appsettings.json")]
    [DataRow("appsettings.Development.json")]
    public void Configuracao_DeLog_NaoRegistraOCaminho(string arquivo)
    {
        // Arrange
        string caminho = Path.Combine(RaizDaApi(), "src", "HelmCare.Api", arquivo);
        using JsonDocument documento = JsonDocument.Parse(File.ReadAllText(caminho));
        JsonElement niveis = documento.RootElement.GetProperty("Logging").GetProperty("LogLevel");

        // Assert
        Assert.AreEqual("Warning", niveis.GetProperty("Microsoft.AspNetCore").GetString());
        Assert.DoesNotContain("IncludeScopes", File.ReadAllText(caminho));
    }

    private static string RaizDaApi()
    {
        DirectoryInfo? pasta = new(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "HelmCare.slnx")))
        {
            pasta = pasta.Parent;
        }

        return pasta?.FullName ?? throw new InvalidOperationException("Pasta api/ não encontrada.");
    }
}
