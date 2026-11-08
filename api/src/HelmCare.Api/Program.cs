using System;
using HelmCare.Api.Persistencia;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// A connection string tem senha, então nunca fica em appsettings*.json:
// vem do user-secrets (Development) ou da variável de ambiente ConnectionStrings__HelmCareApi.
string connectionString = builder.Configuration.GetConnectionString("HelmCareApi")
    ?? throw new InvalidOperationException(
        "Connection string 'HelmCareApi' não configurada. Em Development, use "
        + "'dotnet user-secrets set ConnectionStrings:HelmCareApi \"...\" --project src/HelmCare.Api'; "
        + "nos outros ambientes, defina a variável ConnectionStrings__HelmCareApi.");

// EF Core com PostgreSQL (banco helmcare_api).
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Controllers com as rotas em /api/v1 (seção 7 do contrato).
builder.Services.AddControllers();

// Erros sempre no formato ProblemDetails (RFC 9457).
builder.Services.AddProblemDetails();

// Documento OpenAPI gerado a partir dos controllers e dos comentários XML.
builder.Services.AddOpenApi();

// Health check em /api/v1/saude: responde Unhealthy se o banco não estiver acessível.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

WebApplication app = builder.Build();

// O documento OpenAPI só fica exposto em Development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Sem UseHttpsRedirection: o contrato define a API em http na porta 5000.
app.MapHealthChecks("/api/v1/saude");
app.MapControllers();

await app.RunAsync();
