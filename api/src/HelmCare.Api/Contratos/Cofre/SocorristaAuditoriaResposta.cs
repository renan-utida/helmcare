namespace HelmCare.Api.Contratos.Cofre;

/// <summary>
/// Socorrista de uma linha da auditoria do Cofre.
/// </summary>
public sealed class SocorristaAuditoriaResposta
{
    /// <summary>
    /// Nome do socorrista.
    /// </summary>
    public required string Nome { get; init; }

    /// <summary>
    /// Organização em que ele atua.
    /// </summary>
    public required string Organizacao { get; init; }
}
