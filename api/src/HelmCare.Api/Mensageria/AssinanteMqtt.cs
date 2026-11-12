using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Mensageria;

/// <summary>
/// Serviço em segundo plano que mantém a API assinando a telemetria e os eventos dos capacetes.
/// A cada <c>IntervaloReconexaoS</c> confere a conexão e, se caiu, conecta e assina de novo.
/// Cada mensagem recebida abre um escopo de DI e vai para o <see cref="RoteadorMensagemMqtt"/>.
/// Nenhuma mensagem e nenhuma falha de conexão derrubam o serviço: tudo vira log.
/// </summary>
public sealed partial class AssinanteMqtt : BackgroundService
{
    private readonly IClienteMqtt _cliente;
    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;
    private readonly OpcoesMqtt _opcoes;
    private readonly string[] _filtros;
    private readonly ILogger<AssinanteMqtt> _logger;

    /// <summary>
    /// Cria o assinante.
    /// </summary>
    /// <param name="cliente">Conexão com o broker.</param>
    /// <param name="escopos">Fábrica de escopos de DI (um por mensagem).</param>
    /// <param name="relogio">Relógio do PeriodicTimer.</param>
    /// <param name="opcoes">Broker, raiz dos tópicos e intervalo de reconexão.</param>
    /// <param name="logger">Log da aplicação.</param>
    public AssinanteMqtt(
        IClienteMqtt cliente,
        IServiceScopeFactory escopos,
        TimeProvider relogio,
        IOptions<OpcoesMqtt> opcoes,
        ILogger<AssinanteMqtt> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _cliente = cliente;
        _escopos = escopos;
        _relogio = relogio;
        _opcoes = opcoes.Value;
        _logger = logger;
        _filtros = [Topicos.FiltroTelemetria(_opcoes.RaizTopicos), Topicos.FiltroEvento(_opcoes.RaizTopicos)];
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        try
        {
            await _cliente.DesconectarAsync(cancellationToken);
            RegistrarDesconexao(_logger);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RegistrarFalhaAoDesconectar(_logger, ex.GetType().Name);
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opcoes.Habilitado)
        {
            RegistrarDesabilitado(_logger);
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_opcoes.IntervaloReconexaoS), _relogio);

        try
        {
            // Primeira tentativa já na subida; depois, uma conferência por tick.
            do
            {
                await GarantirConexaoAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Parada normal da aplicação.
        }
    }

    private async Task GarantirConexaoAsync(CancellationToken cancellationToken)
    {
        if (_cliente.EstaConectado)
        {
            return;
        }

        try
        {
            RegistrarConectando(_logger, _opcoes.Host, _opcoes.Porta, _opcoes.ClientId);
            await _cliente.ConectarAsync(_filtros, AoReceberAsync, cancellationToken);
            RegistrarConectado(_logger, _opcoes.Host);
            RegistrarAssinatura(_logger, _filtros[0]);
            RegistrarAssinatura(_logger, _filtros[1]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RegistrarFalhaAoConectar(_logger, ex.GetType().Name, _opcoes.IntervaloReconexaoS);
        }
    }

    private async Task AoReceberAsync(MensagemMqttRecebida mensagem, CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope escopo = _escopos.CreateAsyncScope();
            RoteadorMensagemMqtt roteador = escopo.ServiceProvider.GetRequiredService<RoteadorMensagemMqtt>();
            await roteador.ProcessarAsync(mensagem, cancellationToken);
        }
        catch (Exception ex)
        {
            // Último recurso: o roteador já trata os erros do processamento; aqui só sobra falha de
            // montagem (DI). O código do capacete vem do tópico; o tipo e o ts estariam no payload,
            // que não foi lido.
            string codigo = Topicos.TentarInterpretar(_opcoes.RaizTopicos, mensagem.Topico, out string? lido, out _)
                ? lido
                : "?";
            RegistrarFalhaNaMensagem(_logger, ex.GetType().Name, codigo);
        }
    }

    [LoggerMessage(EventId = 300, Level = LogLevel.Information, Message = "MQTT desabilitado (Mqtt:Habilitado = false): a API não vai conectar no broker.")]
    private static partial void RegistrarDesabilitado(ILogger logger);

    [LoggerMessage(EventId = 301, Level = LogLevel.Information, Message = "Conectando ao broker MQTT {Host}:{Porta} como {ClientId}...")]
    private static partial void RegistrarConectando(ILogger logger, string host, int porta, string clientId);

    [LoggerMessage(EventId = 302, Level = LogLevel.Information, Message = "Conectado ao broker MQTT {Host}.")]
    private static partial void RegistrarConectado(ILogger logger, string host);

    [LoggerMessage(EventId = 303, Level = LogLevel.Information, Message = "Assinado {Filtro} (QoS 1).")]
    private static partial void RegistrarAssinatura(ILogger logger, string filtro);

    [LoggerMessage(EventId = 304, Level = LogLevel.Warning, Message = "Falha {TipoExcecao} ao conectar no broker MQTT; nova tentativa em {Intervalo} s.")]
    private static partial void RegistrarFalhaAoConectar(ILogger logger, string tipoExcecao, int intervalo);

    [LoggerMessage(EventId = 305, Level = LogLevel.Error, Message = "Falha {TipoExcecao} ao tratar mensagem MQTT do capacete {Codigo}; mensagem descartada.")]
    private static partial void RegistrarFalhaNaMensagem(ILogger logger, string tipoExcecao, string codigo);

    [LoggerMessage(EventId = 306, Level = LogLevel.Information, Message = "Desconectado do broker MQTT.")]
    private static partial void RegistrarDesconexao(ILogger logger);

    [LoggerMessage(EventId = 307, Level = LogLevel.Warning, Message = "Falha {TipoExcecao} ao desconectar do broker MQTT.")]
    private static partial void RegistrarFalhaAoDesconectar(ILogger logger, string tipoExcecao);
}
