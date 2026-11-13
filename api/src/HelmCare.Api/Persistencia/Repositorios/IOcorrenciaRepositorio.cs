using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Acesso às ocorrências de SOS.
/// </summary>
public interface IOcorrenciaRepositorio : IRepositorio<Ocorrencia>
{
    /// <summary>
    /// Diz se o entregador tem uma ocorrência ativa (os estados de <see cref="Ocorrencia.EstaAtiva"/>).
    /// </summary>
    /// <param name="entregadorId">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns><see langword="true"/> se há ocorrência em <c>SOS_CAMADA_1</c>, <c>SOS_CAMADA_2</c> ou <c>SOCORRO_A_CAMINHO</c>.</returns>
    Task<bool> ExisteAtivaDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken);

    /// <summary>
    /// Busca a ocorrência com o impacto e os aceites carregados (usada nas rotas de ocorrência).
    /// </summary>
    /// <param name="id">Id da ocorrência.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>A ocorrência completa, ou nulo se não existir.</returns>
    Task<Ocorrencia?> ObterAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Busca a ocorrência com o impacto e os aceites, só para leitura (sem rastreio de alterações).
    /// Usada no <c>GET /ocorrencias/{id}</c>, que precisa do impacto para o cronômetro e do aceite
    /// para decidir entre a localização exata e a aproximada.
    /// </summary>
    /// <param name="id">Id da ocorrência.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>A ocorrência completa, ou nulo se não existir.</returns>
    Task<Ocorrencia?> ObterParaConsultaAsync(Guid id, CancellationToken cancellationToken);
}
