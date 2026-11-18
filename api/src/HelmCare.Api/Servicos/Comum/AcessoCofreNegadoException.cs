using System;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Servicos.Comum;

/// <summary>
/// Tentativa de acesso ao Cofre negada pela regra do socorrista (não verificado ou janela expirada),
/// já gravada na auditoria. Vira <b>403 Forbidden</b> no <c>TratadorExcecoes</c>, com o
/// <see cref="Motivo"/> no campo <c>motivo</c> do ProblemDetails, para a página mostrar a mensagem certa.
/// </summary>
public sealed class AcessoCofreNegadoException : Exception
{
    /// <summary>
    /// Cria a exceção sem motivo específico (não verificado).
    /// </summary>
    public AcessoCofreNegadoException()
        : this(MotivoNegacaoCofre.SocorristaNaoVerificado)
    {
    }

    /// <summary>
    /// Cria a exceção com o motivo e a mensagem padrão dele.
    /// </summary>
    /// <param name="motivo">Motivo da negação.</param>
    public AcessoCofreNegadoException(MotivoNegacaoCofre motivo)
        : base(Mensagem(motivo))
    {
        Motivo = motivo;
    }

    /// <summary>
    /// Cria a exceção com uma mensagem (o motivo fica como não verificado).
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c>.</param>
    public AcessoCofreNegadoException(string message)
        : base(message)
    {
        Motivo = MotivoNegacaoCofre.SocorristaNaoVerificado;
    }

    /// <summary>
    /// Cria a exceção com a mensagem e a exceção que a causou.
    /// </summary>
    /// <param name="message">Mensagem que vai para o campo <c>detail</c>.</param>
    /// <param name="innerException">Exceção original.</param>
    public AcessoCofreNegadoException(string message, Exception innerException)
        : base(message, innerException)
    {
        Motivo = MotivoNegacaoCofre.SocorristaNaoVerificado;
    }

    /// <summary>
    /// Motivo da negação.
    /// </summary>
    public MotivoNegacaoCofre Motivo { get; }

    private static string Mensagem(MotivoNegacaoCofre motivo)
    {
        return motivo == MotivoNegacaoCofre.AcessoExpirado
            ? "O acesso a esta ficha expirou."
            : "O cadastro do socorrista ainda não foi verificado.";
    }
}
