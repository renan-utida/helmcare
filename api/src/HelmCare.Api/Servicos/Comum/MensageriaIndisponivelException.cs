using System;

namespace HelmCare.Api.Servicos.Comum;

/// <summary>
/// A API não está conectada ao broker MQTT, então o comando não chegou ao capacete.
/// Vira <b>503 Service Unavailable</b> no <c>TratadorExcecoes</c>: a falha é temporária e o
/// cliente pode repetir o pedido.
/// </summary>
public sealed class MensageriaIndisponivelException : Exception
{
    /// <summary>
    /// Cria a exceção sem mensagem específica.
    /// </summary>
    public MensageriaIndisponivelException()
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem, que vai só para o log (o cliente recebe um texto fixo).
    /// </summary>
    /// <param name="message">Mensagem.</param>
    public MensageriaIndisponivelException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem e a exceção da biblioteca MQTT que a causou.
    /// </summary>
    /// <param name="message">Mensagem.</param>
    /// <param name="innerException">Exceção original.</param>
    public MensageriaIndisponivelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
