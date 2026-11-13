using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Modelos;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Registro de idempotência dos eventos MQTT (seção 6.2): mesmo dispositivo, tipo e <c>ts</c>
/// são um evento só. Não herda de <see cref="IRepositorio{T}"/> porque
/// <see cref="EventoMqttProcessado"/> é um modelo interno da persistência, e não uma entidade do domínio.
/// </summary>
public interface IEventoMqttProcessadoRepositorio
{
    /// <summary>
    /// Diz se o evento já foi processado.
    /// </summary>
    /// <param name="codigoDispositivo">Código do capacete.</param>
    /// <param name="tipo">Tipo do evento.</param>
    /// <param name="ts">Instante do evento, em UTC.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns><see langword="true"/> se o trio já está registrado.</returns>
    Task<bool> JaProcessadoAsync(string codigoDispositivo, TipoEventoMqtt tipo, DateTime ts, CancellationToken cancellationToken);

    /// <summary>
    /// Marca o registro para ser inserido no mesmo <see cref="IUnidadeDeTrabalho.SalvarAsync"/> do efeito do evento.
    /// </summary>
    /// <param name="registro">Registro do evento processado.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>A tarefa da operação.</returns>
    Task AdicionarAsync(EventoMqttProcessado registro, CancellationToken cancellationToken);
}
