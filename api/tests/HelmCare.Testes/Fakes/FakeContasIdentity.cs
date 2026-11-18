using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Seguranca;
using HelmCare.Dominio.Comum;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Contas em memória, com a mesma regra de e-mail sem diferenciar maiúsculas e o bloqueio
/// controlado pelo teste.
/// </summary>
public sealed class FakeContasIdentity : IContasIdentity
{
    private readonly List<ContaFake> _contas = [];

    /// <summary>
    /// Contas criadas (inclusive as semeadas).
    /// </summary>
    public IReadOnlyList<ContaFake> Contas => _contas;

    /// <summary>
    /// Quantas vezes a senha foi conferida (inclusive com e-mail desconhecido, como o ContasIdentity real faz com o hash).
    /// </summary>
    public int Verificacoes { get; private set; }

    /// <summary>
    /// Semeia uma conta.
    /// </summary>
    /// <param name="email">E-mail.</param>
    /// <param name="senha">Senha.</param>
    /// <param name="papel">Papel.</param>
    /// <param name="bloqueada">Verdadeiro para simular a conta bloqueada.</param>
    /// <returns>O id do usuário.</returns>
    public string Semear(string email, string senha, string papel, bool bloqueada = false)
    {
        var conta = new ContaFake(Guid.CreateVersion7().ToString(), email, senha, papel, bloqueada);
        _contas.Add(conta);
        return conta.UsuarioId;
    }

    /// <inheritdoc/>
    public Task<bool> EmailEmUsoAsync(string email, CancellationToken cancellationToken)
    {
        return Task.FromResult(Achar(email) is not null);
    }

    /// <inheritdoc/>
    public Task<string> CriarAsync(string email, string senha, string papel, CancellationToken cancellationToken)
    {
        if (Achar(email) is not null)
        {
            throw new RegraNegocioException(IContasIdentity.MensagemEmailRepetido);
        }

        return Task.FromResult(Semear(email, senha, papel));
    }

    /// <inheritdoc/>
    public Task<VerificacaoSenha> VerificarSenhaAsync(string email, string senha, CancellationToken cancellationToken)
    {
        Verificacoes++;
        ContaFake? conta = Achar(email);

        VerificacaoSenha resultado = conta switch
        {
            null => new VerificacaoSenha(SituacaoLogin.Invalida, null, []),
            { Bloqueada: true } => new VerificacaoSenha(SituacaoLogin.Bloqueada, conta.UsuarioId, []),
            _ when conta.Senha != senha => new VerificacaoSenha(SituacaoLogin.Invalida, conta.UsuarioId, []),
            _ => new VerificacaoSenha(SituacaoLogin.Valida, conta.UsuarioId, [conta.Papel]),
        };

        return Task.FromResult(resultado);
    }

    /// <inheritdoc/>
    public Task<string?> ObterEmailAsync(string usuarioId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_contas.FirstOrDefault(c => c.UsuarioId == usuarioId)?.Email);
    }

    private ContaFake? Achar(string email)
    {
        return _contas.FirstOrDefault(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Conta guardada pelo <see cref="FakeContasIdentity"/>.
/// </summary>
/// <param name="UsuarioId">Id do usuário.</param>
/// <param name="Email">E-mail.</param>
/// <param name="Senha">Senha (em claro: é só um fake de teste).</param>
/// <param name="Papel">Papel.</param>
/// <param name="Bloqueada">Conta bloqueada.</param>
public sealed record ContaFake(string UsuarioId, string Email, string Senha, string Papel, bool Bloqueada);
