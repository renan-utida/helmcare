using System.Threading;
using System.Threading.Tasks;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Confirma de uma vez, numa única transação, tudo o que os repositórios da requisição
/// adicionaram ou alteraram. Implementada pelo próprio <see cref="AppDbContext"/>; os testes
/// usam um fake que só conta as chamadas.
/// </summary>
public interface IUnidadeDeTrabalho
{
    /// <summary>
    /// Grava as alterações pendentes no banco.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A tarefa da gravação.</returns>
    Task SalvarAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Esquece tudo o que está rastreado (alterações pendentes e entidades lidas), para a próxima
    /// leitura vir do banco. Usada na nova tentativa do aceite depois de um conflito de concorrência.
    /// </summary>
    void DescartarAlteracoes();
}
