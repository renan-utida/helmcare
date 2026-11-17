using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace HelmCare.Api.Contratos.Validacao;

/// <summary>
/// Regra da senha da seção 3 do contrato: de 8 a 64 caracteres, com pelo menos uma letra maiúscula,
/// uma minúscula e um dígito. Símbolo não é exigido. O Identity confere de novo as mesmas regras
/// (exceto o máximo, que só existe aqui).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SenhaAttribute : ValidationAttribute
{
    /// <summary>
    /// Tamanho mínimo da senha.
    /// </summary>
    public const int TamanhoMinimo = 8;

    /// <summary>
    /// Tamanho máximo da senha.
    /// </summary>
    public const int TamanhoMaximo = 64;

    /// <summary>
    /// Cria o atributo com a mensagem padrão em português.
    /// </summary>
    public SenhaAttribute()
        : base("O campo senha deve ter de 8 a 64 caracteres, com pelo menos uma letra maiúscula, uma minúscula e um dígito.")
    {
    }

    /// <summary>
    /// Confere a senha. Nulo passa: a obrigatoriedade é do <see cref="RequiredAttribute"/>.
    /// </summary>
    /// <param name="value">Valor recebido.</param>
    /// <returns><see langword="true"/> se a senha segue a regra.</returns>
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string senha
            && senha.Length is >= TamanhoMinimo and <= TamanhoMaximo
            && senha.Any(char.IsUpper)
            && senha.Any(char.IsLower)
            && senha.Any(char.IsDigit);
    }
}
