using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Unidade de trabalho em memória: não grava nada, só conta quantas vezes o serviço confirmou.
/// </summary>
public sealed class FakeUnidadeDeTrabalho : IUnidadeDeTrabalho
{
    /// <summary>
    /// Quantas vezes <see cref="SalvarAsync"/> foi chamada.
    /// </summary>
    public int VezesSalvo { get; private set; }

    /// <inheritdoc/>
    public Task SalvarAsync(CancellationToken cancellationToken)
    {
        VezesSalvo++;
        return Task.CompletedTask;
    }
}
