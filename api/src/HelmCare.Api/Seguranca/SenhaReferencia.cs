using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace HelmCare.Api.Seguranca;

/// <summary>
/// Hash de uma senha aleatória, gerado uma vez na subida da API. No login com um e-mail desconhecido,
/// a senha informada é conferida contra este hash: o trabalho (PBKDF2) é o mesmo de uma senha errada,
/// e o tempo de resposta não revela se o e-mail existe.
/// </summary>
/// <remarks>
/// A senha aleatória é descartada logo depois do hash e nunca confere com nada. O hash usa o formato
/// e o número de iterações padrão do Identity, os mesmos dos hashes das contas reais.
/// </remarks>
public sealed class SenhaReferencia
{
    /// <summary>
    /// Gera o hash de referência.
    /// </summary>
    public SenhaReferencia()
    {
        string aleatoria = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Hash = new PasswordHasher<IdentityUser>().HashPassword(Usuario, aleatoria);
    }

    /// <summary>
    /// Usuário vazio usado só como argumento do hasher (o hasher padrão não lê nenhum campo dele).
    /// </summary>
    public IdentityUser Usuario { get; } = new();

    /// <summary>
    /// Hash da senha aleatória.
    /// </summary>
    public string Hash { get; }
}
