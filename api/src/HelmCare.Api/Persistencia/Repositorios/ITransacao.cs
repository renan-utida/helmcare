using System;
using System.Threading;
using System.Threading.Tasks;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Transação do banco aberta pela <see cref="IUnidadeDeTrabalho"/>. Tudo o que for gravado depois de
/// aberta (inclusive pelo Identity, que usa o mesmo contexto) só vale com o
/// <see cref="ConfirmarAsync"/>. Descartada sem confirmar, desfaz tudo.
/// </summary>
public interface ITransacao : IAsyncDisposable
{
    /// <summary>
    /// Confirma a transação.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A tarefa da confirmação.</returns>
    Task ConfirmarAsync(CancellationToken cancellationToken);
}
