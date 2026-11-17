using System;

namespace HelmCare.Api.Servicos.Comum;

/// <summary>
/// O usuário está autenticado, mas não pode usar o recurso: ele é de outro entregador ou o token
/// tem o papel errado (seção 7, "Identidade e autorização"). Vira <b>403 Forbidden</b> no
/// <c>TratadorExcecoes</c>. Só é lançada depois de o recurso ser encontrado, para valer a ordem 401, 404, 403.
/// A mensagem nunca leva dado pessoal.
/// </summary>
public sealed class AcessoNegadoException : Exception
{
    /// <summary>
    /// Cria a exceção sem mensagem específica.
    /// </summary>
    public AcessoNegadoException()
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem, em português, que vai para o campo <c>detail</c>.
    /// </summary>
    /// <param name="message">Mensagem (ex.: "O recurso pertence a outro entregador.").</param>
    public AcessoNegadoException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem e a exceção que a causou.
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c>.</param>
    /// <param name="innerException">Exceção original.</param>
    public AcessoNegadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
