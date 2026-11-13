using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Acesso aos turnos. O <see cref="IRepositorio{T}.ObterPorIdAsync"/> herdado devolve o turno
/// rastreado, para alterar (encerrar); o <see cref="ObterAsync"/> é só leitura.
/// </summary>
public interface ITurnoRepositorio : IRepositorio<Turno>
{
    /// <summary>
    /// Busca o turno só para leitura (sem rastreio de alterações).
    /// </summary>
    /// <param name="id">Id do turno.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O turno, ou nulo se não existir.</returns>
    Task<Turno?> ObterAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Diz se o entregador tem um turno ativo (sem <c>Fim</c>).
    /// </summary>
    /// <param name="entregadorId">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns><see langword="true"/> se há turno ativo.</returns>
    Task<bool> ExisteAtivoDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken);

    /// <summary>
    /// Lê, numa consulta só, o estado do capacete do entregador, o último índice de calor, o fim da
    /// contagem do impacto em aberto mais recente e o id da ocorrência ativa.
    /// </summary>
    /// <param name="entregadorId">Id do entregador do turno.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>Os dados crus, ou nulo se o entregador não tem capacete.</returns>
    Task<DadosAlertaTurno?> ObterDadosAlertaAsync(Guid entregadorId, CancellationToken cancellationToken);
}
