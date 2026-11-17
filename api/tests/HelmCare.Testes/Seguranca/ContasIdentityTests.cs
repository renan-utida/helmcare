using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Seguranca;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Seguranca;

/// <summary>
/// Testes do <see cref="ContasIdentity"/> com o <see cref="UserManager{TUser}"/> e o
/// <see cref="SignInManager{TUser}"/> reais, sobre uma loja de usuários vazia em memória.
/// </summary>
[TestClass]
public sealed class ContasIdentityTests
{
    /// <summary>
    /// Garante que o login com um e-mail desconhecido faz a mesma verificação de hash de uma senha
    /// errada, contra o hash de referência gerado na subida, e responde inválido sem id.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task VerificarSenhaAsync_EmailDesconhecido_VerificaOHashDeReferencia()
    {
        // Arrange
        var hasher = new HasherQueConta();
        var referencia = new SenhaReferencia();
        using var loja = new LojaVazia();
        using UserManager<IdentityUser> usuarios = Usuarios(loja, hasher);
        ContasIdentity contas = Criar(usuarios, referencia);

        // Act
        VerificacaoSenha resultado = await contas.VerificarSenhaAsync("ninguem@exemplo.com", "Senha123", CancellationToken.None);

        // Assert
        Assert.AreEqual(SituacaoLogin.Invalida, resultado.Situacao);
        Assert.IsNull(resultado.UsuarioId);
        Assert.AreEqual(1, hasher.Verificacoes.Count);
        Assert.AreEqual((referencia.Hash, "Senha123"), hasher.Verificacoes[0]);
    }

    /// <summary>
    /// Garante que o hash de referência é um hash real do Identity, que nunca confere com uma senha comum.
    /// </summary>
    [TestMethod]
    public void SenhaReferencia_Hash_NaoConfereComSenhaComum()
    {
        // Arrange
        var referencia = new SenhaReferencia();

        // Act
        PasswordVerificationResult resultado = new PasswordHasher<IdentityUser>()
            .VerifyHashedPassword(referencia.Usuario, referencia.Hash, "Senha123");

        // Assert
        Assert.AreEqual(PasswordVerificationResult.Failed, resultado);
        Assert.AreNotEqual(new SenhaReferencia().Hash, referencia.Hash);
    }

    private static UserManager<IdentityUser> Usuarios(LojaVazia loja, IPasswordHasher<IdentityUser> hasher)
    {
        return new UserManager<IdentityUser>(
            loja,
            Options.Create(new IdentityOptions()),
            hasher,
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<IdentityUser>>.Instance);
    }

    private static ContasIdentity Criar(UserManager<IdentityUser> usuarios, SenhaReferencia referencia)
    {
        IOptions<IdentityOptions> opcoes = Options.Create(new IdentityOptions());
        var entrada = new SignInManager<IdentityUser>(
            usuarios,
            new HttpContextAccessor(),
            new UserClaimsPrincipalFactory<IdentityUser>(usuarios, opcoes),
            opcoes,
            NullLogger<SignInManager<IdentityUser>>.Instance,
            new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions())),
            new DefaultUserConfirmation<IdentityUser>());

        return new ContasIdentity(usuarios, entrada, referencia);
    }

    // Hasher que anota cada verificação (hash e senha recebidos) e sempre responde que não confere.
    private sealed class HasherQueConta : IPasswordHasher<IdentityUser>
    {
        public List<(string Hash, string Senha)> Verificacoes { get; } = [];

        public string HashPassword(IdentityUser user, string password)
        {
            return "hash";
        }

        public PasswordVerificationResult VerifyHashedPassword(IdentityUser user, string hashedPassword, string providedPassword)
        {
            Verificacoes.Add((hashedPassword, providedPassword));
            return PasswordVerificationResult.Failed;
        }
    }

    // Loja sem nenhum usuário: toda busca por e-mail volta nula; o resto não é usado neste teste.
    private sealed class LojaVazia : IUserEmailStore<IdentityUser>
    {
        public Task<IdentityUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            return Task.FromResult<IdentityUser?>(null);
        }

        public Task<IdentityUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IdentityUser?>(null);
        }

        public Task<IdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            return Task.FromResult<IdentityUser?>(null);
        }

        public void Dispose()
        {
        }

        public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string?> GetUserNameAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SetUserNameAsync(IdentityUser user, string? userName, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string?> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SetNormalizedUserNameAsync(IdentityUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SetEmailAsync(IdentityUser user, string? email, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string?> GetEmailAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> GetEmailConfirmedAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SetEmailConfirmedAsync(IdentityUser user, bool confirmed, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string?> GetNormalizedEmailAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SetNormalizedEmailAsync(IdentityUser user, string? normalizedEmail, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
