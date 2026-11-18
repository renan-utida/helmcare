using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Configuracao;

/// <summary>
/// Testes da página do Cofre (seção 7 do contrato, 1.16), com o pipeline de roteamento real em memória
/// e os arquivos reais de <c>wwwroot/cofre</c>: a mesma resposta para qualquer <c>qrToken</c>, os
/// cabeçalhos de segurança, os assets fora da rota do <c>qrToken</c> e o HTML sem dados nem script inline.
/// </summary>
[TestClass]
public sealed class PaginaCofreTests
{
    private static readonly string _wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");

    /// <summary>
    /// Garante a mesma resposta (status, tipo e corpo) para um qrToken com formato válido e um inexistente,
    /// sem nenhum dos dois no HTML.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task Pagina_QrValidoOuInexistente_MesmaResposta()
    {
        // Act
        Resposta valido = await PedirAsync("GET", "/cofre/kB4mZ2xQ9rT1vW6yA3cE8gJ5nP0sU7hL2dF4jK9mN1q");
        Resposta inexistente = await PedirAsync("GET", "/cofre/nao-existe");

        // Assert
        Assert.AreEqual(200, valido.Status);
        Assert.AreEqual(valido.Status, inexistente.Status);
        Assert.AreEqual(valido.Tipo, inexistente.Tipo);
        Assert.AreEqual(valido.Corpo, inexistente.Corpo);
        Assert.StartsWith("text/html", valido.Tipo);
        Assert.DoesNotContain("kB4mZ2xQ9rT1", valido.Corpo);
        Assert.DoesNotContain("nao-existe", inexistente.Corpo);
        Assert.AreEqual(PaginaCofre.NomePagina, valido.Endpoint);
    }

    /// <summary>
    /// Garante os cabeçalhos de segurança na página e nos dois assets.
    /// </summary>
    /// <param name="caminho">Caminho pedido.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("/cofre/qualquer-token")]
    [DataRow("/cofre/assets/cofre.js")]
    [DataRow("/cofre/assets/cofre.css")]
    public async Task Pagina_ECadaAsset_TemOsCabecalhosDeSeguranca(string caminho)
    {
        // Act
        Resposta resposta = await PedirAsync("GET", caminho);

        // Assert
        Assert.AreEqual(200, resposta.Status);
        Assert.AreEqual("default-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'", resposta.Cabecalhos["Content-Security-Policy"].ToString());
        Assert.AreEqual("no-referrer", resposta.Cabecalhos["Referrer-Policy"].ToString());
        Assert.AreEqual("nosniff", resposta.Cabecalhos["X-Content-Type-Options"].ToString());
        Assert.AreEqual("DENY", resposta.Cabecalhos["X-Frame-Options"].ToString());
        Assert.AreEqual("no-store", resposta.Cabecalhos["Cache-Control"].ToString());
    }

    /// <summary>
    /// Garante que os assets não caem na rota do qrToken (cada um tem o próprio tipo e conteúdo) e que a
    /// página não colide com a rota JSON do Cofre.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task Rotas_AssetsEApiNaoCaemNaPagina()
    {
        // Act
        Resposta js = await PedirAsync("GET", "/cofre/assets/cofre.js");
        Resposta css = await PedirAsync("GET", "/cofre/assets/cofre.css");
        Resposta api = await PedirAsync("POST", "/api/v1/cofre/qualquer-token/acesso", executar: false);
        Resposta auditoria = await PedirAsync("GET", "/api/v1/cofre/auditoria/3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90", executar: false);

        // Assert
        Assert.StartsWith("text/javascript", js.Tipo);
        Assert.Contains("use strict", js.Corpo);
        Assert.StartsWith("text/css", css.Tipo);
        Assert.AreNotEqual(PaginaCofre.NomePagina, js.Endpoint);
        Assert.Contains($"{nameof(CofreController)}.{nameof(CofreController.Acessar)}", api.Endpoint ?? string.Empty);
        Assert.Contains($"{nameof(CofreController)}.{nameof(CofreController.ListarAuditoria)}", auditoria.Endpoint ?? string.Empty);
    }

