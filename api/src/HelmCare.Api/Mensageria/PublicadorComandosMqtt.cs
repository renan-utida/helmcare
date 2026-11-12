using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Mensageria.Mensagens;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Mensageria;

/// <summary>
/// Implementação do <see cref="IPublicadorComandos"/> sobre o <see cref="IClienteMqtt"/>. QoS 1
/// porque o capacete usa sessão persistente: se ele estiver fora da rede por alguns segundos, o
/// broker guarda o comando e entrega quando ele voltar.
/// </summary>
public sealed partial class PublicadorComandosMqtt : IPublicadorComandos
{
    private readonly IClienteMqtt _cliente;
    private readonly TimeProvider _relogio;
    private readonly string _raizTopicos;
    private readonly ILogger<PublicadorComandosMqtt> _logger;

    /// <summary>
    /// Cria o publicador.
    /// </summary>
    /// <param name="cliente">Conexão com o broker.</param>
    /// <param name="relogio">Relógio do servidor, para o <c>ts</c> do comando.</param>
    /// <param name="opcoes">Raiz dos tópicos.</param>
    /// <param name="logger">Log da aplicação.</param>
    public PublicadorComandosMqtt(
        IClienteMqtt cliente,
        TimeProvider relogio,
        IOptions<OpcoesMqtt> opcoes,
        ILogger<PublicadorComandosMqtt> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _cliente = cliente;
        _relogio = relogio;
        _raizTopicos = opcoes.Value.RaizTopicos;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PublicarAsync(string codigoDispositivo, TipoComandoMqtt tipo, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoDispositivo);
        Garantir.Definido(tipo, "tipo");

        if (!_cliente.EstaConectado)
        {
            throw new MensageriaIndisponivelException("A API não está conectada ao broker MQTT.");
        }

        var comando = new ComandoMensagem { Tipo = tipo, Ts = _relogio.GetUtcNow().UtcDateTime };

        await _cliente.PublicarAsync(
            Topicos.ComandoDe(_raizTopicos, codigoDispositivo),
            comando.ParaPayload(),
            QosMqtt.PeloMenosUmaVez,
            cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            string nomeDoComando = NomeContrato.Para(tipo);
            RegistrarComandoPublicado(_logger, nomeDoComando, codigoDispositivo);
        }
    }

    [LoggerMessage(EventId = 400, Level = LogLevel.Information, Message = "Comando {Tipo} publicado para o capacete {Codigo}.")]
    private static partial void RegistrarComandoPublicado(ILogger logger, string tipo, string codigo);
}
