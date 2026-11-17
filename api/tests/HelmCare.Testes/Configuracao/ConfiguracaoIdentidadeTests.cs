using System;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Seguranca;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Configuracao;

/// <summary>
/// Testes das duas regras de implantação da seção 1 do contrato (1.15) e do limite de logins: chaves
/// do Data Protection persistentes, cabeçalhos encaminhados, partição por IP e os ProblemDetails do
/// 401 e do 403 sem exceção.
/// </summary>
[TestClass]
public sealed class ConfiguracaoIdentidadeTests
{
    /// <summary>
    /// Garante que o caminho configurado vale em qualquer ambiente.
    /// </summary>
    [TestMethod]
    public void ResolverDiretorioChaves_Configurado_UsaOCaminho()
    {
        // Arrange
        string configurado = Path.Combine(Path.GetTempPath(), "helmcare-chaves");

        // Act
        string diretorio = ConfiguracaoIdentidade.ResolverDiretorioChaves(configurado, Ambiente(Environments.Production));

        // Assert
        Assert.AreEqual(Path.GetFullPath(configurado), diretorio);
    }

    /// <summary>
    /// Garante o padrão de Development: no perfil do usuário, fora do repositório e da pasta publicada.
    /// </summary>
    [TestMethod]
    public void ResolverDiretorioChaves_DevelopmentSemCaminho_UsaOPerfilDoUsuario()
    {
        // Act
        string diretorio = ConfiguracaoIdentidade.ResolverDiretorioChaves(null, Ambiente(Environments.Development));

        // Assert
        string esperado = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HelmCare",
            "chaves-data-protection");
        Assert.AreEqual(esperado, diretorio);
        Assert.IsFalse(diretorio.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Garante que, fora de Development, a API não sobe sem o caminho, com uma mensagem clara.
    /// </summary>
    /// <param name="ambiente">Ambiente sem o caminho configurado.</param>
    [TestMethod]
    [DataRow("Production")]
    [DataRow("Staging")]
    public void ResolverDiretorioChaves_ForaDeDevelopmentSemCaminho_Falha(string ambiente)
    {
        // Act
        InvalidOperationException excecao = Assert.ThrowsExactly<InvalidOperationException>(
            () => ConfiguracaoIdentidade.ResolverDiretorioChaves(" ", Ambiente(ambiente)));

        // Assert
        Assert.Contains("HelmCare__DiretorioChavesDataProtection", excecao.Message);
    }

    /// <summary>
    /// Garante que um reinício da API não invalida os tokens: o token gerado por um provedor é aberto
    /// por outro, montado depois com o mesmo diretório de chaves.
    /// </summary>
    [TestMethod]
    public void DataProtection_MesmoDiretorioDepoisDoReinicio_TokenContinuaValido()
    {
        // Arrange
        string diretorio = DiretorioTemporario();
        try
        {
            string token;
            using (ServiceProvider antes = Montar(diretorio))
            {
                token = Protetor(antes, TimeProvider.System).Emitir(Usuario()).AccessToken;
            }

            // Act: "reinício" da API, com um provedor novo e o mesmo diretório.
            using ServiceProvider depois = Montar(diretorio);
            ClaimsPrincipal? aberto = Protetor(depois, TimeProvider.System).AbrirAcesso(token);

            // Assert
            Assert.AreEqual("usuario-1", aberto?.FindFirstValue(ClaimTypes.NameIdentifier));
        }
        finally
        {
            Directory.Delete(diretorio, recursive: true);
        }
    }

    /// <summary>
    /// Garante o contrário: com outro diretório (chaves perdidas), o token deixa de valer. É o que
    /// aconteceria a cada reinício sem as chaves persistentes.
    /// </summary>
    [TestMethod]
    public void DataProtection_OutroDiretorio_TokenInvalido()
    {
        // Arrange
        string primeiro = DiretorioTemporario();
        string segundo = DiretorioTemporario();
        try
        {
            string token;
            using (ServiceProvider antes = Montar(primeiro))
            {
                token = Protetor(antes, TimeProvider.System).Emitir(Usuario()).AccessToken;
            }

            // Act
            using ServiceProvider depois = Montar(segundo);
            ClaimsPrincipal? aberto = Protetor(depois, TimeProvider.System).AbrirAcesso(token);

            // Assert
            Assert.IsNull(aberto);
        }
        finally
        {
            Directory.Delete(primeiro, recursive: true);
            Directory.Delete(segundo, recursive: true);
        }
    }

    /// <summary>
    /// Garante as validades: o token de acesso vence em 60 min e o de renovação vale 14 dias; um não
    /// serve no lugar do outro.
    /// </summary>
    [TestMethod]
    public void ProtetorTokens_Validades_60MinutosE14Dias()
    {
        // Arrange
        string diretorio = DiretorioTemporario();
        try
        {
            using ServiceProvider provedor = Montar(diretorio);
            var relogio = new FakeTimeProvider(new DateTimeOffset(2026, 11, 16, 20, 0, 0, TimeSpan.Zero));
            ProtetorTokens protetor = Protetor(provedor, relogio);
            TokensEmitidos tokens = protetor.Emitir(Usuario());

            // Act e Assert
            Assert.AreEqual(3600, tokens.ExpiresIn);
            Assert.IsNull(protetor.AbrirRenovacao(tokens.AccessToken));
            Assert.IsNull(protetor.AbrirAcesso(tokens.RefreshToken));

            relogio.Advance(TimeSpan.FromMinutes(59));
            Assert.IsNotNull(protetor.AbrirAcesso(tokens.AccessToken));

            relogio.Advance(TimeSpan.FromMinutes(1));
            Assert.IsNull(protetor.AbrirAcesso(tokens.AccessToken));
            Assert.IsNotNull(protetor.AbrirRenovacao(tokens.RefreshToken));

            relogio.Advance(TimeSpan.FromDays(14));
            Assert.IsNull(protetor.AbrirRenovacao(tokens.RefreshToken));
            Assert.IsNull(protetor.AbrirAcesso("adulterado"));
        }
        finally
        {
            Directory.Delete(diretorio, recursive: true);
        }
    }

    /// <summary>
    /// Garante que o X-Forwarded-For vale quando a conexão vem do proxy confiável (o nginx na mesma VM).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CabecalhosEncaminhados_ProxyConfiavel_UsaOIpDoCliente()
    {
        // Act
        HttpContext contexto = await PassarPeloMiddlewareAsync(IPAddress.Loopback, "203.0.113.7");

        // Assert
        Assert.AreEqual(IPAddress.Parse("203.0.113.7"), contexto.Connection.RemoteIpAddress);
        Assert.AreEqual("https", contexto.Request.Scheme);
    }

    /// <summary>
    /// Garante que o X-Forwarded-For de quem não é proxy confiável é ignorado (um cliente não escolhe o
    /// próprio IP para fugir do limite).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CabecalhosEncaminhados_OrigemNaoConfiavel_Ignora()
    {
        // Act
        HttpContext contexto = await PassarPeloMiddlewareAsync(IPAddress.Parse("198.51.100.20"), "203.0.113.7");

        // Assert
        Assert.AreEqual(IPAddress.Parse("198.51.100.20"), contexto.Connection.RemoteIpAddress);
    }

    /// <summary>
    /// Garante que um item que não é IP na lista de proxies impede a subida.
    /// </summary>
    [TestMethod]
    public void ConfigurarProxies_ItemInvalido_Falha()
    {
        // Act e Assert
        Assert.ThrowsExactly<InvalidOperationException>(
            () => ConfiguracaoIdentidade.ConfigurarProxies(new ForwardedHeadersOptions(), "127.0.0.1,nginx"));
    }

    /// <summary>
    /// Garante a partição do limitador por IP: o mesmo IP cai na mesma janela; IPs diferentes, em janelas
    /// diferentes. Com 10 por minuto, a 11ª do mesmo IP é recusada e a de outro IP passa.
    /// </summary>
    [TestMethod]
    public void ParticaoPorIp_DezPorMinuto_RecusaA11DoMesmoIp()
    {
        // Arrange
        HttpContext deA = ContextoDe("203.0.113.7");
        HttpContext deB = ContextoDe("203.0.113.8");
        RateLimitPartition<string> particaoA = ConfiguracaoIdentidade.ParticaoPorIp(deA, 10);
        RateLimitPartition<string> particaoB = ConfiguracaoIdentidade.ParticaoPorIp(deB, 10);
        using RateLimiter limitadorA = particaoA.Factory(particaoA.PartitionKey);
        using RateLimiter limitadorB = particaoB.Factory(particaoB.PartitionKey);

        // Act
        int aceitasA = 0;
        for (int i = 0; i < 11; i++)
        {
            using RateLimitLease permissao = limitadorA.AttemptAcquire();
            aceitasA += permissao.IsAcquired ? 1 : 0;
        }

        using RateLimitLease deOutroIp = limitadorB.AttemptAcquire();

        // Assert
        Assert.AreEqual("203.0.113.7", particaoA.PartitionKey);
        Assert.AreEqual(ConfiguracaoIdentidade.ParticaoPorIp(ContextoDe("203.0.113.7"), 10).PartitionKey, particaoA.PartitionKey);
        Assert.AreNotEqual(particaoA.PartitionKey, particaoB.PartitionKey);
        Assert.AreEqual(10, aceitasA);
        Assert.IsTrue(deOutroIp.IsAcquired);
    }

    /// <summary>
    /// Garante o 401 e o 403 do esquema bearer (só status) em ProblemDetails com textos em português e
    /// a rota em <c>instance</c>; um ProblemDetails que já tem detalhe fica como está.
    /// </summary>
    /// <param name="status">Status da resposta.</param>
    /// <param name="titulo">Título esperado.</param>
    [TestMethod]
    [DataRow(401, ProblemasPadrao.TituloNaoAutenticado)]
    [DataRow(403, ProblemasPadrao.TituloAcessoNegado)]
    [DataRow(404, "Recurso não encontrado")]
    public void Completar_SoStatus_TextosEmPortugues(int status, string titulo)
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/v1/entregadores/sessao";
        var contexto = new ProblemDetailsContext { HttpContext = http, ProblemDetails = { Status = status } };
        var jaTratado = new ProblemDetailsContext { HttpContext = http, ProblemDetails = { Status = 403, Title = "X", Detail = "Y" } };

        // Act
        ProblemasPadrao.Completar(contexto);
        ProblemasPadrao.Completar(jaTratado);

        // Assert
        Assert.AreEqual(titulo, contexto.ProblemDetails.Title);
        Assert.IsFalse(string.IsNullOrEmpty(contexto.ProblemDetails.Detail));
        Assert.AreEqual("/api/v1/entregadores/sessao", contexto.ProblemDetails.Instance);
        Assert.AreEqual("Y", jaTratado.ProblemDetails.Detail);
    }