    /// <summary>
    /// Garante o HTML estático: sem script inline (só com src), sem atributo de evento, sem estilo inline,
    /// sem endereço externo (CDN) e com o botão do SAMU 192.
    /// </summary>
    [TestMethod]
    public void Html_Estatico_SemScriptInlineNemCdn()
    {
        // Arrange
        string html = File.ReadAllText(Path.Combine(_wwwroot, "cofre", "index.html"));

        // Assert
        Assert.DoesNotMatchRegex(new Regex("<script(?![^>]*\\bsrc=)[^>]*>", RegexOptions.IgnoreCase), html);
        Assert.DoesNotMatchRegex(new Regex("\\son[a-z]+\\s*=", RegexOptions.IgnoreCase), html);
        Assert.DoesNotMatchRegex(new Regex("\\sstyle\\s*=", RegexOptions.IgnoreCase), html);
        Assert.DoesNotMatchRegex(new Regex("(src|href)=\"(https?:)?//", RegexOptions.IgnoreCase), html);
        Assert.Contains("href=\"tel:192\"", html);
        Assert.Contains("Ligar para o SAMU 192", html);
    }

    /// <summary>
    /// Garante que o JavaScript guarda o token só na memória (sem localStorage, sessionStorage ou cookie)
    /// e escreve os dados como texto (sem innerHTML).
    /// </summary>
    [TestMethod]
    public void Script_TokenSoNaMemoria()
    {
        // Arrange
        string js = File.ReadAllText(Path.Combine(_wwwroot, "cofre", "assets", "cofre.js"));

        // Assert
        Assert.DoesNotContain("localStorage", js);
        Assert.DoesNotContain("sessionStorage", js);
        Assert.DoesNotContain("document.cookie", js);
        Assert.DoesNotContain("innerHTML", js);
        Assert.Contains("/api/v1/socorristas/login", js);
        Assert.Contains("/acesso", js);
    }

    // Monta o roteamento com a página e os controllers e devolve o endpoint escolhido. Com executar,
    // roda o endpoint (a página e os assets não dependem de serviço nenhum).
    private static async Task<Resposta> PedirAsync(string metodo, string caminho, bool executar = true)
    {
        WebApplicationBuilder construtor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        construtor.Services.AddControllers().AddApplicationPart(typeof(CofreController).Assembly);

        await using WebApplication app = construtor.Build();
        using var arquivos = new PhysicalFileProvider(_wwwroot);

        string? escolhido = null;
        app.UseRouting();
        app.Use(async (HttpContext contexto, RequestDelegate proximo) =>
        {
            escolhido = contexto.GetEndpoint()?.DisplayName;
            if (executar && contexto.GetEndpoint()?.RequestDelegate is { } endpoint)
            {
                await endpoint(contexto);
            }
        });
        PaginaCofre.Mapear(app, arquivos);
        app.MapControllers();

        RequestDelegate pipeline = ((IApplicationBuilder)app).Build();
        var requisicao = new DefaultHttpContext { RequestServices = app.Services };
        requisicao.Request.Method = metodo;
        requisicao.Request.Path = caminho;
        using var corpo = new MemoryStream();
        requisicao.Response.Body = corpo;

        await pipeline(requisicao);

        return new Resposta(
            requisicao.Response.StatusCode,
            requisicao.Response.ContentType ?? string.Empty,
            Encoding.UTF8.GetString(corpo.ToArray()),
            requisicao.Response.Headers,
            escolhido);
    }

    private sealed class Resposta
    {
        public Resposta(int status, string tipo, string corpo, IHeaderDictionary cabecalhos, string? endpoint)
        {
            Status = status;
            Tipo = tipo;
            Corpo = corpo;
            Cabecalhos = cabecalhos;
            Endpoint = endpoint;
        }

        public int Status { get; }

        public string Tipo { get; }

        public string Corpo { get; }

        public IHeaderDictionary Cabecalhos { get; }

        public string? Endpoint { get; }
    }
}
