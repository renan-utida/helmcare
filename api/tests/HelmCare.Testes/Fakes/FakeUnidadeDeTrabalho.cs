using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Unidade de trabalho em memória: não grava nada, só conta quantas vezes o serviço confirmou.
/// Pode simular uma falha do banco com <see cref="FalhaAoSalvar"/>.
/// </summary>
public sealed class FakeUnidadeDeTrabalho : IUnidadeDeTrabalho
{
    /// <summary>
    /// Quantas vezes <see cref="SalvarAsync"/> terminou com sucesso.
    /// </summary>
    public int VezesSalvo { get; private set; }

    /// <summary>
    /// Quando preenchida, <see cref="SalvarAsync"/> lança a exceção criada por ela (ex.: violação de índice único).
    /// </summary>
    public Func<Exception>? FalhaAoSalvar { get; set; }

    /// <inheritdoc/>
    public Task SalvarAsync(CancellationToken cancellationToken)
    {
        if (FalhaAoSalvar is not null)
        {
            throw FalhaAoSalvar();
        }

        VezesSalvo++;
        return Task.CompletedTask;
    }
}
