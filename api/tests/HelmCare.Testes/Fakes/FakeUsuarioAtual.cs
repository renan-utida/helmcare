using System;
using HelmCare.Api.Seguranca;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Usuário do token, montado pelo teste: anônimo, entregador ou socorrista.
/// </summary>
public sealed class FakeUsuarioAtual : IUsuarioAtual
{
    /// <inheritdoc/>
    public bool Autenticado { get; init; }

    /// <inheritdoc/>
    public bool EhEntregador { get; init; }

    /// <inheritdoc/>
    public Guid? EntregadorId { get; init; }

    /// <inheritdoc/>
    public Guid? SocorristaId { get; init; }

    /// <summary>
    /// Requisição sem token.
    /// </summary>
    /// <returns>O usuário anônimo.</returns>
    public static FakeUsuarioAtual Anonimo()
    {
        return new FakeUsuarioAtual();
    }

    /// <summary>
    /// Token de entregador.
    /// </summary>
    /// <param name="entregadorId">Id do entregador do token.</param>
    /// <returns>O usuário.</returns>
    public static FakeUsuarioAtual Entregador(Guid entregadorId)
    {
        return new FakeUsuarioAtual { Autenticado = true, EhEntregador = true, EntregadorId = entregadorId };
    }

    /// <summary>
    /// Token de socorrista.
    /// </summary>
    /// <returns>O usuário.</returns>
    public static FakeUsuarioAtual Socorrista()
    {
        return new FakeUsuarioAtual { Autenticado = true, SocorristaId = Guid.CreateVersion7() };
    }
}
