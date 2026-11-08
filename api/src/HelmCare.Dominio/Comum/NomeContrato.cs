using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;

namespace HelmCare.Dominio.Comum;

/// <summary>
/// Converte os enums do domínio para o nome definido no contrato de integração
/// (UPPER_SNAKE_CASE) e de volta, lendo o atributo <see cref="JsonStringEnumMemberNameAttribute"/>
/// de cada membro. É a mesma fonte usada pelo serializador JSON, então o banco, o MQTT
/// e a API usam sempre o mesmo texto.
/// </summary>
public static class NomeContrato
{
    /// <summary>
    /// Devolve o nome do contrato de um valor de enum (ex.: <c>SosCamada1</c> vira <c>SOS_CAMADA_1</c>).
    /// </summary>
    /// <typeparam name="TEnum">Tipo do enum do domínio.</typeparam>
    /// <param name="valor">Valor a converter.</param>
    /// <returns>O texto exato do contrato.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Quando o valor não é um membro declarado do enum.</exception>
    public static string Para<TEnum>(TEnum valor)
        where TEnum : struct, Enum
    {
        if (Cache<TEnum>.PorValor.TryGetValue(valor, out string? nome))
        {
            return nome;
        }

        throw new ArgumentOutOfRangeException(
            nameof(valor),
            valor,
            $"O valor não é um membro declarado de {typeof(TEnum).Name}.");
    }

    /// <summary>
    /// Tenta ler um texto do contrato e devolver o membro correspondente do enum.
    /// A comparação é ordinal e sensível a maiúsculas, porque o contrato define o texto exato.
    /// </summary>
    /// <typeparam name="TEnum">Tipo do enum do domínio.</typeparam>
    /// <param name="texto">Texto recebido (ex.: <c>SOS_CAMADA_1</c>).</param>
    /// <param name="valor">Membro encontrado, ou o valor padrão quando o texto é inválido.</param>
    /// <returns><see langword="true"/> quando o texto é um nome válido do contrato.</returns>
    public static bool TentarLer<TEnum>(string? texto, out TEnum valor)
        where TEnum : struct, Enum
    {
        if (texto is not null && Cache<TEnum>.PorNome.TryGetValue(texto, out valor))
        {
            return true;
        }

        valor = default;
        return false;
    }

    /// <summary>
    /// Cache por tipo de enum: o runtime cria uma instância estática por <typeparamref name="TEnum"/>,
    /// então a reflexão roda uma única vez por enum, na primeira conversão.
    /// </summary>
    /// <typeparam name="TEnum">Tipo do enum do domínio.</typeparam>
    private static class Cache<TEnum>
        where TEnum : struct, Enum
    {
        /// <summary>
        /// Dicionário do membro do enum para o nome do contrato.
        /// </summary>
        public static readonly FrozenDictionary<TEnum, string> PorValor = MontarPorValor();

        /// <summary>
        /// Dicionário do nome do contrato para o membro do enum, com comparação ordinal.
        /// Inicializado depois de <see cref="PorValor"/>, porque os campos estáticos
        /// são inicializados na ordem em que aparecem no arquivo.
        /// </summary>
        public static readonly FrozenDictionary<string, TEnum> PorNome =
            PorValor.ToFrozenDictionary(par => par.Value, par => par.Key, StringComparer.Ordinal);

        /// <summary>
        /// Lê o atributo <see cref="JsonStringEnumMemberNameAttribute"/> de cada membro do enum.
        /// </summary>
        /// <returns>O mapa do membro para o nome do contrato.</returns>
        /// <exception cref="InvalidOperationException">Quando algum membro não tem o atributo.</exception>
        private static FrozenDictionary<TEnum, string> MontarPorValor()
        {
            Dictionary<TEnum, string> porValor = [];

            foreach (FieldInfo campo in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                JsonStringEnumMemberNameAttribute atributo =
                    campo.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()
                    ?? throw new InvalidOperationException(
                        $"O membro {typeof(TEnum).Name}.{campo.Name} não tem o atributo "
                        + "[JsonStringEnumMemberName] com o nome do contrato.");

                var membro = (TEnum)campo.GetValue(null)!;
                porValor.Add(membro, atributo.Name);
            }

            return porValor.ToFrozenDictionary();
        }
    }
}
