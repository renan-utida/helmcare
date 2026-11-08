using System;

namespace HelmCare.Dominio.Comum;

/// <summary>
/// Um dado recebido é inválido (campo vazio, formato errado, valor fora da faixa).
/// Vira <b>400 Bad Request</b>: o problema está na própria requisição, e o cliente pode
/// corrigi-la e reenviar.
/// </summary>
public sealed class ValidacaoDominioException : DominioException
{
    /// <summary>
    /// Cria a exceção sem mensagem específica.
    /// </summary>
    public ValidacaoDominioException()
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem, em português, que cita o campo inválido.
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c> do ProblemDetails.</param>
    public ValidacaoDominioException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem e a exceção que a causou.
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c> do ProblemDetails.</param>
    /// <param name="innerException">Exceção original.</param>
    public ValidacaoDominioException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
