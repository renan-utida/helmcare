using System;
using System.IO;
using System.Net;
using System.Threading.RateLimiting;
using HelmCare.Api.Contratos.Validacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HelmCare.Api.Configuracao;

/// <summary>
/// As duas regras de implantação da seção 1 do contrato (chaves do Data Protection persistentes e
/// cabeçalhos encaminhados) e o limite de logins por IP, em métodos estáticos que o
/// <c>Program</c> chama e os testes exercitam sem subir a API.
/// </summary>
public static class ConfiguracaoIdentidade
{
    /// <summary>
    /// Nome da política do limitador usada no login do entregador, no do socorrista e na renovação do token.
    /// </summary>
    public const string PoliticaLogin = "login";

    /// <summary>
    /// Nome da aplicação no Data Protection. Fixo, para as chaves valerem entre versões e pastas
    /// diferentes da mesma API.
    /// </summary>
    public const string NomeAplicacao = "HelmCare.Api";

    /// <summary>
    /// Regras do Identity da seção 3 do contrato: senha de 8 a 64 caracteres com maiúscula, minúscula e
    /// dígito (sem símbolo obrigatório; o máximo fica no DTO), e-mail único e bloqueio de 15 min depois
    /// de 5 tentativas erradas, também nas contas novas.
    /// </summary>
    /// <param name="opcoes">Opções do Identity.</param>
    public static void ConfigurarIdentity(IdentityOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        opcoes.Password.RequiredLength = SenhaAttribute.TamanhoMinimo;
        opcoes.Password.RequireUppercase = true;
        opcoes.Password.RequireLowercase = true;
        opcoes.Password.RequireDigit = true;
        opcoes.Password.RequireNonAlphanumeric = false;
        opcoes.Password.RequiredUniqueChars = 1;

        opcoes.User.RequireUniqueEmail = true;

        // O nome de usuário é o próprio e-mail, já validado pelo DTO e pelo Identity; a lista padrão de
        // caracteres recusaria e-mails válidos (com apóstrofo, por exemplo). Vazia, não restringe.
        opcoes.User.AllowedUserNameCharacters = string.Empty;

        opcoes.Lockout.MaxFailedAccessAttempts = 5;
        opcoes.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        opcoes.Lockout.AllowedForNewUsers = true;
    }

    /// <summary>
    /// Decide o diretório das chaves: o configurado, se houver; em Development, o padrão no perfil do
    /// usuário (fora do repositório e da pasta publicada); nos outros ambientes, a API não sobe.
    /// </summary>
    /// <param name="configurado">Valor de <see cref="OpcoesHelmCare.DiretorioChavesDataProtection"/>.</param>
    /// <param name="ambiente">Ambiente da aplicação.</param>
    /// <returns>O caminho absoluto do diretório.</returns>
    /// <exception cref="InvalidOperationException">Fora de Development, sem o caminho configurado.</exception>
    public static string ResolverDiretorioChaves(string? configurado, IHostEnvironment ambiente)
    {
        ArgumentNullException.ThrowIfNull(ambiente);

        if (!string.IsNullOrWhiteSpace(configurado))
        {
            return Path.GetFullPath(configurado);
        }

        if (ambiente.IsDevelopment())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HelmCare",
                "chaves-data-protection");
        }

        throw new InvalidOperationException(
            "Configure o diretório das chaves do Data Protection na variável HelmCare__DiretorioChavesDataProtection, "
            + "num caminho persistente fora da pasta publicada (ex.: /var/lib/helmcare/chaves-data-protection). "
            + "Sem ele, cada reinício da API invalida todos os tokens.");
    }

    /// <summary>
    /// Registra o Data Protection com o nome fixo da aplicação e as chaves gravadas no diretório.
    /// </summary>
    /// <param name="servicos">Serviços da aplicação.</param>
    /// <param name="diretorio">Diretório persistente das chaves.</param>
    public static void AdicionarDataProtection(IServiceCollection servicos, string diretorio)
    {
        servicos.AddDataProtection()
            .SetApplicationName(NomeAplicacao)
            .PersistKeysToFileSystem(new DirectoryInfo(diretorio));
    }

    /// <summary>
    /// Configura os cabeçalhos encaminhados: <c>X-Forwarded-For</c> e <c>X-Forwarded-Proto</c>, aceitos
    /// só quando a conexão vem de um dos proxies confiáveis.
    /// </summary>
    /// <param name="opcoes">Opções do middleware de cabeçalhos encaminhados.</param>
    /// <param name="proxiesConfiaveis">IPs separados por vírgula (ex.: <c>127.0.0.1,::1</c>).</param>
    /// <exception cref="InvalidOperationException">Quando algum item não é um IP.</exception>
    public static void ConfigurarProxies(ForwardedHeadersOptions opcoes, string proxiesConfiaveis)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(proxiesConfiaveis);

        opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // O padrão do ASP.NET Core já confia no loopback; a lista é refeita para valer só o configurado.
        opcoes.KnownProxies.Clear();
        opcoes.KnownIPNetworks.Clear();

        foreach (string item in proxiesConfiaveis.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!IPAddress.TryParse(item, out IPAddress? ip))
            {
                throw new InvalidOperationException("HelmCare:ProxiesConfiaveis deve ter só IPs separados por vírgula.");
            }

            opcoes.KnownProxies.Add(ip);
        }
    }

    /// <summary>
    /// Registra o limitador: janela fixa por IP do cliente, sem fila, e <c>429</c> em ProblemDetails
    /// acima do limite. A política só vale nas rotas marcadas com ela.
    /// </summary>
    /// <param name="servicos">Serviços da aplicação.</param>
    /// <param name="limitePorMinuto">Requisições aceitas por minuto para cada IP.</param>
    public static void AdicionarLimiteLogins(IServiceCollection servicos, int limitePorMinuto)
    {
        servicos.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.AddPolicy(PoliticaLogin, contexto => ParticaoPorIp(contexto, limitePorMinuto));
            opcoes.OnRejected = async (rejeicao, cancellationToken) =>
            {
                HttpContext http = rejeicao.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                IProblemDetailsService problemas = http.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemas.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Muitas tentativas",
                        Detail = "Limite de tentativas por minuto atingido. Espere um pouco e tente de novo.",
                        Instance = http.Request.Path,
                    },
                });
            };
        });
    }

    /// <summary>
    /// Partição do limitador: uma janela fixa de 1 min para cada IP do cliente. O IP já vem corrigido
    /// pelos cabeçalhos encaminhados, que rodam antes; sem isso, atrás do nginx, todos os usuários
    /// cairiam na mesma partição (o IP do proxy).
    /// </summary>
    /// <param name="contexto">Requisição.</param>
    /// <param name="limitePorMinuto">Requisições aceitas por minuto.</param>
    /// <returns>A partição do IP da requisição.</returns>
    public static RateLimitPartition<string> ParticaoPorIp(HttpContext contexto, int limitePorMinuto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        string ip = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";

        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limitePorMinuto,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }
}
