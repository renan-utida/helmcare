namespace HelmCare.Api.Seguranca;

/// <summary>
/// Par de tokens do login e da renovação. Os dois só saem na resposta dessas rotas e nunca vão
/// para o log (seção 9 do contrato).
/// </summary>
/// <param name="AccessToken">Token de acesso (bearer), opaco, protegido pelo Data Protection.</param>
/// <param name="ExpiresIn">Validade do token de acesso, em segundos.</param>
/// <param name="RefreshToken">Token de renovação, trocado por um par novo no <c>POST /entregadores/renovar-token</c>.</param>
public sealed record TokensEmitidos(string AccessToken, long ExpiresIn, string RefreshToken);
