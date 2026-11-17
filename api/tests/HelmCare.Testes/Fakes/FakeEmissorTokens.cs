using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Seguranca;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Emissor que gera tokens previsíveis (<c>acesso-{usuarioId}</c> e <c>renovacao-{usuarioId}</c>) e só
/// aceita como renovação os que ele mesmo emitiu e o teste não revogou.
/// </summary>
public sealed class FakeEmissorTokens : IEmissorTokens
{
    /// <summary>
    /// Validade informada do token de acesso, em segundos.
    /// </summary>
    public const long ValidadeSegundos = 3600;

    private readonly Dictionary<string, string> _renovacoes = [];

    /// <inheritdoc/>
    public Task<TokensEmitidos> EmitirAsync(string usuarioId, CancellationToken cancellationToken)
    {
        string renovacao = $"renovacao-{usuarioId}";
        _renovacoes[renovacao] = usuarioId;
        return Task.FromResult(new TokensEmitidos($"acesso-{usuarioId}", ValidadeSegundos, renovacao));
    }

    /// <inheritdoc/>
    public Task<string?> ValidarRenovacaoAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return Task.FromResult(_renovacoes.GetValueOrDefault(refreshToken));
    }

    /// <summary>
    /// Simula o token de renovação vencido ou com o security stamp trocado.
    /// </summary>
    /// <param name="refreshToken">Token a revogar.</param>
    public void Revogar(string refreshToken)
    {
        _renovacoes.Remove(refreshToken);
    }
}
