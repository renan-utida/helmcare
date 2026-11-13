using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Configuracoes;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Comum;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HelmCare.Api.Configuracao;

/// <summary>
/// Converte toda exceção que escapa de um controller em ProblemDetails, com <c>title</c> e
/// <c>detail</c> em português e o <c>traceId</c> da requisição. Os erros de validação dos DTOs
/// não passam por aqui: o <c>[ApiController]</c> já responde 400 antes de chamar a ação.
/// </summary>
/// <remarks>
/// O log registra só o tipo da exceção, o status e o <c>traceId</c>. A mensagem da exceção e o
/// corpo da requisição ficam de fora, porque podem conter dado pessoal (seção 9 do contrato).
/// Com o <c>traceId</c>, quem recebeu o erro consegue apontar a requisição no log.
/// </remarks>
public sealed partial class TratadorExcecoes : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<TratadorExcecoes> _logger;

    /// <summary>
    /// Cria o tratador.
    /// </summary>
    /// <param name="problemDetails">Serviço do ASP.NET Core que escreve o ProblemDetails na resposta.</param>
    /// <param name="logger">Log da aplicação.</param>
    public TratadorExcecoes(IProblemDetailsService problemDetails, ILogger<TratadorExcecoes> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        (int status, string titulo, string detalhe) = Classificar(exception);
        string traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        Registrar(exception, status, traceId);

        httpContext.Response.StatusCode = status;

        var problema = new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Detail = detalhe,
            Instance = httpContext.Request.Path,
        };
        problema.Extensions["traceId"] = traceId;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema,
            Exception = exception,
        });
    }

    // A ordem importa: DbUpdateConcurrencyException herda de DbUpdateException.
    // As mensagens das exceções do domínio e do serviço são escritas para o cliente (em português e
    // sem dado pessoal); as do EF Core, do Npgsql e da mensageria citam tabelas, índices ou o broker,
    // por isso nunca são repassadas.
    private static (int Status, string Titulo, string Detalhe) Classificar(Exception exception)
    {
        return exception switch
        {
            ValidacaoDominioException e => (StatusCodes.Status400BadRequest, "Dados inválidos", e.Message),
            RecursoNaoEncontradoException e => (StatusCodes.Status404NotFound, "Recurso não encontrado", e.Message),
            TransicaoInvalidaException e => (StatusCodes.Status409Conflict, "Transição de estado inválida", e.Message),
            RegraNegocioException e => (StatusCodes.Status409Conflict, "Regra de negócio violada", e.Message),
            // 503 não está no quadro "Códigos padrão" da seção 7: é uma falha temporária da
            // mensageria (o comando não chegou ao capacete), e o cliente pode repetir o pedido.
            MensageriaIndisponivelException => (
                StatusCodes.Status503ServiceUnavailable,
                "Capacete indisponível",
                "O capacete não pôde ser avisado agora; tente de novo."),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Conflito de concorrência",
                "O recurso foi alterado por outra requisição ao mesmo tempo. Consulte de novo e repita a operação."),
            // Dois POST /turnos simultâneos do mesmo entregador passam pela checagem do serviço, e o
            // segundo esbarra no índice de turno ativo: mesma resposta da checagem.
            DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: TurnoConfiguracao.IndiceTurnoAtivo,
                },
            } => (StatusCodes.Status409Conflict, "Regra de negócio violada", "O entregador já tem um turno ativo."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => (
                StatusCodes.Status409Conflict,
                "Registro duplicado",
                "Já existe um registro com estes dados, que precisam ser únicos."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Erro interno",
                "Ocorreu um erro inesperado. Informe o traceId ao suporte."),
        };
    }

    // Erro inesperado (500) vai como Error; as falhas esperadas (4xx) vão como Warning.
    // O IsEnabled evita montar os argumentos quando o nível está desligado.
    private void Registrar(Exception exception, int status, string traceId)
    {
        if (status >= StatusCodes.Status500InternalServerError)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                RegistrarErroInesperado(_logger, exception.GetType().Name, traceId);
            }
        }
        else if (_logger.IsEnabled(LogLevel.Warning))
        {
            RegistrarFalhaTratada(_logger, exception.GetType().Name, status, traceId);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Exceção {TipoExcecao} respondida com {Status} (traceId {TraceId}).")]
    private static partial void RegistrarFalhaTratada(ILogger logger, string tipoExcecao, int status, string traceId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Exceção inesperada {TipoExcecao} respondida com 500 (traceId {TraceId}).")]
    private static partial void RegistrarErroInesperado(ILogger logger, string tipoExcecao, string traceId);
}
