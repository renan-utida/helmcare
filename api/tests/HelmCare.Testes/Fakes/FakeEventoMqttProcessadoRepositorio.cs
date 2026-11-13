using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Modelos;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Enums;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Registro de idempotência em memória.
/// </summary>
public sealed class FakeEventoMqttProcessadoRepositorio : IEventoMqttProcessadoRepositorio
{
    private readonly List<EventoMqttProcessado> _registros = [];

    /// <summary>
    /// Registros adicionados.
    /// </summary>
    public IReadOnlyList<EventoMqttProcessado> Registros => _registros;

    /// <inheritdoc/>
    public Task<bool> JaProcessadoAsync(
        string codigoDispositivo,
        TipoEventoMqtt tipo,
        DateTime ts,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_registros.Exists(
            r => r.CodigoDispositivo == codigoDispositivo && r.Tipo == tipo && r.Ts == ts));
    }

    /// <inheritdoc/>
    public Task AdicionarAsync(EventoMqttProcessado registro, CancellationToken cancellationToken)
    {
        _registros.Add(registro);
        return Task.CompletedTask;
    }
}
