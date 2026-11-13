using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Repositório de entregadores em memória. Como os objetos ficam vivos na lista, a ficha e os
/// contatos já estão "carregados", e o que o serviço altera é o que o teste confere.
/// </summary>
public sealed class FakeEntregadorRepositorio : IEntregadorRepositorio
{
    private readonly List<Entregador> _entregadores = [];

    /// <summary>
    /// Entregadores adicionados.
    /// </summary>
    public IReadOnlyList<Entregador> Entregadores => _entregadores;

    /// <summary>
    /// Coloca um entregador na lista, como se já estivesse no banco.
    /// </summary>
    /// <param name="entregador">Entregador existente.</param>
    public void Semear(Entregador entregador)
    {
        _entregadores.Add(entregador);
    }

    /// <inheritdoc/>
    public Task<Entregador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_entregadores.FirstOrDefault(e => e.Id == id));
    }

    /// <inheritdoc/>
    public Task<Entregador?> ObterComCadastroAsync(Guid id, CancellationToken cancellationToken)
    {
        return ObterPorIdAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task AdicionarAsync(Entregador entidade, CancellationToken cancellationToken)
    {
        _entregadores.Add(entidade);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> CpfJaCadastradoAsync(string cpf, CancellationToken cancellationToken)
    {
        return Task.FromResult(_entregadores.Exists(e => e.Cpf == cpf));
    }
}
