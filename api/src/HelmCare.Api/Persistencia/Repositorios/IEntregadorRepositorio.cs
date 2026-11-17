using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Acesso aos entregadores (raiz do cadastro, com a ficha médica e os contatos).
/// </summary>
public interface IEntregadorRepositorio : IRepositorio<Entregador>
{
    /// <summary>
    /// Busca o entregador com a ficha médica e os contatos de emergência carregados.
    /// </summary>
    /// <param name="id">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O entregador completo, ou nulo se não existir.</returns>
    Task<Entregador?> ObterComCadastroAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Diz se já existe entregador com o CPF (só os 11 dígitos, como o domínio guarda).
    /// </summary>
    /// <param name="cpf">CPF normalizado.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns><see langword="true"/> se o CPF já está cadastrado.</returns>
    Task<bool> CpfJaCadastradoAsync(string cpf, CancellationToken cancellationToken);

    /// <summary>
    /// Diz se já existe entregador com a CNH (só os 11 dígitos).
    /// </summary>
    /// <param name="cnh">CNH normalizada.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns><see langword="true"/> se a CNH já está cadastrada.</returns>
    Task<bool> CnhJaCadastradaAsync(string cnh, CancellationToken cancellationToken);

    /// <summary>
    /// Diz se outro entregador já usa o telefone (só os dígitos).
    /// </summary>
    /// <param name="telefone">Telefone normalizado.</param>
    /// <param name="excetoEntregadorId">Entregador que não conta (o próprio, na troca de telefone), ou nulo no cadastro.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns><see langword="true"/> se o telefone já é de outro entregador.</returns>
    Task<bool> TelefoneEmUsoAsync(string telefone, Guid? excetoEntregadorId, CancellationToken cancellationToken);

    /// <summary>
    /// Busca, sem rastrear, o entregador da conta do Identity.
    /// </summary>
    /// <param name="usuarioId">Id do usuário do Identity.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O entregador, ou nulo se a conta não é de entregador.</returns>
    Task<Entregador?> ObterPorUsuarioAsync(string usuarioId, CancellationToken cancellationToken);

    /// <summary>
    /// Conta os contatos de emergência do entregador, sem carregar nenhum dado deles. Usada pelo
    /// aviso simulado ao contato, que só registra a quantidade.
    /// </summary>
    /// <param name="entregadorId">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>A quantidade de contatos (zero se não houver nenhum).</returns>
    Task<int> ContarContatosAsync(Guid entregadorId, CancellationToken cancellationToken);
}
