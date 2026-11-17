using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Comum;
using Microsoft.AspNetCore.Identity;

namespace HelmCare.Api.Seguranca;

/// <summary>
/// <see cref="IContasIdentity"/> com o <see cref="UserManager{TUser}"/> e o
/// <see cref="SignInManager{TUser}"/> do ASP.NET Core Identity. O e-mail também é o nome de usuário:
/// o índice único do nome normalizado (maiúsculas) segura o e-mail repetido até na corrida.
/// </summary>
public sealed class ContasIdentity : IContasIdentity
{
    /// <summary>
    /// Mensagem do <c>400</c> de senha fora da regra (seção 3 do contrato).
    /// </summary>
    public const string MensagemSenhaInvalida =
        "O campo senha deve ter de 8 a 64 caracteres, com pelo menos uma letra maiúscula, uma minúscula e um dígito.";

    /// <summary>
    /// Mensagem do <c>400</c> de e-mail fora da regra.
    /// </summary>
    public const string MensagemEmailInvalido = "O campo email deve ser um e-mail válido, com até 254 caracteres.";

    private readonly UserManager<IdentityUser> _usuarios;
    private readonly SignInManager<IdentityUser> _entrada;
    private readonly SenhaReferencia _referencia;

    /// <summary>
    /// Cria o serviço de contas.
    /// </summary>
    /// <param name="usuarios">Gerenciador de usuários do Identity.</param>
    /// <param name="entrada">Confere a senha e aplica o bloqueio.</param>
    /// <param name="referencia">Hash de referência para o e-mail desconhecido (mesmo tempo de uma senha errada).</param>
    public ContasIdentity(UserManager<IdentityUser> usuarios, SignInManager<IdentityUser> entrada, SenhaReferencia referencia)
    {
        _usuarios = usuarios;
        _entrada = entrada;
        _referencia = referencia;
    }

    /// <inheritdoc/>
    public async Task<bool> EmailEmUsoAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _usuarios.FindByEmailAsync(email) is not null;
    }

    /// <inheritdoc/>
    public async Task<string> CriarAsync(string email, string senha, string papel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var usuario = new IdentityUser { UserName = email, Email = email };

        ExigirSucesso(await _usuarios.CreateAsync(usuario, senha));
        ExigirSucesso(await _usuarios.AddToRoleAsync(usuario, papel));

        return usuario.Id;
    }

    /// <inheritdoc/>
    public async Task<VerificacaoSenha> VerificarSenhaAsync(string email, string senha, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IdentityUser? usuario = await _usuarios.FindByEmailAsync(email);

        if (usuario is null)
        {
            // Mesmo trabalho de hash de uma senha errada, para o tempo de resposta não revelar que o
            // e-mail não existe. O resultado é descartado: o hash de referência nunca confere.
            _ = _usuarios.PasswordHasher.VerifyHashedPassword(_referencia.Usuario, _referencia.Hash, senha);
            return new VerificacaoSenha(SituacaoLogin.Invalida, null, []);
        }

        // lockoutOnFailure: cada senha errada conta para o bloqueio (5 tentativas, 15 min).
        SignInResult resultado = await _entrada.CheckPasswordSignInAsync(usuario, senha, lockoutOnFailure: true);

        if (resultado.IsLockedOut)
        {
            return new VerificacaoSenha(SituacaoLogin.Bloqueada, usuario.Id, []);
        }

        if (!resultado.Succeeded)
        {
            return new VerificacaoSenha(SituacaoLogin.Invalida, usuario.Id, []);
        }

        IList<string> papeis = await _usuarios.GetRolesAsync(usuario);
        return new VerificacaoSenha(SituacaoLogin.Valida, usuario.Id, papeis.ToList());
    }

    /// <inheritdoc/>
    public async Task<string?> ObterEmailAsync(string usuarioId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IdentityUser? usuario = await _usuarios.FindByIdAsync(usuarioId);
        return usuario?.Email;
    }

    // Os erros do Identity viram as exceções que o TratadorExcecoes já conhece, com textos em
    // português e sem repetir o valor recebido. Os DTOs já validam o mesmo antes, então isto é a
    // segunda barreira (e o que segura o e-mail repetido).
    private static void ExigirSucesso(IdentityResult resultado)
    {
        if (resultado.Succeeded)
        {
            return;
        }

        List<string> codigos = resultado.Errors.Select(e => e.Code).ToList();

        if (codigos.Exists(c => c is "DuplicateEmail" or "DuplicateUserName"))
        {
            throw new RegraNegocioException(IContasIdentity.MensagemEmailRepetido);
        }

        if (codigos.Exists(c => c.StartsWith("Password", StringComparison.Ordinal)))
        {
            throw new ValidacaoDominioException(MensagemSenhaInvalida);
        }

        if (codigos.Exists(c => c is "InvalidEmail" or "InvalidUserName"))
        {
            throw new ValidacaoDominioException(MensagemEmailInvalido);
        }

        throw new InvalidOperationException($"O Identity recusou a operação ({string.Join(", ", codigos)}).");
    }
}
