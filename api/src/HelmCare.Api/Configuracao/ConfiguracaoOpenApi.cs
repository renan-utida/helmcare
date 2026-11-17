using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace HelmCare.Api.Configuracao;

/// <summary>
/// Documento OpenAPI (e, com ele, o Scalar): título, descrição e o esquema bearer do Identity. As
/// rotas que exigem login pedem o token; as públicas (<c>[AllowAnonymous]</c>) ficam sem cadeado.
/// </summary>
public static class ConfiguracaoOpenApi
{
    /// <summary>
    /// Nome do esquema de segurança no documento.
    /// </summary>
    public const string EsquemaBearer = "Bearer";

    /// <summary>
    /// Aplica as transformações no documento.
    /// </summary>
    /// <param name="opcoes">Opções do gerador do OpenAPI.</param>
    public static void Configurar(OpenApiOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        opcoes.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info.Title = "HelmCare API";
            document.Info.Description = "API principal do HelmCare: cadastro, login, SOS escalonado e Cofre médico.";

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[EsquemaBearer] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                Description = "accessToken do POST /api/v1/entregadores/login (token opaco do Identity, não é JWT).",
            };

            return Task.CompletedTask;
        });

        opcoes.AddOperationTransformer((operation, context, cancellationToken) =>
        {
            bool publica = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();

            if (!publica)
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(EsquemaBearer, context.Document)] = [],
                });
            }

            return Task.CompletedTask;
        });
    }
}
