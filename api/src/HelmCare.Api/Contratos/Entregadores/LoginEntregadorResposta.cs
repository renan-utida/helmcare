using System;
using HelmCare.Api.Seguranca;

namespace HelmCare.Api.Contratos.Entregadores;

/// <summary>
/// Corpo do <c>200</c> do <c>POST /api/v1/entregadores/login</c> e do
/// <c>POST /api/v1/entregadores/renovar-token</c> (seção 7 do contrato): os tokens e a sessão.
/// Os tokens nunca vão para o log.
/// </summary>
public sealed class LoginEntregadorResposta
{
    /// <summary>
    /// Tipo do token, sempre <c>Bearer</c>.
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// Token de acesso, enviado no cabeçalho <c>Authorization: Bearer ...</c>.
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// Validade do token de acesso, em segundos.
    /// </summary>
    public required long ExpiresIn { get; init; }

    /// <summary>
    /// Token de renovação, trocado por um par novo no <c>POST /entregadores/renovar-token</c>.
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// Id do entregador do token.
    /// </summary>
    public required Guid EntregadorId { get; init; }

    /// <summary>
    /// Nome do entregador.
    /// </summary>
    public required string Nome { get; init; }

    /// <summary>
    /// Id do voluntário do entregador, ou nulo.
    /// </summary>
    public required Guid? VoluntarioId { get; init; }

    /// <summary>
    /// Código do capacete vinculado, ou nulo.
    /// </summary>
    public required string? CodigoDispositivo { get; init; }

    /// <summary>
    /// Id do turno ativo, ou nulo.
    /// </summary>
    public required Guid? TurnoAtivoId { get; init; }

    /// <summary>
    /// Id da ocorrência ativa do entregador como vítima, ou nulo.
    /// </summary>
    public required Guid? OcorrenciaAtivaId { get; init; }

    /// <summary>
    /// Junta os tokens e a sessão.
    /// </summary>
    /// <param name="tokens">Tokens emitidos.</param>
    /// <param name="sessao">Sessão do entregador.</param>
    /// <returns>O corpo do login.</returns>
    public static LoginEntregadorResposta De(TokensEmitidos tokens, SessaoEntregadorResposta sessao)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(sessao);

        return new LoginEntregadorResposta
        {
            AccessToken = tokens.AccessToken,
            ExpiresIn = tokens.ExpiresIn,
            RefreshToken = tokens.RefreshToken,
            EntregadorId = sessao.EntregadorId,
            Nome = sessao.Nome,
            VoluntarioId = sessao.VoluntarioId,
            CodigoDispositivo = sessao.CodigoDispositivo,
            TurnoAtivoId = sessao.TurnoAtivoId,
            OcorrenciaAtivaId = sessao.OcorrenciaAtivaId,
        };
    }
}
