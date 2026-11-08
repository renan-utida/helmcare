using System;

namespace HelmCare.Dominio.Comum;

/// <summary>
/// A ação pedida não é permitida no estado atual da entidade (ex.: encerrar um turno já
/// encerrado ou aceitar uma ocorrência cancelada). Vira <b>409 Conflict</b>: a requisição
/// é válida, mas conflita com o estado atual do recurso.
/// </summary>
public sealed class TransicaoInvalidaException : DominioException
{
    /// <summary>
    /// Cria a exceção sem mensagem específica.
    /// </summary>
    public TransicaoInvalidaException()
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem, em português, que cita o estado atual e a ação negada.
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c> do ProblemDetails.</param>
    public TransicaoInvalidaException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem e a exceção que a causou.
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c> do ProblemDetails.</param>
    /// <param name="innerException">Exceção original.</param>
    public TransicaoInvalidaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
