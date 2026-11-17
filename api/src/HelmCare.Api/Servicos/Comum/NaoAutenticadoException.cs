using System;

namespace HelmCare.Api.Servicos.Comum;

/// <summary>
/// Falta o token de acesso, ou ele é inválido ou está vencido, numa rota que exige login. Também é o login
/// recusado (e-mail ou senha errados, conta bloqueada ou conta de outro papel), sempre com o mesmo texto.
/// Vira <b>401 Unauthorized</b> no <c>TratadorExcecoes</c>, sem nenhum token na resposta.
/// A mensagem nunca leva dado pessoal.
/// </summary>
public sealed class NaoAutenticadoException : Exception
{
    /// <summary>
    /// Cria a exceção sem mensagem específica.
    /// </summary>
    public NaoAutenticadoException()
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem, em português, que vai para o campo <c>detail</c>.
    /// </summary>
    /// <param name="message">Mensagem (ex.: "E-mail ou senha inválidos.").</param>
    public NaoAutenticadoException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem e a exceção que a causou.
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c>.</param>
    /// <param name="innerException">Exceção original.</param>
    public NaoAutenticadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
