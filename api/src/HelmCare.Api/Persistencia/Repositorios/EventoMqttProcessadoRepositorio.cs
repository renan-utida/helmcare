using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Modelos;
using HelmCare.Dominio.Enums;
using Microsoft.EntityFrameworkCore;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Implementação do <see cref="IEventoMqttProcessadoRepositorio"/> com o EF Core. A consulta usa o
/// índice único (CodigoDispositivo, Tipo, Ts), que também barra a duplicata se duas mensagens
/// iguais chegarem ao mesmo tempo.
/// </summary>
public sealed class EventoMqttProcessadoRepositorio : IEventoMqttProcessadoRepositorio
{
    private readonly AppDbContext _contexto;

    /// <summary>
    /// Cria o repositório.
    /// </summary>
    /// <param name="contexto">Contexto do EF Core da operação.</param>
    public EventoMqttProcessadoRepositorio(AppDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc/>
    public Task<bool> JaProcessadoAsync(
        string codigoDispositivo,
        TipoEventoMqtt tipo,
        DateTime ts,
        CancellationToken cancellationToken)
    {
        return _contexto.EventosMqttProcessados.AnyAsync(
            e => e.CodigoDispositivo == codigoDispositivo && e.Tipo == tipo && e.Ts == ts,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AdicionarAsync(EventoMqttProcessado registro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registro);
        await _contexto.EventosMqttProcessados.AddAsync(registro, cancellationToken);
    }
}
