using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Acesso aos capacetes.
/// </summary>
public interface IDispositivoRepositorio : IRepositorio<Dispositivo>
{
    /// <summary>
    /// Busca o capacete pelo código (ex.: <c>HC-0001</c>), que é como o MQTT o identifica.
    /// </summary>
    /// <param name="codigo">Código do capacete.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O capacete, ou nulo se não existir.</returns>
    Task<Dispositivo?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken);

    /// <summary>
    /// Busca, sem rastrear, o capacete pelo <c>qrToken</c> do QR Code (Cofre médico). O <c>qrToken</c> é
    /// credencial: quem chama nunca o registra no log.
    /// </summary>
    /// <param name="qrToken">Token do QR Code.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O capacete, ou nulo se o token não existir.</returns>
    Task<Dispositivo?> ObterPorQrTokenAsync(string qrToken, CancellationToken cancellationToken);

    /// <summary>
    /// Diz se o código já está vinculado a algum entregador.
    /// </summary>
    /// <param name="codigo">Código do capacete.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns><see langword="true"/> se o código já existe.</returns>
    Task<bool> CodigoJaCadastradoAsync(string codigo, CancellationToken cancellationToken);

    /// <summary>
    /// Diz se o entregador já tem um capacete (a regra é um por entregador).
    /// </summary>
    /// <param name="entregadorId">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns><see langword="true"/> se o entregador já tem capacete.</returns>
    Task<bool> EntregadorJaTemDispositivoAsync(Guid entregadorId, CancellationToken cancellationToken);

    /// <summary>
    /// Busca o capacete do entregador só para leitura (sem rastreio de alterações).
    /// </summary>
    /// <param name="entregadorId">Id do entregador.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O capacete, ou nulo se o entregador não tem capacete.</returns>
    Task<Dispositivo?> ObterDoEntregadorAsync(Guid entregadorId, CancellationToken cancellationToken);
}
