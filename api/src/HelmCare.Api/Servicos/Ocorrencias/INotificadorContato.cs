using System;
using System.Threading;
using System.Threading.Tasks;

namespace HelmCare.Api.Servicos.Ocorrencias;

/// <summary>
/// Avisa os contatos de emergência que o entregador caiu e o SOS foi aberto. No protótipo o aviso
/// é simulado (<see cref="NotificadorContatoSimulado"/>); um envio real (SMS, WhatsApp) entraria
/// atrás desta mesma interface.
/// </summary>
public interface INotificadorContato
{
    /// <summary>
    /// Avisa da abertura da ocorrência. Só é chamado depois que a ocorrência foi gravada.
    /// </summary>
    /// <param name="ocorrenciaId">Ocorrência aberta.</param>
    /// <param name="quantidadeContatos">Quantos contatos de emergência o entregador tem.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>A tarefa do aviso.</returns>
    Task NotificarAberturaAsync(Guid ocorrenciaId, int quantidadeContatos, CancellationToken cancellationToken);
}
