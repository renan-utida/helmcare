using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Servicos.Comum;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace HelmCare.Api.Mensageria;

/// <summary>
/// Implementação do <see cref="IClienteMqtt"/> com a MQTTnet. Singleton: a API tem uma única
/// conexão com o broker, compartilhada pelo assinante e pelo publicador. Não reconecta sozinha;
/// quem verifica a conexão e reconecta é o <c>AssinanteMqtt</c>.
/// </summary>
public sealed partial class ConexaoMqtt : IClienteMqtt, IDisposable
{
    private readonly MqttClientFactory _fabrica = new();
    private readonly IMqttClient _cliente;
    private readonly OpcoesMqtt _opcoes;
    private readonly ILogger<ConexaoMqtt> _logger;
    private Func<MensagemMqttRecebida, CancellationToken, Task>? _aoReceber;

    /// <summary>
    /// Cria a conexão (ainda desconectada).
    /// </summary>
    /// <param name="opcoes">Broker, porta e clientId.</param>
    /// <param name="logger">Log da aplicação.</param>
    public ConexaoMqtt(IOptions<OpcoesMqtt> opcoes, ILogger<ConexaoMqtt> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _opcoes = opcoes.Value;
        _logger = logger;
        _cliente = _fabrica.CreateMqttClient();
        _cliente.ApplicationMessageReceivedAsync += AoReceberDaBibliotecaAsync;
        _cliente.DisconnectedAsync += AoDesconectarAsync;
    }

    /// <inheritdoc/>
    public bool EstaConectado => _cliente.IsConnected;

    /// <inheritdoc/>
    public async Task ConectarAsync(
        IReadOnlyCollection<string> filtros,
        Func<MensagemMqttRecebida, CancellationToken, Task> aoReceber,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtros);
        ArgumentNullException.ThrowIfNull(aoReceber);

        _aoReceber = aoReceber;

        // MQTT 3.1.1, o mesmo do capacete. Sessão limpa: ver o comentário de OpcoesMqtt.
        MqttClientOptions opcoesConexao = new MqttClientOptionsBuilder()
            .WithTcpServer(_opcoes.Host, _opcoes.Porta)
            .WithClientId(_opcoes.ClientId)
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .WithCleanSession(true)
            .Build();

        await _cliente.ConnectAsync(opcoesConexao, cancellationToken);

        MqttClientSubscribeOptionsBuilder assinatura = _fabrica.CreateSubscribeOptionsBuilder();

        foreach (string filtro in filtros)
        {
            assinatura.WithTopicFilter(f => f.WithTopic(filtro).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce));
        }

        MqttClientSubscribeResult resultado = await _cliente.SubscribeAsync(assinatura.Build(), cancellationToken);

        // O broker responde um código por filtro; qualquer código acima de QoS 2 é recusa.
        MqttClientSubscribeResultItem? recusado = resultado.Items
            .FirstOrDefault(i => i.ResultCode > MqttClientSubscribeResultCode.GrantedQoS2);

        if (recusado is not null)
        {
            throw new MensageriaIndisponivelException(
                $"O broker recusou a assinatura de {recusado.TopicFilter.Topic} ({recusado.ResultCode}).");
        }
    }

    /// <inheritdoc/>
    public async Task PublicarAsync(
        string topico,
        ReadOnlyMemory<byte> payload,
        QosMqtt qos,
        CancellationToken cancellationToken)
    {
        if (!_cliente.IsConnected)
        {
            throw new MensageriaIndisponivelException("A API não está conectada ao broker MQTT.");
        }

        MqttApplicationMessage mensagem = new MqttApplicationMessageBuilder()
            .WithTopic(topico)
            .WithPayload(new ReadOnlySequence<byte>(payload))
            .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
            .WithRetainFlag(false)
            .Build();

        MqttClientPublishResult resultado;

        try
        {
            resultado = await _cliente.PublishAsync(mensagem, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A conexão caiu no meio da publicação: para quem chamou, é a mesma indisponibilidade.
            throw new MensageriaIndisponivelException("A publicação no broker MQTT falhou.", ex);
        }

        if (!resultado.IsSuccess)
        {
            throw new MensageriaIndisponivelException($"O broker recusou a publicação ({resultado.ReasonCode}).");
        }
    }

    /// <inheritdoc/>
    public async Task DesconectarAsync(CancellationToken cancellationToken)
    {
        if (_cliente.IsConnected)
        {
            await _cliente.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), cancellationToken);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cliente.ApplicationMessageReceivedAsync -= AoReceberDaBibliotecaAsync;
        _cliente.DisconnectedAsync -= AoDesconectarAsync;
        _cliente.Dispose();
    }

    // A MQTTnet espera este método terminar antes de entregar a próxima mensagem, então as
    // mensagens são processadas uma de cada vez, na ordem de chegada. O payload é copiado porque
    // o buffer da biblioteca é reaproveitado depois do retorno.
    private Task AoReceberDaBibliotecaAsync(MqttApplicationMessageReceivedEventArgs argumentos)
    {
        Func<MensagemMqttRecebida, CancellationToken, Task>? aoReceber = _aoReceber;

        if (aoReceber is null)
        {
            return Task.CompletedTask;
        }

        MqttApplicationMessage mensagem = argumentos.ApplicationMessage;
        var recebida = new MensagemMqttRecebida(mensagem.Topic, mensagem.Payload.ToArray());
        return aoReceber(recebida, CancellationToken.None);
    }

    private Task AoDesconectarAsync(MqttClientDisconnectedEventArgs argumentos)
    {
        if (argumentos.ClientWasConnected)
        {
            RegistrarQueda(_logger, argumentos.Reason.ToString());
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Warning, Message = "Conexão MQTT perdida (motivo: {Motivo}).")]
    private static partial void RegistrarQueda(ILogger logger, string motivo);
}
