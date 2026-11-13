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
}
