using System;

namespace HelmCare.Api.Seguranca;

/// <summary>
/// Quem fez a requisição, lido das claims do token. O roteador MQTT e o laço em segundo plano não
/// têm usuário e não usam esta interface.
/// </summary>
public interface IUsuarioAtual
{
    /// <summary>
    /// Verdadeiro quando a requisição trouxe um token válido.
    /// </summary>
    bool Autenticado { get; }

    /// <summary>
    /// Verdadeiro quando o token tem o papel <see cref="Papeis.Entregador"/>.
    /// </summary>
    bool EhEntregador { get; }

    /// <summary>
    /// Verdadeiro quando o token tem o papel <see cref="Papeis.Socorrista"/>.
    /// </summary>
    bool EhSocorrista { get; }

    /// <summary>
    /// Id do entregador do token, ou nulo.
    /// </summary>
    Guid? EntregadorId { get; }

    /// <summary>
    /// Id do socorrista do token, ou nulo.
    /// </summary>
    Guid? SocorristaId { get; }
}
