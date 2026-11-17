namespace HelmCare.Api.Seguranca;

/// <summary>
/// Nomes das claims próprias do HelmCare no token: o id do perfil de quem fez o login. O papel vai
/// na claim de papel padrão do Identity.
/// </summary>
public static class ClaimsHelmCare
{
    /// <summary>
    /// Id do <c>Entregador</c> do usuário (só no papel <see cref="Papeis.Entregador"/>).
    /// </summary>
    public const string EntregadorId = "entregadorId";

    /// <summary>
    /// Id do <c>Socorrista</c> do usuário (só no papel <see cref="Papeis.Socorrista"/>).
    /// </summary>
    public const string SocorristaId = "socorristaId";
}
