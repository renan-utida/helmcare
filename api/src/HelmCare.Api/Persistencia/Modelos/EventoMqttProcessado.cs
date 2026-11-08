using System;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Persistencia.Modelos;

/// <summary>
/// Registro de idempotência dos eventos MQTT (decisão da v1.3). Não é entidade de domínio:
/// existe só para a persistência. Pela seção 6.2 do contrato, eventos com o mesmo
/// dispositivo, tipo e <c>ts</c> são um só (o capacete republica o SOS com o mesmo <c>ts</c>
/// até receber resposta). O índice único nessas três colunas faz o banco recusar a repetição.
/// </summary>
public sealed class EventoMqttProcessado
{
    /// <summary>
    /// Tamanho máximo do código do dispositivo vindo do tópico MQTT.
    /// </summary>
    public const int TamanhoMaximoCodigo = 20;

    /// <summary>
    /// Cria o registro de um evento processado.
    /// </summary>
    /// <param name="codigoDispositivo">Código do capacete (ex.: <c>HC-0001</c>).</param>
    /// <param name="tipo">Tipo do evento.</param>
    /// <param name="ts">Instante do evento informado pelo capacete, em UTC.</param>
    /// <param name="recebidoEm">Instante em que a API recebeu o evento, em UTC.</param>
    /// <exception cref="ValidacaoDominioException">Quando algum dado é inválido.</exception>
    public EventoMqttProcessado(string codigoDispositivo, TipoEventoMqtt tipo, DateTime ts, DateTime recebidoEm)
    {
        Id = Guid.CreateVersion7();
        CodigoDispositivo = Garantir.TextoObrigatorio(codigoDispositivo, "dispositivo", TamanhoMaximoCodigo);
        Tipo = Garantir.Definido(tipo, "tipo");
        Ts = Garantir.Utc(ts, "ts");
        RecebidoEm = Garantir.Utc(recebidoEm, "recebidoEm");
    }

    // Construtor usado só pelo EF Core ao materializar o registro do banco.
    private EventoMqttProcessado()
    {
        CodigoDispositivo = string.Empty;
    }

    /// <summary>
    /// Identificador do registro (Guid versão 7, ordenado no tempo).
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Código do capacete que publicou o evento.
    /// </summary>
    public string CodigoDispositivo { get; private set; }

    /// <summary>
    /// Tipo do evento.
    /// </summary>
    public TipoEventoMqtt Tipo { get; private set; }

    /// <summary>
    /// Instante do evento informado pelo capacete, em UTC.
    /// </summary>
    public DateTime Ts { get; private set; }

    /// <summary>
    /// Instante em que a API recebeu o evento, em UTC.
    /// </summary>
    public DateTime RecebidoEm { get; private set; }
}
