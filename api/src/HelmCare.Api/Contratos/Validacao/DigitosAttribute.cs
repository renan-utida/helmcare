using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace HelmCare.Api.Contratos.Validacao;

/// <summary>
/// Valida um documento ou telefone que pode vir com ou sem máscara (seção 3 do contrato):
/// aceita só dígitos e os caracteres de máscara (ponto, hífen, parênteses e espaço), e a
/// quantidade de dígitos precisa ser uma das aceitas. Valor nulo passa, porque quem exige o
/// campo é o <see cref="RequiredAttribute"/>. O domínio repete a regra ao guardar só os dígitos.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class DigitosAttribute : ValidationAttribute
{
    /// <summary>
    /// Cria a validação com as quantidades de dígitos aceitas.
    /// </summary>
    /// <param name="quantidadesAceitas">Quantidades aceitas (ex.: 10 e 11 para telefone).</param>
    public DigitosAttribute(params int[] quantidadesAceitas)
    {
        QuantidadesAceitas = quantidadesAceitas;
    }

    /// <summary>
    /// Quantidades de dígitos aceitas.
    /// </summary>
    public int[] QuantidadesAceitas { get; }

    /// <inheritdoc/>
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string texto || !texto.All(c => char.IsAsciiDigit(c) || ".-() ".Contains(c, StringComparison.Ordinal)))
        {
            return false;
        }

        int digitos = texto.Count(char.IsAsciiDigit);
        return QuantidadesAceitas.Contains(digitos);
    }
}
