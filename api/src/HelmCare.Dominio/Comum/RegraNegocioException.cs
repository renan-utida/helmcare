using System;

namespace HelmCare.Dominio.Comum;

/// <summary>
/// Uma regra de negócio impede a operação, mesmo com dados válidos e transição permitida
/// (ex.: vincular um socorrista que já tem usuário a um segundo usuário). Vira
/// <b>409 Conflict</b>: o que conflita são os dados atuais, e não o formato do que foi enviado.
/// </summary>
public sealed class RegraNegocioException : DominioException
{
    /// <summary>
    /// Cria a exceção sem mensagem específica.
    /// </summary>
    public RegraNegocioException()
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem, em português, que explica a regra violada.
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c> do ProblemDetails.</param>
    public RegraNegocioException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem e a exceção que a causou.
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c> do ProblemDetails.</param>
    /// <param name="innerException">Exceção original.</param>
    public RegraNegocioException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
