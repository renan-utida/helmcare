using System.Collections.Generic;

namespace HelmCare.Api.Seguranca;

/// <summary>
/// Resultado da conferência da senha no login.
/// </summary>
/// <param name="Situacao">Se a senha confere, não confere ou a conta está bloqueada.</param>
/// <param name="UsuarioId">Id do usuário quando o e-mail existe; nulo para um e-mail desconhecido.</param>
/// <param name="Papeis">Papéis da conta (vazio quando a senha não confere).</param>
public sealed record VerificacaoSenha(SituacaoLogin Situacao, string? UsuarioId, IReadOnlyList<string> Papeis);
