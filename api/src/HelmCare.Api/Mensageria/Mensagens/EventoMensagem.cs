using System;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Mensageria.Mensagens;

/// <summary>
/// Evento do capacete (seção 6.2 do contrato), publicado a cada mudança de estado. Todos os
/// eventos de um mesmo impacto repetem <c>picoG</c>, <c>orientacaoAnormal</c>,
/// <c>pilotandoAntes</c>, <c>presencaAntes</c>, <c>latitude</c> e <c>longitude</c>; só mudam o
/// <c>tipo</c> e o <c>ts</c>. As coordenadas são dado de localização: nunca vão para o log.
/// </summary>
public sealed class EventoMensagem
{
    /// <summary>
    /// <c>dispositivo</c>: código do capacete.
    /// </summary>
    public required string Dispositivo { get; init; }

    /// <summary>
    /// <c>ts</c>: instante do evento no relógio do capacete, em UTC. Junto com o dispositivo e o
    /// tipo, identifica o evento (idempotência).
    /// </summary>
    public required DateTime Ts { get; init; }

    /// <summary>
    /// <c>tipo</c>: tipo do evento.
    /// </summary>
    public required TipoEventoMqtt Tipo { get; init; }

    /// <summary>
    /// <c>picoG</c>: maior aceleração total medida no impacto, em g.
    /// </summary>
    public required double PicoG { get; init; }

    /// <summary>
    /// <c>orientacaoAnormal</c>: inclinação maior que 60° depois do impacto (só informativo).
    /// </summary>
    public required bool OrientacaoAnormal { get; init; }

    /// <summary>
    /// <c>pilotandoAntes</c>: velocidade maior ou igual a 15 km/h antes do impacto.
    /// </summary>
    public required bool PilotandoAntes { get; init; }

    /// <summary>
    /// <c>presencaAntes</c>: capacete na cabeça antes do impacto.
    /// </summary>
    public required bool PresencaAntes { get; init; }

    /// <summary>
    /// <c>latitude</c>: em graus decimais.
    /// </summary>
    public required double Latitude { get; init; }

    /// <summary>
    /// <c>longitude</c>: em graus decimais.
    /// </summary>
    public required double Longitude { get; init; }
}
