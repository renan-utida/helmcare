using System;
using HelmCare.Dominio.Comum;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HelmCare.Api.Persistencia.Conversores;

/// <summary>
/// Grava os enums do domínio no banco com o nome exato do contrato (ex.: <c>SOS_CAMADA_1</c>),
/// usando o mesmo <see cref="NomeContrato"/> do JSON e do MQTT. Assim o banco, a API e o
/// capacete usam sempre o mesmo texto, e uma consulta SQL manual mostra os valores do contrato.
/// </summary>
/// <typeparam name="TEnum">Tipo do enum do domínio.</typeparam>
public sealed class NomeContratoConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Tamanho máximo da coluna de texto de todos os enums (o maior nome hoje tem 25 caracteres).
    /// </summary>
    public const int TamanhoMaximo = 30;

    /// <summary>
    /// Cria o conversor nos dois sentidos.
    /// </summary>
    public NomeContratoConverter()
        : base(valor => NomeContrato.Para(valor), texto => Ler(texto))
    {
    }

    // Um texto fora do contrato no banco é corrupção de dados (alguém editou a tabela à mão
    // ou um enum foi renomeado sem migration): falhar alto é melhor que inventar um valor.
    private static TEnum Ler(string texto)
    {
        if (NomeContrato.TentarLer(texto, out TEnum valor))
        {
            return valor;
        }

        throw new InvalidOperationException(
            $"O valor '{texto}' gravado no banco não é um nome do contrato para {typeof(TEnum).Name}.");
    }
}
