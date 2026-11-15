using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HelmCare.Api.Servicos.Ocorrencias;

/// <summary>
/// Aviso simulado ao contato de emergência (seção 7, "Regras do SOS na API"): só uma linha de log
/// com o id da ocorrência e a quantidade de contatos. Nunca nome, telefone ou localização (seção 9).
/// </summary>
public sealed partial class NotificadorContatoSimulado : INotificadorContato
{
    private readonly ILogger<NotificadorContatoSimulado> _logger;

    /// <summary>
    /// Cria o notificador.
    /// </summary>
    /// <param name="logger">Log da aplicação.</param>
    public NotificadorContatoSimulado(ILogger<NotificadorContatoSimulado> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task NotificarAberturaAsync(Guid ocorrenciaId, int quantidadeContatos, CancellationToken cancellationToken)
    {
        RegistrarAviso(_logger, ocorrenciaId, quantidadeContatos);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 730, Level = LogLevel.Information, Message = "Aviso simulado ao contato de emergência da ocorrência {OcorrenciaId} ({Quantidade} contato(s)).")]
    private static partial void RegistrarAviso(ILogger logger, Guid ocorrenciaId, int quantidade);
}
