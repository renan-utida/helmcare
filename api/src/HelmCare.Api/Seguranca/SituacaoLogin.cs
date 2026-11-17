namespace HelmCare.Api.Seguranca;

/// <summary>
/// Situação de uma tentativa de login.
/// </summary>
public enum SituacaoLogin
{
    /// <summary>
    /// E-mail e senha conferem.
    /// </summary>
    Valida,

    /// <summary>
    /// E-mail desconhecido ou senha errada.
    /// </summary>
    Invalida,

    /// <summary>
    /// Conta bloqueada por tentativas erradas (mesmo com a senha certa).
    /// </summary>
    Bloqueada,
}
