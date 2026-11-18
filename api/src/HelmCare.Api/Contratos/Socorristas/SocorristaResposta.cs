using System;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Contratos.Socorristas;

/// <summary>
/// Corpo do <c>201</c> do <c>POST /api/v1/dev/socorristas</c>, com o e-mail mascarado.
/// </summary>
public sealed class SocorristaResposta
{
    /// <summary>
    /// Id do socorrista.
    /// </summary>
    public required Guid SocorristaId { get; init; }

    /// <summary>
    /// Nome do socorrista.
    /// </summary>
    public required string Nome { get; init; }

    /// <summary>
    /// Organização em que atua.
    /// </summary>
    public required string Organizacao { get; init; }

    /// <summary>
    /// Se o cadastro foi verificado.
    /// </summary>
    public required bool Verificado { get; init; }

    /// <summary>
    /// E-mail mascarado (<c>s***@exemplo.com</c>).
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// Monta a resposta, mascarando o e-mail.
    /// </summary>
    /// <param name="socorrista">Socorrista criado.</param>
    /// <param name="email">E-mail da conta, sem máscara.</param>
    /// <returns>A resposta.</returns>
    public static SocorristaResposta De(Socorrista socorrista, string email)
    {
        ArgumentNullException.ThrowIfNull(socorrista);

        return new SocorristaResposta
        {
            SocorristaId = socorrista.Id,
            Nome = socorrista.Nome,
            Organizacao = socorrista.Organizacao,
            Verificado = socorrista.Verificado,
            Email = Mascaras.Email(email),
        };
    }
}
