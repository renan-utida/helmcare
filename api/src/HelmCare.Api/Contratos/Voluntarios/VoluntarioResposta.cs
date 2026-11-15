using System;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Contratos.Voluntarios;

/// <summary>
/// Voluntário devolvido pelo <c>POST /api/v1/voluntarios</c> e pelo
/// <c>PUT /api/v1/voluntarios/{id}/disponibilidade</c> (seção 7 do contrato).
/// </summary>
/// <remarks>
/// A posição é localização pessoal (seção 9): só volta nestas respostas, para o próprio voluntário.
/// </remarks>
public sealed class VoluntarioResposta
{
    /// <summary>
    /// Id do voluntário (é ele que identifica o voluntário nas rotas da rede).
    /// </summary>
    public required Guid VoluntarioId { get; init; }

    /// <summary>
    /// Entregador que é este voluntário.
    /// </summary>
    public required Guid EntregadorId { get; init; }

    /// <summary>
    /// Tipo de voluntário.
    /// </summary>
    public required TipoVoluntario Tipo { get; init; }

    /// <summary>
    /// Se o voluntário está recebendo chamados (falso no cadastro).
    /// </summary>
    public required bool Disponivel { get; init; }

    /// <summary>
    /// Latitude da última posição, ou nulo quando indisponível.
    /// </summary>
    /// <example>-23.5614</example>
    public required double? Latitude { get; init; }

    /// <summary>
    /// Longitude da última posição, ou nulo quando indisponível.
    /// </summary>
    /// <example>-46.6559</example>
    public required double? Longitude { get; init; }

    /// <summary>
    /// Monta a resposta a partir da entidade.
    /// </summary>
    /// <param name="voluntario">Voluntário gravado.</param>
    /// <returns>A resposta.</returns>
    public static VoluntarioResposta De(Voluntario voluntario)
    {
        ArgumentNullException.ThrowIfNull(voluntario);

        return new VoluntarioResposta
        {
            VoluntarioId = voluntario.Id,
            EntregadorId = voluntario.EntregadorId,
            Tipo = voluntario.Tipo,
            Disponivel = voluntario.Disponivel,
            Latitude = voluntario.Latitude,
            Longitude = voluntario.Longitude,
        };
    }
}
