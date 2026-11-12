using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Servicos.Comum;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Cliente MQTT em memória: não abre rede, guarda o que foi publicado e os filtros assinados.
/// </summary>
public sealed class FakeClienteMqtt : IClienteMqtt
{
    private readonly List<PublicacaoFake> _publicadas = [];

    /// <summary>
    /// Estado da conexão simulada (começa conectado).
    /// </summary>
    public bool EstaConectado { get; set; } = true;

    /// <summary>
    /// Mensagens publicadas, na ordem.
    /// </summary>
    public IReadOnlyList<PublicacaoFake> Publicadas => _publicadas;

    /// <summary>
    /// Filtros da última conexão.
    /// </summary>
    public IReadOnlyCollection<string> Filtros { get; private set; } = [];

    /// <inheritdoc/>
    public Task ConectarAsync(
        IReadOnlyCollection<string> filtros,
        Func<MensagemMqttRecebida, CancellationToken, Task> aoReceber,
        CancellationToken cancellationToken)
    {
        Filtros = filtros;
        EstaConectado = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task PublicarAsync(string topico, ReadOnlyMemory<byte> payload, QosMqtt qos, CancellationToken cancellationToken)
    {
        if (!EstaConectado)
        {
            throw new MensageriaIndisponivelException("Fake desconectado.");
        }

        _publicadas.Add(new PublicacaoFake(topico, Encoding.UTF8.GetString(payload.Span), qos));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DesconectarAsync(CancellationToken cancellationToken)
    {
        EstaConectado = false;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Uma publicação guardada pelo <see cref="FakeClienteMqtt"/>.
/// </summary>
/// <param name="Topico">Tópico.</param>
/// <param name="Payload">Payload em texto.</param>
/// <param name="Qos">Nível de entrega.</param>
public sealed record PublicacaoFake(string Topico, string Payload, QosMqtt Qos);
