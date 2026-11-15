using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Unidade de trabalho em memória: não grava nada, só conta quantas vezes o serviço confirmou.
/// Pode simular uma falha do banco em toda gravação (<see cref="FalhaAoSalvar"/>) ou só em uma
/// delas (<see cref="FalhaNaChamada"/>).
/// </summary>
public sealed class FakeUnidadeDeTrabalho : IUnidadeDeTrabalho
{
    private int _chamadas;

    /// <summary>
    /// Quantas vezes <see cref="SalvarAsync"/> terminou com sucesso.
    /// </summary>
    public int VezesSalvo { get; private set; }

    /// <summary>
    /// Quando preenchida, <see cref="SalvarAsync"/> lança a exceção criada por ela (ex.: violação de índice único).
    /// </summary>
    public Func<Exception>? FalhaAoSalvar { get; set; }

    /// <summary>
    /// Recebe o número da chamada (a primeira é 1) e devolve a exceção a lançar nela, ou nulo para gravar.
    /// </summary>
    public Func<int, Exception?>? FalhaNaChamada { get; set; }

    /// <summary>
    /// Quantas vezes <see cref="DescartarAlteracoes"/> foi chamado.
    /// </summary>
    public int VezesDescartado { get; private set; }

    /// <inheritdoc/>
    public Task SalvarAsync(CancellationToken cancellationToken)
    {
        _chamadas++;

        if (FalhaAoSalvar is not null)
        {
            throw FalhaAoSalvar();
        }

        if (FalhaNaChamada?.Invoke(_chamadas) is { } falha)
        {
            throw falha;
        }

        VezesSalvo++;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void DescartarAlteracoes()
    {
        VezesDescartado++;
    }
}
