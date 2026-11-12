using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HelmCare.Api.Mensageria;

/// <summary>
/// Nível de garantia de entrega do MQTT, sem expor o tipo da biblioteca.
/// </summary>
public enum QosMqtt
{
    /// <summary>
    /// QoS 0: no máximo uma vez, sem confirmação.
    /// </summary>
    NoMaximoUmaVez = 0,

    /// <summary>
    /// QoS 1: pelo menos uma vez, com confirmação do broker.
    /// </summary>
    PeloMenosUmaVez = 1,
}

/// <summary>
/// Mensagem recebida do broker. O payload é uma cópia, válida depois que a biblioteca libera o buffer.
/// </summary>
/// <param name="Topico">Tópico em que a mensagem chegou.</param>
/// <param name="Payload">Conteúdo em UTF-8.</param>
public sealed record MensagemMqttRecebida(string Topico, ReadOnlyMemory<byte> Payload);

/// <summary>
/// Conexão da API com o broker MQTT. A implementação real (<see cref="ConexaoMqtt"/>) é a única
/// classe que conhece a biblioteca MQTTnet; o assinante, o roteador e o publicador dependem só
/// desta interface, e os testes usam um fake que guarda o que foi publicado.
/// </summary>
public interface IClienteMqtt
{
    /// <summary>
    /// Diz se a conexão com o broker está ativa agora.
    /// </summary>
    bool EstaConectado { get; }

    /// <summary>
    /// Conecta no broker e assina os filtros. As mensagens recebidas vão para
    /// <paramref name="aoReceber"/>, uma de cada vez, na ordem de chegada.
    /// </summary>
    /// <param name="filtros">Filtros de tópico assinados em QoS 1.</param>
    /// <param name="aoReceber">Tratamento de cada mensagem recebida.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>A tarefa da conexão.</returns>
    Task ConectarAsync(
        IReadOnlyCollection<string> filtros,
        Func<MensagemMqttRecebida, CancellationToken, Task> aoReceber,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publica uma mensagem, sem retain.
    /// </summary>
    /// <param name="topico">Tópico de destino.</param>
    /// <param name="payload">Conteúdo em UTF-8.</param>
    /// <param name="qos">Nível de entrega.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>A tarefa da publicação.</returns>
    /// <exception cref="Servicos.Comum.MensageriaIndisponivelException">Quando não há conexão ou o broker recusa.</exception>
    Task PublicarAsync(string topico, ReadOnlyMemory<byte> payload, QosMqtt qos, CancellationToken cancellationToken);

    /// <summary>
    /// Desconecta do broker de forma limpa (usado ao parar a API).
    /// </summary>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>A tarefa da desconexão.</returns>
    Task DesconectarAsync(CancellationToken cancellationToken);
}