    private static async Task<HttpContext> PassarPeloMiddlewareAsync(IPAddress origem, string cliente)
    {
        var opcoes = new ForwardedHeadersOptions();
        ConfiguracaoIdentidade.ConfigurarProxies(opcoes, new OpcoesHelmCare().ProxiesConfiaveis);
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(opcoes));

        var contexto = new DefaultHttpContext();
        contexto.Connection.RemoteIpAddress = origem;
        contexto.Request.Scheme = "http";
        contexto.Request.Headers["X-Forwarded-For"] = cliente;
        contexto.Request.Headers["X-Forwarded-Proto"] = "https";

        await middleware.Invoke(contexto);
        return contexto;
    }

    private static DefaultHttpContext ContextoDe(string ip)
    {
        var contexto = new DefaultHttpContext();
        contexto.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return contexto;
    }

    private static HostingEnvironment Ambiente(string nome)
    {
        return new HostingEnvironment { EnvironmentName = nome };
    }

    private static string DiretorioTemporario()
    {
        string diretorio = Path.Combine(Path.GetTempPath(), "helmcare-dp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(diretorio);
        return diretorio;
    }

    // Os mesmos serviços que o Program registra para os tokens: Data Protection com o diretório e o
    // esquema bearer do Identity (que monta os protetores a partir dele).
    private static ServiceProvider Montar(string diretorio)
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        ConfiguracaoIdentidade.AdicionarDataProtection(servicos, diretorio);
        servicos.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme);
        return servicos.BuildServiceProvider();
    }

    private static ProtetorTokens Protetor(ServiceProvider provedor, TimeProvider relogio)
    {
        BearerTokenOptions opcoes = provedor.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(IdentityConstants.BearerScheme);
        return new ProtetorTokens(opcoes, relogio);
    }

    private static ClaimsPrincipal Usuario()
    {
        var identidade = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "usuario-1"), new Claim(ClaimTypes.Role, Papeis.Entregador)],
            IdentityConstants.BearerScheme);
        return new ClaimsPrincipal(identidade);
    }
}
