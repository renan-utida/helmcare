using System;

namespace HelmCare.Api.Servicos.Comum;

/// <summary>
/// O id ou o <c>qrToken</c> pedido não existe (entregador, turno, ocorrência, voluntário,
/// dispositivo ou Cofre). Vira <b>404 Not Found</b> no <c>TratadorExcecoes</c>.
/// Fica na camada de serviço, e não no domínio, porque quem procura no banco é o serviço.
/// A mensagem nunca leva dado pessoal, só o tipo do recurso.
/// </summary>
public sealed class RecursoNaoEncontradoException : Exception
{
    /// <summary>
    /// Cria a exceção sem mensagem específica.
    /// </summary>
    public RecursoNaoEncontradoException()
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem, em português, que vai para o campo <c>detail</c>.
    /// </summary>
    /// <param name="message">Mensagem (ex.: "Entregador não encontrado.").</param>
    public RecursoNaoEncontradoException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem e a exceção que a causou.
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c>.</param>
    /// <param name="innerException">Exceção original.</param>
    public RecursoNaoEncontradoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
