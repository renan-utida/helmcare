using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Mensageria.Processadores;

/// <summary>
/// Estratégia de um tipo de evento da tabela "Como a API registra cada evento" (seção 6.2).
/// Há uma implementação por <see cref="TipoEventoMqtt"/>, e o <c>RoteadorMensagemMqtt</c> escolhe
/// pela propriedade <see cref="Tipo"/>. O processador só altera as entidades; quem grava (junto
/// com o registro de idempotência) é o roteador.
/// </summary>
public interface IProcessadorEvento
{
    /// <summary>
    /// Tipo de evento que este processador trata.
    /// </summary>
    TipoEventoMqtt Tipo { get; }

    /// <summary>
    /// Aplica o evento no impacto, na ocorrência e no estado do capacete.
    /// </summary>
    /// <param name="contexto">Capacete e evento.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>A tarefa do processamento.</returns>
    /// <exception cref="Dominio.Comum.DominioException">Quando o evento viola uma regra do domínio (a mensagem é descartada).</exception>
    Task ProcessarAsync(ContextoEvento contexto, CancellationToken cancellationToken);
}
