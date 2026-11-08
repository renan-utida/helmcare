using System;

namespace HelmCare.Dominio.Comum;

/// <summary>
/// Base de todas as exceções que o domínio lança quando uma regra do HelmCare é violada.
/// É abstrata porque o tratamento HTTP depende do tipo concreto: a API traduz cada derivada
/// em um status diferente (400 ou 409), sempre no formato ProblemDetails.
/// </summary>
public abstract class DominioException : Exception
{
    /// <summary>
    /// Cria a exceção sem mensagem específica.
    /// </summary>
    protected DominioException()
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem, em português, que explica a regra violada.
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c> do ProblemDetails.</param>
    protected DominioException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem e a exceção que a causou.
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c> do ProblemDetails.</param>
    /// <param name="innerException">Exceção original.</param>
    protected DominioException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
