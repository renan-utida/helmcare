using System;

namespace HelmCare.Api.Contratos;

/// <summary>
/// Máscaras de PII das respostas (seção 9 do contrato). Recebem o valor como o domínio guarda,
/// só com os dígitos, e deixam visível apenas o final, o bastante para o entregador reconhecer
/// o próprio dado sem expor o número inteiro.
/// </summary>
public static class Mascaras
{
    /// <summary>
    /// Mascara o CPF deixando os 2 últimos dígitos: <c>***.***.***-12</c>.
    /// </summary>
    /// <param name="cpf">CPF com 11 dígitos.</param>
    /// <returns>O CPF mascarado.</returns>
    /// <exception cref="ArgumentException">Quando o CPF não tem 11 dígitos.</exception>
    public static string Cpf(string cpf)
    {
        ExigirDigitos(cpf, nameof(cpf), 11);
        return string.Concat("***.***.***-", cpf.AsSpan(9, 2));
    }

    /// <summary>
    /// Mascara o telefone deixando o DDD e os 4 últimos dígitos: <c>(11) *****-1234</c> com 11
    /// dígitos (celular) e <c>(11) ****-1234</c> com 10 (fixo).
    /// </summary>
    /// <param name="telefone">Telefone com DDD, 10 ou 11 dígitos.</param>
    /// <returns>O telefone mascarado.</returns>
    /// <exception cref="ArgumentException">Quando o telefone não tem 10 nem 11 dígitos.</exception>
    public static string Telefone(string telefone)
    {
        ExigirDigitos(telefone, nameof(telefone), 10, 11);
        string meio = new('*', telefone.Length - 6);
        return $"({telefone[..2]}) {meio}-{telefone[^4..]}";
    }

    /// <summary>
    /// Mascara a CNH deixando os 4 últimos dígitos: <c>*******1234</c>.
    /// </summary>
    /// <param name="cnh">CNH com 11 dígitos.</param>
    /// <returns>A CNH mascarada.</returns>
    /// <exception cref="ArgumentException">Quando a CNH não tem 11 dígitos.</exception>
    public static string Cnh(string cnh)
    {
        ExigirDigitos(cnh, nameof(cnh), 11);
        return string.Concat("*******", cnh.AsSpan(7, 4));
    }

    /// <summary>
    /// Mascara o e-mail deixando a primeira letra e o domínio: <c>r***@exemplo.com</c> (seção 7 do contrato).
    /// </summary>
    /// <param name="email">E-mail com um <c>@</c> que não é o primeiro caractere.</param>
    /// <returns>O e-mail mascarado.</returns>
    /// <exception cref="ArgumentException">Quando o valor não tem a forma de um e-mail.</exception>
    public static string Email(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        int arroba = email.LastIndexOf('@');

        if (arroba < 1 || arroba == email.Length - 1)
        {
            // A mensagem nunca repete o valor recebido, que é PII.
            throw new ArgumentException("O valor deve ser um e-mail.", nameof(email));
        }

        return string.Concat(email.AsSpan(0, 1), "***", email.AsSpan(arroba));
    }

    private static void ExigirDigitos(string valor, string nome, params int[] tamanhos)
    {
        ArgumentNullException.ThrowIfNull(valor, nome);

        if (Array.IndexOf(tamanhos, valor.Length) < 0 || valor.AsSpan().ContainsAnyExceptInRange('0', '9'))
        {
            // A mensagem nunca repete o valor recebido, que é PII.
            throw new ArgumentException($"O valor deve ter {string.Join(" ou ", tamanhos)} dígitos.", nome);
        }
    }
}
