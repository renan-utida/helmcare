using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Controllers com as rotas em /api/v1 (seção 7 do contrato).
builder.Services.AddControllers();

// Erros sempre no formato ProblemDetails (RFC 9457).
builder.Services.AddProblemDetails();

// Documento OpenAPI gerado a partir dos controllers e dos comentários XML.
builder.Services.AddOpenApi();

// Health check simples, exposto em /api/v1/saude.
builder.Services.AddHealthChecks();

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
