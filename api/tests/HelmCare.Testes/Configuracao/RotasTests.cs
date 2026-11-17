using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Configuracao;

/// <summary>
/// Testes de roteamento em memória, sem servidor nem rede: o pipeline de roteamento real do ASP.NET
/// Core com os controllers da API, só para ver qual ação cada caminho escolhe.
/// </summary>
[TestClass]
public sealed class RotasTests
{
    /// <summary>
    /// Garante que <c>/entregadores/sessao</c> não colide com <c>/entregadores/{id:guid}</c>: cada um
    /// vai para a própria ação.
    /// </summary>
    /// <param name="caminho">Caminho pedido.</param>
    /// <param name="acao">Ação esperada.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("/api/v1/entregadores/sessao", nameof(EntregadoresController.ObterSessao))]
    [DataRow("/api/v1/entregadores/3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90", nameof(EntregadoresController.Obter))]
    public async Task Sessao_NaoColideComOId(string caminho, string acao)
    {
        // Act
        string? escolhida = await RotaAsync(Environments.Production, "GET", caminho);

        // Assert
        Assert.IsNotNull(escolhida);
        Assert.Contains($"EntregadoresController.{acao} ", escolhida);
    }

    /// <summary>
    /// Garante que as rotas públicas são exatamente as do contrato (seção 7, 1.15), mais a de
    /// desenvolvimento: todas as outras ações dos controllers exigem login.
    /// </summary>
    [TestMethod]
    public void RotasPublicas_SaoAsDoContrato()
    {
        // Arrange
        string[] esperadas =
        [
            "DesenvolvimentoController.CriarContaEntregador",
            "EntregadoresController.Criar",
            "EntregadoresController.Entrar",
            "EntregadoresController.RenovarToken",
            "TrechosCriticosController.Listar",
        ];

        // Act
        string[] publicas = typeof(EntregadoresController).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ControllerBase)))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => t.IsDefined(typeof(AllowAnonymousAttribute)) || m.IsDefined(typeof(AllowAnonymousAttribute)))
                .Select(m => $"{t.Name}.{m.Name}"))
            .Order(System.StringComparer.Ordinal)
            .ToArray();

        // Assert
        CollectionAssert.AreEqual(esperadas, publicas);
    }

    /// <summary>
    /// Garante que todas as rotas com id do entregador usam a restrição <c>:guid</c>.
    /// </summary>
    [TestMethod]
    public void RotasComId_UsamARestricaoGuid()
    {
        // Arrange
        string[] modelos = typeof(EntregadoresController).GetMethods()
            .SelectMany(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>())
            .Select(a => a.Template ?? string.Empty)
            .Where(t => t.Contains('{', System.StringComparison.Ordinal))
            .ToArray();

        // Assert
        Assert.IsNotEmpty(modelos);
        Assert.IsTrue(modelos.All(t => t.Contains("{id:guid}", System.StringComparison.Ordinal)));
    }

    /// <summary>
    /// Garante que a rota de desenvolvimento existe em Development e não existe nos outros ambientes.
    /// </summary>
    /// <param name="ambiente">Ambiente.</param>
    /// <param name="mapeada">Se a rota deve existir.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("Development", true)]
    [DataRow("Production", false)]
    [DataRow("Staging", false)]
    public async Task RotaDeDesenvolvimento_SoEmDevelopment(string ambiente, bool mapeada)
    {
        // Act
        string? escolhida = await RotaAsync(
            ambiente,
            "POST",
            "/api/v1/dev/entregadores/3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90/conta");

        // Assert
        Assert.AreEqual(mapeada, escolhida?.Contains(nameof(DesenvolvimentoController.CriarContaEntregador), System.StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// Garante o <c>Cache-Control: no-store</c> nas duas rotas da ficha médica.
    /// </summary>
    /// <param name="acao">Ação da ficha.</param>
    [TestMethod]
    [DataRow(nameof(EntregadoresController.ObterFichaMedica))]
    [DataRow(nameof(EntregadoresController.DefinirFichaMedica))]
    public void FichaMedica_SemCache(string acao)
    {
        // Act
        ResponseCacheAttribute? cache = typeof(EntregadoresController).GetMethod(acao)?.GetCustomAttribute<ResponseCacheAttribute>();

        // Assert
        Assert.IsNotNull(cache);
        Assert.IsTrue(cache.NoStore);
        Assert.AreEqual(ResponseCacheLocation.Any, cache.Location);
    }

    // Monta só o roteamento e os controllers da API (com o provedor por ambiente) e devolve o nome da
    // ação que o caminho escolheu, ou nulo se nenhuma.
    private static async Task<string?> RotaAsync(string ambiente, string metodo, string caminho)
    {
        WebApplicationBuilder construtor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = ambiente });
        ProvedorControllersPorAmbiente.Aplicar(
            construtor.Services.AddControllers().AddApplicationPart(typeof(EntregadoresController).Assembly),
            construtor.Environment.IsDevelopment());

        await using WebApplication app = construtor.Build();

        string? escolhida = null;
        app.UseRouting();
        app.Use((HttpContext contexto, RequestDelegate _) =>
        {
            escolhida = contexto.GetEndpoint()?.DisplayName;
            return Task.CompletedTask;
        });
        app.MapControllers();

        RequestDelegate pipeline = ((IApplicationBuilder)app).Build();
        var requisicao = new DefaultHttpContext { RequestServices = app.Services };
        requisicao.Request.Method = metodo;
        requisicao.Request.Path = caminho;

        await pipeline(requisicao);
        return escolhida;
    }
}
