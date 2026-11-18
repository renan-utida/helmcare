using System;
using HelmCare.Api.Seguranca;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Contratos.Socorristas;

/// <summary>
/// Corpo do <c>200</c> do <c>POST /api/v1/socorristas/login</c>: o mesmo formato do login do
/// entregador, com os campos do socorrista no lugar dos do entregador. Os tokens nunca vão para o log.
/// </summary>
public sealed class LoginSocorristaResposta
{
    /// <summary>
    /// Tipo do token, sempre <c>Bearer</c>.
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// Token de acesso.
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// Validade do token de acesso, em segundos.
    /// </summary>
    public required long ExpiresIn { get; init; }

    /// <summary>
    /// Token de renovação. No protótipo, o socorrista não renova: a página faz o login de novo.
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// Id do socorrista.
    /// </summary>
    public required Guid SocorristaId { get; init; }

    /// <summary>
    /// Nome do socorrista.
    /// </summary>
    public required string Nome { get; init; }

    /// <summary>
    /// Organização em que ele atua.
    /// </summary>
    public required string Organizacao { get; init; }

    /// <summary>
    /// Se o cadastro foi verificado (sem isso, o Cofre responde <c>403</c>).
    /// </summary>
    public required bool Verificado { get; init; }

    /// <summary>
    /// Junta os tokens e o perfil.
    /// </summary>
    /// <param name="tokens">Tokens emitidos.</param>
    /// <param name="socorrista">Socorrista da conta.</param>
    /// <returns>O corpo do login.</returns>
    public static LoginSocorristaResposta De(TokensEmitidos tokens, Socorrista socorrista)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(socorrista);

        return new LoginSocorristaResposta
        {
            AccessToken = tokens.AccessToken,
            ExpiresIn = tokens.ExpiresIn,
            RefreshToken = tokens.RefreshToken,
            SocorristaId = socorrista.Id,
            Nome = socorrista.Nome,
            Organizacao = socorrista.Organizacao,
            Verificado = socorrista.Verificado,
        };
    }
}
