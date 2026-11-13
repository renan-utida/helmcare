using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Repositório de ocorrências em memória. "Ativa" usa o próprio <see cref="Ocorrencia.EstaAtiva"/>.
/// </summary>
public sealed class FakeOcorrenciaRepositorio : IOcorrenciaRepositorio
{
    private readonly List<Ocorrencia> _ocorrencias = [];

    /// <summary>
    /// Ocorrências adicionadas ou semeadas.
    /// </summary>
    public IReadOnlyList<Ocorrencia> Ocorrencias => _ocorrencias;

    /// <summary>
    /// Coloca uma ocorrência na lista, como se já estivesse no banco.
    /// </summary>
    /// <param name="ocorrencia">Ocorrência existente.</param>
    public void Semear(Ocorrencia ocorrencia)
    {
        _ocorrencias.Add(ocorrencia);
    }

    /// <inheritdoc/>
    public Task<Ocorrencia?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_ocorrencias.FirstOrDefault(o => o.Id == id));
    }

    /// <inheritdoc/>
    public Task<Ocorrencia?> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        return ObterPorIdAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Ocorrencia?> ObterParaConsultaAsync(Guid id, CancellationToken cancellationToken)
    {
        return ObterPorIdAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task AdicionarAsync(Ocorrencia entidade, CancellationToken cancellationToken)
    {
        _ocorrencias.Add(entidade);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> ExisteAtivaDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_ocorrencias.Exists(o => o.EntregadorId == entregadorId && o.EstaAtiva));
    }
}
