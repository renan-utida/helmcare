using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Mensageria.Processadores;

/// <summary>
/// <c>IMPACTO_DETECTADO</c>: registra o <see cref="EventoImpacto"/> com <c>ocorridoEm = ts</c>.
/// Sem pilotagem e sem presença antes, o impacto nasce <c>IGNORADO</c> e o capacete volta para
/// <c>ARMADO</c>; senão, nasce em aberto e o capacete fica em <c>IMPACTO_DETECTADO</c>.
/// </summary>
public sealed class ProcessadorImpactoDetectado : IProcessadorEvento
{
    private readonly IEventoImpactoRepositorio _impactos;

    /// <summary>
    /// Cria o processador.
    /// </summary>
    /// <param name="impactos">Repositório de impactos.</param>
    public ProcessadorImpactoDetectado(IEventoImpactoRepositorio impactos)
    {
        _impactos = impactos;
    }

    /// <inheritdoc/>
    public TipoEventoMqtt Tipo => TipoEventoMqtt.ImpactoDetectado;

    /// <inheritdoc/>
    public async Task ProcessarAsync(ContextoEvento contexto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var impacto = EventoImpacto.Registrar(
            contexto.Dispositivo.Id,
            contexto.Evento.Ts,
            contexto.Evento.PicoG,
            contexto.Evento.OrientacaoAnormal,
            contexto.Evento.PilotandoAntes,
            contexto.Evento.PresencaAntes);

        await _impactos.AdicionarAsync(impacto, cancellationToken);

        EstadoDispositivo estado = impacto.Resultado == ResultadoImpacto.Ignorado
            ? EstadoDispositivo.Armado
            : EstadoDispositivo.ImpactoDetectado;

        contexto.Dispositivo.RegistrarEstado(estado, contexto.Evento.Ts);
    }
}
