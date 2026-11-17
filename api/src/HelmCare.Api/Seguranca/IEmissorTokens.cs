using System.Threading;
using System.Threading.Tasks;

namespace HelmCare.Api.Seguranca;

/// <summary>
/// Emite e renova os tokens do login. Os serviços dependem desta interface, e não do Identity, para
/// os testes trocarem a emissão por um fake.
/// </summary>
public interface IEmissorTokens
{
    /// <summary>
    /// Emite um par de tokens novo para o usuário, com as claims da <see cref="FabricaClaimsUsuario"/>.
    /// </summary>
    /// <param name="usuarioId">Id do usuário do Identity.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>Os tokens emitidos.</returns>
    Task<TokensEmitidos> EmitirAsync(string usuarioId, CancellationToken cancellationToken);

    /// <summary>
    /// Confere o token de renovação como o <c>/refresh</c> do <c>MapIdentityApi</c>: legível, dentro
    /// da validade e com o security stamp ainda igual ao do usuário (uma troca de senha invalida os
    /// tokens antigos).
    /// </summary>
    /// <param name="refreshToken">Token de renovação recebido.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O id do usuário, ou nulo quando o token não vale.</returns>
    Task<string?> ValidarRenovacaoAsync(string refreshToken, CancellationToken cancellationToken);
}
