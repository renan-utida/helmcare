using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Serviço de ProblemDetails que guarda o problema em vez de escrevê-lo na resposta,
/// para o teste do tratador de erros conferir o status, o título e o detalhe.
/// </summary>
public sealed class FakeProblemDetailsService : IProblemDetailsService
{
    /// <summary>
    /// Último ProblemDetails recebido, ou nulo se nada foi escrito.
    /// </summary>
    public ProblemDetails? Escrito { get; private set; }

    /// <inheritdoc/>
    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        Escrito = context?.ProblemDetails;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
    {
        Escrito = context?.ProblemDetails;
        return ValueTask.FromResult(true);
    }
}
