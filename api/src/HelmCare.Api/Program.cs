using System;
using System.Text.Json;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Mensageria.Processadores;
using HelmCare.Api.Persistencia;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Servicos.Cadastro;
using HelmCare.Api.Servicos.Ocorrencias;
using HelmCare.Api.Servicos.Risco;
using HelmCare.Api.Servicos.Turnos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
builder.Services.AddHostedService<AssinanteMqtt>();

// Casos de uso do cadastro.
builder.Services.AddScoped<EntregadorServico>();
builder.Services.AddScoped<DispositivoServico>();

// Índice de Risco do Turno simulado (seção 8), sem rede: na sexta entram a Open-Meteo e o
// cliente HTTP do Serviço de Risco, e estas duas implementações ficam como reserva.
builder.Services.AddSingleton<IPrevisaoTempo, PrevisaoTempoSimulada>();
builder.Services.AddSingleton<IServicoRisco, ServicoRiscoSimulado>();

// Casos de uso do turno e da consulta de ocorrências.
builder.Services.AddScoped<TurnoServico>();
builder.Services.AddScoped<OcorrenciaServico>();

// Controllers com as rotas em /api/v1 (seção 7 do contrato). O JSON segue o padrão Web
// (camelCase). Os enums não precisam de conversor global: cada tipo do domínio já traz
// [JsonConverter(typeof(JsonStringEnumConverter<T>))] e o nome do contrato em cada membro.
builder.Services.AddControllers(options =>
    {
        // Os erros de validação saem com o nome do campo no JSON (cpf), e não o da classe (Cpf).
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// Erros sempre no formato ProblemDetails (RFC 9457). O TratadorExcecoes converte as exceções
// do domínio, do serviço e do EF Core no status certo.
builder.Services.AddProblemDetails(options =>
{
    // O 400 automático do [ApiController] vem com o título padrão em inglês; troca pelo mesmo
    // título que o TratadorExcecoes usa para os dados inválidos do domínio.
    options.CustomizeProblemDetails = contexto =>
    {
        if (contexto.ProblemDetails is ValidationProblemDetails)
        {
            contexto.ProblemDetails.Title = "Dados inválidos";
        }
    };
});
builder.Services.AddExceptionHandler<TratadorExcecoes>();

// Documento OpenAPI gerado a partir dos controllers e dos comentários XML.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "HelmCare API";
        document.Info.Description = "API principal do HelmCare: cadastro, SOS escalonado e Cofre médico.";
        return Task.CompletedTask;
    });
});

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

app.UseExceptionHandler();

// Respostas de erro sem corpo (ex.: rota inexistente) também saem em ProblemDetails.
app.UseStatusCodePages();

// O documento OpenAPI (/openapi/v1.json) e o Scalar (/scalar) só ficam expostos em Development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("HelmCare API"));
}

// Sem UseHttpsRedirection: o contrato define a API em http na porta 5000.
app.MapHealthChecks("/api/v1/saude");
app.MapControllers();

await app.RunAsync();
