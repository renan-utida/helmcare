using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Comum;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Operações comuns a todo repositório de entidade. Os serviços dependem desta interface (e das
/// específicas que herdam dela), nunca do <see cref="AppDbContext"/>: assim os testes trocam o
/// banco por fakes em memória escritos à mão. Os repositórios não salvam: quem confirma a
/// gravação é a <see cref="IUnidadeDeTrabalho"/>, uma vez por operação do serviço.
/// </summary>
/// <typeparam name="T">Entidade do domínio.</typeparam>
public interface IRepositorio<T>
    where T : Entidade
{
    /// <summary>
    /// Busca a entidade pelo id, sem carregar as relações.
    /// </summary>
    /// <param name="id">Id da entidade.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A entidade, ou nulo se não existir.</returns>
    Task<T?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Marca uma entidade nova para ser inserida na próxima <see cref="IUnidadeDeTrabalho.SalvarAsync"/>.
    /// </summary>
    /// <param name="entidade">Entidade nova.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A tarefa da operação.</returns>
    Task AdicionarAsync(T entidade, CancellationToken cancellationToken);
}
