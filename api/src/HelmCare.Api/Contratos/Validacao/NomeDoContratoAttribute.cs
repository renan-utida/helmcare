using System;
using System.ComponentModel.DataAnnotations;
using HelmCare.Dominio.Comum;

namespace HelmCare.Api.Contratos.Validacao;

/// <summary>
/// Valida um texto que precisa ser um dos nomes do contrato de um enum do domínio (ex.:
/// <c>ENTREGADOR</c> ou <c>VIGIA_MOTO</c>), com a mesma leitura do <see cref="NomeContrato"/>: exata
/// e sensível a maiúsculas. Valor nulo passa, porque quem exige o campo é o <see cref="RequiredAttribute"/>.
/// </summary>
/// <remarks>
/// O campo do DTO fica como texto, e não como o enum, para um valor inválido cair aqui, com a
/// mensagem em português: com o enum, o desserializador recusaria antes, com uma mensagem em
/// inglês que cita o tipo interno do C#.
/// </remarks>
/// <typeparam name="TEnum">Enum do domínio com o nome do contrato em cada membro.</typeparam>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NomeDoContratoAttribute<TEnum> : ValidationAttribute
    where TEnum : struct, Enum
{
    /// <inheritdoc/>
    public override bool IsValid(object? value)
    {
        return value is null || (value is string texto && NomeContrato.TentarLer<TEnum>(texto, out _));
    }
}
