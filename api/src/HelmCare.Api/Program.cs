using System;
using System.Text.Json;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Integracoes.ServicoRisco;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Mensageria.Processadores;
using HelmCare.Api.Persistencia;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Cadastro;
using HelmCare.Api.Servicos.Identidade;
using HelmCare.Api.Servicos.Ocorrencias;
using HelmCare.Api.Servicos.Risco;
using HelmCare.Api.Servicos.TrechosCriticos;
using HelmCare.Api.Servicos.Turnos;
using HelmCare.Api.Servicos.Voluntarios;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// A connection string tem senha, então nunca fica em appsettings*.json:
// vem do user-secrets (Development) ou da variável de ambiente ConnectionStrings__HelmCareApi.
string connectionString = builder.Configuration.GetConnectionString("HelmCareApi")
    ?? throw new InvalidOperationException(
        "Connection string 'HelmCareApi' não configurada. Em Development, use "
        + "'dotnet user-secrets set ConnectionStrings:HelmCareApi \"...\" --project src/HelmCare.Api'; "
        + "nos outros ambientes, defina a variável ConnectionStrings__HelmCareApi.");

// Constantes da seção 5 do contrato (seção "HelmCare" do appsettings.json, trocáveis por
// variável de ambiente). ValidateOnStart: valor fora da faixa impede a API de subir.
builder.Services.AddOptions<OpcoesHelmCare>()
    .Bind(builder.Configuration.GetSection(OpcoesHelmCare.Secao))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Os valores que configuram o próprio host (chaves, proxies e limite de logins) são lidos antes do
// Build; a validação de faixa acima continua valendo na subida.
OpcoesHelmCare opcoesIniciais = builder.Configuration.GetSection(OpcoesHelmCare.Secao).Get<OpcoesHelmCare>()
    ?? new OpcoesHelmCare();

// Regra de implantação da seção 1: chaves do Data Protection num diretório persistente, fora da
// pasta publicada. Sem isso, cada reinício da API invalidaria todos os tokens.
ConfiguracaoIdentidade.AdicionarDataProtection(
    builder.Services,
    ConfiguracaoIdentidade.ResolverDiretorioChaves(opcoesIniciais.DiretorioChavesDataProtection, builder.Environment));

// Conexão com o broker MQTT (seção "Mqtt", sem segredo). Mqtt__Habilitado=false sobe a API sem broker.
builder.Services.AddOptions<OpcoesMqtt>()
    .Bind(builder.Configuration.GetSection(OpcoesMqtt.Secao))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Relógio único da aplicação. Os serviços recebem TimeProvider e nunca leem DateTime.UtcNow;
// nos testes, entra o FakeTimeProvider.
builder.Services.AddSingleton(TimeProvider.System);

// EF Core com PostgreSQL (banco helmcare_api).
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Identity com o esquema bearer próprio (token opaco, protegido pelo Data Protection; sem JWT). Sem
// MapIdentityApi: login, renovação e sessão são rotas nossas, com o corpo do contrato. A fábrica de
// claims põe o papel e o id do perfil no token, igual no login e na renovação.
builder.Services.AddIdentityApiEndpoints<IdentityUser>(ConfiguracaoIdentidade.ConfigurarIdentity)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<FabricaClaimsUsuario>();

// Validade dos tokens (configuração interna, fora da seção 5): 60 min e 14 dias por padrão.
builder.Services.AddOptions<BearerTokenOptions>(IdentityConstants.BearerScheme)
    .Configure<IOptions<OpcoesHelmCare>>((bearer, helmcare) =>
    {
        bearer.BearerTokenExpiration = TimeSpan.FromMinutes(helmcare.Value.ValidadeAccessTokenMin);
        bearer.RefreshTokenExpiration = TimeSpan.FromDays(helmcare.Value.ValidadeRefreshTokenDias);
    });

