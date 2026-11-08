using System;
using System.Linq;

namespace HelmCare.Dominio.Comum;

/// <summary>
/// Validações reaproveitadas pelas entidades. Toda falha lança
/// <see cref="ValidacaoDominioException"/> com uma mensagem em português que cita o campo
/// pelo nome usado no contrato, para o cliente saber o que corrigir.
/// </summary>
public static class Garantir
{
    /// <summary>
    /// Exige um texto preenchido e dentro do tamanho máximo.
    /// </summary>
    /// <param name="valor">Texto recebido.</param>
    /// <param name="campo">Nome do campo no contrato, usado na mensagem.</param>
    /// <param name="tamanhoMaximo">Quantidade máxima de caracteres depois do Trim.</param>
    /// <returns>O texto sem espaços nas pontas.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o texto é nulo, vazio, só espaços ou longo demais.</exception>
    public static string TextoObrigatorio(string? valor, string campo, int tamanhoMaximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ValidacaoDominioException($"O campo {campo} é obrigatório.");
        }

        return TamanhoMaximo(valor.Trim(), campo, tamanhoMaximo);
    }

    /// <summary>
    /// Aceita um texto opcional: nulo vira <see cref="string.Empty"/>, mas o tamanho máximo continua valendo.
    /// </summary>
    /// <param name="valor">Texto recebido.</param>
    /// <param name="campo">Nome do campo no contrato, usado na mensagem.</param>
    /// <param name="tamanhoMaximo">Quantidade máxima de caracteres depois do Trim.</param>
    /// <returns>O texto sem espaços nas pontas, ou <see cref="string.Empty"/>.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o texto é longo demais.</exception>
    public static string TextoOpcional(string? valor, string campo, int tamanhoMaximo)
    {
        return TamanhoMaximo(valor?.Trim() ?? string.Empty, campo, tamanhoMaximo);
    }

    /// <summary>
    /// Remove a máscara (pontos, traços, parênteses, espaços) e confere a quantidade de dígitos.
    /// Usado para CPF, CNH e telefone, que são guardados só com dígitos.
    /// </summary>
    /// <param name="valor">Texto recebido, com ou sem máscara.</param>
    /// <param name="campo">Nome do campo no contrato, usado na mensagem.</param>
    /// <param name="tamanhosAceitos">Quantidades de dígitos aceitas (ex.: 10 ou 11 para telefone).</param>
    /// <returns>Somente os dígitos.</returns>
    /// <exception cref="ValidacaoDominioException">Quando a quantidade de dígitos não está entre as aceitas.</exception>
    public static string SoDigitos(string? valor, string campo, params int[] tamanhosAceitos)
    {
        ArgumentNullException.ThrowIfNull(tamanhosAceitos);

        string digitos = new((valor ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

        if (!tamanhosAceitos.Contains(digitos.Length))
        {
            throw new ValidacaoDominioException(
                $"O campo {campo} deve ter {string.Join(" ou ", tamanhosAceitos)} dígitos.");
        }

        return digitos;
    }

    /// <summary>
    /// Exige um instante em UTC (<see cref="DateTimeKind.Utc"/>). Datas locais ou sem fuso
    /// seriam gravadas e comparadas errado, então são barradas na entrada do domínio.
    /// </summary>
    /// <param name="instante">Instante recebido.</param>
    /// <param name="campo">Nome do campo no contrato, usado na mensagem.</param>
    /// <returns>O próprio instante, para permitir atribuição direta.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o <see cref="DateTime.Kind"/> não é UTC.</exception>
    public static DateTime Utc(DateTime instante, string campo)
    {
        if (instante.Kind != DateTimeKind.Utc)
        {
            throw new ValidacaoDominioException($"O campo {campo} deve estar em UTC.");
        }

        return instante;
    }

    /// <summary>
    /// Exige um número inteiro dentro de um intervalo fechado.
    /// </summary>
    /// <param name="valor">Número recebido.</param>
    /// <param name="campo">Nome do campo no contrato, usado na mensagem.</param>
    /// <param name="minimo">Menor valor aceito.</param>
    /// <param name="maximo">Maior valor aceito.</param>
    /// <returns>O próprio número.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o número está fora do intervalo.</exception>
    public static int Intervalo(int valor, string campo, int minimo, int maximo)
    {
        if (valor < minimo || valor > maximo)
        {
            throw new ValidacaoDominioException($"O campo {campo} deve estar entre {minimo} e {maximo}.");
        }

        return valor;
    }

    /// <summary>
    /// Exige um identificador preenchido (diferente de <see cref="Guid.Empty"/>).
    /// </summary>
    /// <param name="id">Identificador recebido.</param>
    /// <param name="campo">Nome do campo no contrato, usado na mensagem.</param>
    /// <returns>O próprio identificador.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o identificador é vazio.</exception>
    public static Guid IdInformado(Guid id, string campo)
    {
        if (id == Guid.Empty)
        {
            throw new ValidacaoDominioException($"O campo {campo} é obrigatório.");
        }

        return id;
    }

    /// <summary>
    /// Exige que um valor de enum seja um membro declarado (barra, por exemplo, <c>(FaixaRisco)99</c>).
    /// </summary>
    /// <typeparam name="TEnum">Tipo do enum.</typeparam>
    /// <param name="valor">Valor recebido.</param>
    /// <param name="campo">Nome do campo no contrato, usado na mensagem.</param>
    /// <returns>O próprio valor.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o valor não é um membro do enum.</exception>
    public static TEnum Definido<TEnum>(TEnum valor, string campo)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(valor))
        {
            throw new ValidacaoDominioException($"O campo {campo} tem um valor inválido.");
        }

        return valor;
    }

    private static string TamanhoMaximo(string texto, string campo, int tamanhoMaximo)
    {
        if (texto.Length > tamanhoMaximo)
        {
            throw new ValidacaoDominioException(
                $"O campo {campo} deve ter no máximo {tamanhoMaximo} caracteres.");
        }

        return texto;
    }
}
