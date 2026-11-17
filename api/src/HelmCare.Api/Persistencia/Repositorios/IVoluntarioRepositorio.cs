using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Acesso aos voluntários da rede de socorro. O <see cref="IRepositorio{T}.ObterPorIdAsync"/>
/// devolve o voluntário rastreado, para o <c>PUT /voluntarios/{id}/disponibilidade</c> alterar e gravar.
/// </summary>
public interface IVoluntarioRepositorio : IRepositorio<Voluntario>
{
    /// <summary>
    /// Diz se o voluntário existe.
    /// </summary>
    /// <param name="id">Id do voluntário.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns><see langword="true"/> se existe.</returns>
    Task<bool> ExisteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Diz se o entregador já é voluntário (um entregador tem no máximo um <see cref="Voluntario"/>).
    /// </summary>
    /// <param name="entregadorId">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns><see langword="true"/> se já existe voluntário para o entregador.</returns>
    Task<bool> ExisteDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken);

    /// <summary>
    /// Id do voluntário do entregador (cada entregador tem no máximo um), para a sessão do app.
    /// </summary>
    /// <param name="entregadorId">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O id do voluntário, ou nulo se o entregador não é voluntário.</returns>
    Task<Guid?> ObterIdDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken);

    /// <summary>
    /// Busca o voluntário só para leitura (sem rastreio de alterações). Usada nos chamados próximos,
    /// que só leem a disponibilidade e a posição.
    /// </summary>
    /// <param name="id">Id do voluntário.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O voluntário, ou nulo se não existir.</returns>
    Task<Voluntario?> ObterParaConsultaAsync(Guid id, CancellationToken cancellationToken);
}
