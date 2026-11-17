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

    /// <summary>
    /// Abre uma transação do banco. Usada no cadastro, em que a conta do Identity e o entregador são
    /// gravados em dois passos e precisam valer juntos: se o segundo falhar, o primeiro é desfeito.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A transação aberta; descartada sem confirmar, desfaz tudo.</returns>
    Task<ITransacao> IniciarTransacaoAsync(CancellationToken cancellationToken);
}
