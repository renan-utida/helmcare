using System;

namespace HelmCare.Api.Servicos.Comum;

/// <summary>
/// O <c>GET /clima</c> ficou sem a Open-Meteo e sem cache válido. Vira <b>503</b> no
/// <c>TratadorExcecoes</c>, com o título "Previsão do tempo indisponível", sem nenhum valor simulado.
/// </summary>
public sealed class PrevisaoIndisponivelException : Exception
{
    /// <summary>
    /// Cria a exceção sem mensagem específica.
    /// </summary>
    public PrevisaoIndisponivelException()
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem interna.
    /// </summary>
    /// <param name="message">Mensagem (nunca vai para o cliente).</param>
    public PrevisaoIndisponivelException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem e a causa.
    /// </summary>
    /// <param name="message">Mensagem (nunca vai para o cliente).</param>
    /// <param name="innerException">Exceção original.</param>
    public PrevisaoIndisponivelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
