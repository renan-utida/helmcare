using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HelmCare.Api.Configuracao;

/// <summary>
/// Completa os ProblemDetails que não passam pelo <see cref="TratadorExcecoes"/>: o 400 automático do
/// <c>[ApiController]</c> e as respostas só com status (o 401 e o 403 do esquema bearer, o 404 de rota
/// inexistente e o 405), com título e detalhe em português e o mesmo formato do tratador.
/// </summary>
public static class ProblemasPadrao
{
    /// <summary>
    /// Título do <c>401</c>.
    /// </summary>
    public const string TituloNaoAutenticado = "Não autenticado";

    /// <summary>
    /// Título do <c>403</c>.
    /// </summary>
    public const string TituloAcessoNegado = "Acesso negado";

    /// <summary>
    /// Aplica os textos em português. Um ProblemDetails que já tem <c>detail</c> (vindo do
    /// <see cref="TratadorExcecoes"/> ou do limitador) fica como está.
    /// </summary>
    /// <param name="contexto">Contexto da escrita do ProblemDetails.</param>
    public static void Completar(ProblemDetailsContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        ProblemDetails problema = contexto.ProblemDetails;

        // Vale para todo ProblemDetails: o caminho do Cofre nunca sai com o qrToken.
        problema.Instance = CaminhoSeguro.SemQrToken(problema.Instance);

        if (problema is ValidationProblemDetails)
        {
            problema.Title = "Dados inválidos";
            return;
        }

        if (problema.Detail is not null)
        {
            return;
        }

        (string titulo, string detalhe)? textos = problema.Status switch
        {
            StatusCodes.Status401Unauthorized => (
                TituloNaoAutenticado,
                "Faça o login: o token de acesso está ausente, é inválido ou venceu."),
            StatusCodes.Status403Forbidden => (TituloAcessoNegado, "O token não tem permissão para este recurso."),
            StatusCodes.Status404NotFound => ("Recurso não encontrado", "A rota pedida não existe."),
            StatusCodes.Status405MethodNotAllowed => ("Método não permitido", "A rota não aceita este método HTTP."),
            _ => null,
        };

        if (textos is { } escolhidos)
        {
            problema.Title = escolhidos.titulo;
            problema.Detail = escolhidos.detalhe;
            problema.Instance ??= CaminhoSeguro.SemQrToken(contexto.HttpContext.Request.Path);
        }
    }
}
