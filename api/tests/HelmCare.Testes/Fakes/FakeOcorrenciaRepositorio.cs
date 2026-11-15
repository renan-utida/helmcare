using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Repositório de ocorrências em memória. "Ativa" usa o próprio <see cref="Ocorrencia.EstaAtiva"/>.
/// A lista é protegida por trava porque o teste do serviço em segundo plano lê e escreve de duas threads.
/// </summary>
public sealed class FakeOcorrenciaRepositorio : IOcorrenciaRepositorio
{
    private readonly List<Ocorrencia> _ocorrencias = [];
    private readonly Lock _trava = new();
    private readonly Channel<DateTime> _consultasDeVencidas = Channel.CreateUnbounded<DateTime>();

    /// <summary>
    /// Ocorrências adicionadas ou semeadas.
    /// </summary>
    public IReadOnlyList<Ocorrencia> Ocorrencias => Copia();

    /// <summary>
    /// Recebe o limite de <c>CriadaEm</c> de cada chamada a <see cref="ListarCamada1VencidasAsync"/>,
    /// na ordem. O teste do serviço em segundo plano espera por aqui o início de cada rodada.
    /// </summary>
    public ChannelReader<DateTime> ConsultasDeVencidas => _consultasDeVencidas.Reader;

    /// <summary>
    /// Coloca uma ocorrência na lista, como se já estivesse no banco.
    /// </summary>
    /// <param name="ocorrencia">Ocorrência existente.</param>
    public void Semear(Ocorrencia ocorrencia)
    {
        lock (_trava)
        {
            _ocorrencias.Add(ocorrencia);
        }
    }

    /// <inheritdoc/>
    public Task<Ocorrencia?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Ocorrencias.FirstOrDefault(o => o.Id == id));
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
        Semear(entidade);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> ExisteAtivaDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Copia().Exists(o => o.EntregadorId == entregadorId && o.EstaAtiva));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Ocorrencia>> ListarAguardandoAceiteDeOutrosAsync(Guid entregadorId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Ocorrencia> lista = Ocorrencias
            .Where(o => o.EstaAguardandoAceite && o.EntregadorId != entregadorId)
            .ToList();
        return Task.FromResult(lista);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Ocorrencia>> ListarCamada1VencidasAsync(DateTime criadaAte, CancellationToken cancellationToken)
    {
        IReadOnlyList<Ocorrencia> lista = Ocorrencias
            .Where(o => o.Estado == EstadoOcorrencia.SosCamada1 && o.CriadaEm <= criadaAte)
            .OrderBy(o => o.CriadaEm)
            .ToList();
        _consultasDeVencidas.Writer.TryWrite(criadaAte);
        return Task.FromResult(lista);
    }

    // Cópia feita sob a trava: quem consulta nunca percorre a lista enquanto outra thread acrescenta.
    private List<Ocorrencia> Copia()
    {
        lock (_trava)
        {
            return [.. _ocorrencias];
        }
    }
}