// O esquema padrão é o bearer (401 e 403 sem redirecionar). O cookie que o AddIdentityApiEndpoints
// também registra nunca é emitido, mas, por garantia, também não redireciona.
builder.Services.ConfigureApplicationCookie(cookie =>
{
    cookie.Events.OnRedirectToLogin = contexto =>
    {
        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    cookie.Events.OnRedirectToAccessDenied = contexto =>
    {
        contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();

// Quem fez a requisição e as regras de quem pode o quê (seção 7, 1.15). Os serviços conferem o dono
// depois de achar o recurso (401, 404, 403); o MQTT e o laço em segundo plano não passam por aqui.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtualHttp>();
builder.Services.AddScoped<IAutorizacaoRecursos, AutorizacaoRecursos>();
builder.Services.AddScoped<IContasIdentity, ContasIdentity>();
builder.Services.AddSingleton<SenhaReferencia>();
builder.Services.AddScoped<IEmissorTokens, EmissorTokensIdentity>();
builder.Services.AddScoped<AutenticacaoEntregadorServico>();

// Regra de implantação da seção 1: atrás do nginx, o IP do cliente vem do X-Forwarded-For, aceito só
// dos proxies confiáveis. Sem isso, o limite de logins por IP valeria para todos os usuários juntos.
builder.Services.Configure<ForwardedHeadersOptions>(
    opcoes => ConfiguracaoIdentidade.ConfigurarProxies(opcoes, opcoesIniciais.ProxiesConfiaveis));

// No máximo 10 logins e renovações por minuto por IP do cliente (429 em ProblemDetails).
ConfiguracaoIdentidade.AdicionarLimiteLogins(builder.Services, opcoesIniciais.LimiteLoginsPorMinuto);

// A unidade de trabalho é o mesmo AppDbContext (escopo da requisição) que os repositórios usam;
// por isso ela resolve a instância já registrada, e não cria outra.
builder.Services.AddScoped<IUnidadeDeTrabalho>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<IEntregadorRepositorio, EntregadorRepositorio>();
builder.Services.AddScoped<IDispositivoRepositorio, DispositivoRepositorio>();

builder.Services.AddScoped<IEventoImpactoRepositorio, EventoImpactoRepositorio>();
builder.Services.AddScoped<IOcorrenciaRepositorio, OcorrenciaRepositorio>();
builder.Services.AddScoped<IEventoMqttProcessadoRepositorio, EventoMqttProcessadoRepositorio>();
builder.Services.AddScoped<ITurnoRepositorio, TurnoRepositorio>();
builder.Services.AddScoped<IVoluntarioRepositorio, VoluntarioRepositorio>();

// Mensageria (seção 6). Uma única conexão com o broker (singleton), compartilhada pelo assinante
// e pelo publicador. O roteador e os processadores têm o escopo de uma mensagem, porque usam o
// AppDbContext. O roteador recebe os 5 processadores e recusa a montagem se faltar algum tipo.
builder.Services.AddSingleton<IClienteMqtt, ConexaoMqtt>();
builder.Services.AddSingleton<IPublicadorComandos, PublicadorComandosMqtt>();
builder.Services.AddSingleton<ProcessadorTelemetria>();
builder.Services.AddScoped<IProcessadorEvento, ProcessadorImpactoDetectado>();
builder.Services.AddScoped<IProcessadorEvento, ProcessadorContagemIniciada>();
builder.Services.AddScoped<IProcessadorEvento, ProcessadorFalsoPositivo>();
builder.Services.AddScoped<IProcessadorEvento, ProcessadorCancelado>();
builder.Services.AddScoped<IProcessadorEvento, ProcessadorSos>();
builder.Services.AddScoped<RoteadorMensagemMqtt>();

// Comandos da ocorrência (SOCORRO_A_CAMINHO e ENCERRAR): publicados depois de gravar, sem bloquear
// a rota nem o roteador. Scoped porque procura o capacete da vítima no banco.
builder.Services.AddScoped<PublicadorComandosOcorrencia>();

// Aviso ao contato de emergência, simulado no protótipo (só uma linha de log, sem dado pessoal).
builder.Services.AddSingleton<INotificadorContato, NotificadorContatoSimulado>();
builder.Services.AddHostedService<AssinanteMqtt>();

// Casos de uso do cadastro.
builder.Services.AddScoped<EntregadorServico>();
builder.Services.AddScoped<DispositivoServico>();

// Previsão do tempo fixa (exemplo da seção 8) até entrar a Open-Meteo.
builder.Services.AddSingleton<IPrevisaoTempo, PrevisaoTempoSimulada>();

// Serviço de Risco (seção 8): URL base e tempo limite na seção "ServicoRisco" (sem segredo; na VM,
// ServicoRisco__UrlBase). O HttpClient tipado vem da fábrica, que cuida do ciclo das conexões.
builder.Services.AddOptions<OpcoesServicoRisco>()
    .Bind(builder.Configuration.GetSection(OpcoesServicoRisco.Secao))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<ClienteServicoRisco>((sp, http) =>
{
    OpcoesServicoRisco opcoes = sp.GetRequiredService<IOptions<OpcoesServicoRisco>>().Value;
    http.BaseAddress = opcoes.UrlBase;
    http.Timeout = TimeSpan.FromSeconds(opcoes.TempoLimiteS);
});

// O TurnoServico recebe o decorador: tenta o Serviço de Risco e, se ele falhar, usa a versão
// simulada (mesma regra, mesmo índice). O turno nunca falha por causa do Serviço de Risco.
builder.Services.AddSingleton<ServicoRiscoSimulado>();
builder.Services.AddScoped<IServicoRisco, ServicoRiscoComReserva>();
builder.Services.AddScoped<TrechosCriticosServico>();

// Casos de uso do turno, da consulta de ocorrências e da rede de voluntários.
builder.Services.AddScoped<TurnoServico>();
builder.Services.AddScoped<OcorrenciaServico>();
builder.Services.AddScoped<AtendimentoOcorrenciaServico>();
builder.Services.AddScoped<VoluntarioServico>();

// Escalonamento para a camada 2 e encerramento automático em segundo plano, sem depender de
// requisição: uma rodada na subida e outra a cada IntervaloEscalonamentoS, cada uma com o próprio
// escopo (e o próprio AppDbContext).
builder.Services.AddScoped<EscalonadorOcorrencias>();
builder.Services.AddScoped<EncerradorAutomaticoOcorrencias>();
builder.Services.AddHostedService<ServicoEscalonamentoSos>();

// Controllers com as rotas em /api/v1 (seção 7 do contrato). O JSON segue o padrão Web
// (camelCase). Os enums não precisam de conversor global: cada tipo do domínio já traz
// [JsonConverter(typeof(JsonStringEnumConverter<T>))] e o nome do contrato em cada membro.
// O controller de desenvolvimento só é registrado em Development (fora dele, 404 e fora do Scalar).
IMvcBuilder mvc = builder.Services.AddControllers(options =>
    {
        // Os erros de validação saem com o nome do campo no JSON (cpf), e não o da classe (Cpf).
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
    });
ProvedorControllersPorAmbiente.Aplicar(mvc, builder.Environment.IsDevelopment())
    .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// Erros sempre no formato ProblemDetails (RFC 9457). O TratadorExcecoes converte as exceções
// do domínio, do serviço e do EF Core no status certo.
builder.Services.AddProblemDetails(options =>
{
    // O 400 automático do [ApiController] e as respostas só com status (401 e 403 do esquema bearer,
    // 404 de rota inexistente) recebem títulos em português, no mesmo formato do TratadorExcecoes.
    options.CustomizeProblemDetails = ProblemasPadrao.Completar;
});
builder.Services.AddExceptionHandler<TratadorExcecoes>();

// Documento OpenAPI gerado a partir dos controllers e dos comentários XML, com o esquema bearer.
builder.Services.AddOpenApi(ConfiguracaoOpenApi.Configurar);

// Health check em /api/v1/saude: responde Unhealthy se o banco não estiver acessível.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

WebApplication app = builder.Build();

// Monta o roteador uma vez na subida: se algum tipo de evento ficou sem processador, a API
// nem sobe, com a mensagem do RoteadorMensagemMqtt.MontarProcessadores.
using (IServiceScope escopo = app.Services.CreateScope())
{
    escopo.ServiceProvider.GetRequiredService<RoteadorMensagemMqtt>();
}

// O hash de referência do login com e-mail desconhecido é gerado aqui, na subida, e não no primeiro login.
app.Services.GetRequiredService<SenhaReferencia>();

// Primeiro os cabeçalhos encaminhados: tudo o que vem depois (log, limitador) já vê o IP do cliente.
app.UseForwardedHeaders();

app.UseExceptionHandler();

// Respostas de erro sem corpo (ex.: rota inexistente) também saem em ProblemDetails.
app.UseStatusCodePages();

// O documento OpenAPI (/openapi/v1.json) e o Scalar (/scalar) só ficam expostos em Development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("HelmCare API"));
}

// Roteamento antes do limitador e da autorização, que dependem da rota escolhida.
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// Sem UseHttpsRedirection: o contrato define a API em http na porta 5000 (o TLS ficaria no nginx).
app.MapHealthChecks("/api/v1/saude");

// Login exigido em todas as rotas dos controllers; as públicas são marcadas uma a uma com
// [AllowAnonymous]. Rota inexistente continua 404 (não há política global).
app.MapControllers().RequireAuthorization();

await app.RunAsync();
