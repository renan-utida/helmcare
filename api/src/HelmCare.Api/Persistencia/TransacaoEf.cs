using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore.Storage;

namespace HelmCare.Api.Persistencia;

/// <summary>
/// <see cref="ITransacao"/> sobre a transação do EF Core. Descartar sem confirmar faz o rollback.
/// </summary>
public sealed class TransacaoEf : ITransacao
{
    private readonly IDbContextTransaction _transacao;

    /// <summary>
    /// Embrulha a transação aberta pelo contexto.
    /// </summary>
    /// <param name="transacao">Transação do EF Core.</param>
    public TransacaoEf(IDbContextTransaction transacao)
    {
        _transacao = transacao;
    }

    /// <inheritdoc/>
    public Task ConfirmarAsync(CancellationToken cancellationToken)
    {
        return _transacao.CommitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        return _transacao.DisposeAsync();
    }
}
