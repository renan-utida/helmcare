using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;

namespace HelmCare.Api.Configuracao;

/// <summary>
/// Página web do Cofre (seção 7 do contrato, 1.16), fora do <c>/api/v1</c>: <c>GET /cofre/{qrToken}</c>
/// devolve sempre o mesmo HTML estático de <c>wwwroot/cofre/index.html</c>, exista o <c>qrToken</c> ou
/// não, sem nenhum dado no HTML. O JavaScript e o CSS ficam em <c>/cofre/assets/</c>, servidos pela
/// própria API, sem CDN. A página lê o <c>qrToken</c> do endereço e chama a API.
/// </summary>
/// <remarks>
/// As três rotas são públicas e ficam fora do Scalar. Nenhuma lê o <c>qrToken</c> nem o registra.
/// </remarks>
public static class PaginaCofre
{
    /// <summary>
    /// Nome de exibição do endpoint da página (usado nos testes de roteamento).
    /// </summary>
    public const string NomePagina = "Página do Cofre";

    /// <summary>
    /// Política de conteúdo: só recursos da própria origem, sem script inline, sem moldura e sem base.
    /// </summary>
    public const string PoliticaConteudo = "default-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";

    /// <summary>
    /// Mapeia a página e os dois assets.
    /// </summary>
    /// <param name="rotas">Construtor de rotas da aplicação.</param>
    /// <param name="arquivos">Arquivos do <c>wwwroot</c>.</param>
    public static void Mapear(IEndpointRouteBuilder rotas, IFileProvider arquivos)
    {
        ArgumentNullException.ThrowIfNull(rotas);
        ArgumentNullException.ThrowIfNull(arquivos);

        // O qrToken fica no endereço só para o JavaScript ler; o servidor nem o usa.
        rotas.MapGet("/cofre/{qrToken}", (HttpContext contexto) => EnviarAsync(contexto, arquivos, "cofre/index.html", "text/html; charset=utf-8"))
            .WithDisplayName(NomePagina)
            .AllowAnonymous()
            .ExcludeFromDescription();

        rotas.MapGet("/cofre/assets/cofre.js", (HttpContext contexto) => EnviarAsync(contexto, arquivos, "cofre/assets/cofre.js", "text/javascript; charset=utf-8"))
            .WithDisplayName("Script do Cofre")
            .AllowAnonymous()
            .ExcludeFromDescription();

        rotas.MapGet("/cofre/assets/cofre.css", (HttpContext contexto) => EnviarAsync(contexto, arquivos, "cofre/assets/cofre.css", "text/css; charset=utf-8"))
            .WithDisplayName("Estilo do Cofre")
            .AllowAnonymous()
            .ExcludeFromDescription();
    }

    /// <summary>
    /// Aplica os cabeçalhos de segurança da página e dos assets.
    /// </summary>
    /// <param name="resposta">Resposta HTTP.</param>
    public static void AplicarCabecalhos(HttpResponse resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        resposta.Headers.ContentSecurityPolicy = PoliticaConteudo;
        resposta.Headers["Referrer-Policy"] = "no-referrer";
        resposta.Headers.XContentTypeOptions = "nosniff";
        resposta.Headers.XFrameOptions = "DENY";
        resposta.Headers.CacheControl = "no-store";
    }

    private static async Task EnviarAsync(HttpContext contexto, IFileProvider arquivos, string caminho, string tipo)
    {
        IFileInfo arquivo = arquivos.GetFileInfo(caminho);
        AplicarCabecalhos(contexto.Response);

        if (!arquivo.Exists)
        {
            contexto.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        contexto.Response.ContentType = tipo;
        contexto.Response.ContentLength = arquivo.Length;
        await contexto.Response.SendFileAsync(arquivo, contexto.RequestAborted);
    }
}
