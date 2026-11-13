using System;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Contratos.Entregadores;

/// <summary>
/// Contato de emergência devolvido no <c>POST /api/v1/entregadores/{id}/contatos</c> e na lista
/// de contatos do entregador, com o telefone mascarado.
/// </summary>
public sealed class ContatoResposta
{
    /// <summary>
    /// Id do contato.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Nome do contato.
    /// </summary>
    public required string Nome { get; init; }

    /// <summary>
    /// Telefone mascarado: <c>(11) *****-1234</c> ou <c>(11) ****-1234</c>.
    /// </summary>
    public required string Telefone { get; init; }

    /// <summary>
    /// Relação com o entregador.
    /// </summary>
    public required string Parentesco { get; init; }

    /// <summary>
    /// Monta a resposta a partir da entidade, mascarando o telefone.
    /// </summary>
    /// <param name="contato">Contato gravado.</param>
    /// <returns>A resposta mascarada.</returns>
    public static ContatoResposta De(ContatoEmergencia contato)
    {
        ArgumentNullException.ThrowIfNull(contato);

        return new ContatoResposta
        {
            Id = contato.Id,
            Nome = contato.Nome,
            Telefone = Mascaras.Telefone(contato.Telefone),
            Parentesco = contato.Parentesco,
        };
    }
}
