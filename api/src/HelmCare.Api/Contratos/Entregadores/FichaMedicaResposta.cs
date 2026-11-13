using System;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Contratos.Entregadores;

/// <summary>
/// Ficha médica devolvida SOMENTE no <c>PUT /api/v1/entregadores/{id}/ficha-medica</c>, para quem
/// acabou de enviar os mesmos dados. Nenhuma outra rota do entregador devolve a ficha
/// (PII sensível, seção 9 do contrato).
/// </summary>
public sealed class FichaMedicaResposta
{
    /// <summary>
    /// Tipo sanguíneo.
    /// </summary>
    public required string TipoSanguineo { get; init; }

    /// <summary>
    /// Alergias (vazio quando não informado).
    /// </summary>
    public required string Alergias { get; init; }

    /// <summary>
    /// Condições críticas (vazio quando não informado).
    /// </summary>
    public required string CondicoesCriticas { get; init; }

    /// <summary>
    /// Medicamentos de uso contínuo (vazio quando não informado).
    /// </summary>
    public required string Medicamentos { get; init; }

    /// <summary>
    /// Monta a resposta a partir da entidade.
    /// </summary>
    /// <param name="ficha">Ficha médica gravada.</param>
    /// <returns>A resposta com os quatro campos.</returns>
    public static FichaMedicaResposta De(FichaMedica ficha)
    {
        ArgumentNullException.ThrowIfNull(ficha);

        return new FichaMedicaResposta
        {
            TipoSanguineo = ficha.TipoSanguineo,
            Alergias = ficha.Alergias,
            CondicoesCriticas = ficha.CondicoesCriticas,
            Medicamentos = ficha.Medicamentos,
        };
    }
}
