using System;
using HelmCare.Api.Mensageria.Mensagens;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Mensageria.Processadores;

/// <summary>
/// O que um processador recebe: o capacete (já carregado e rastreado pelo EF Core) e o evento lido.
/// </summary>
public sealed class ContextoEvento
{
    /// <summary>
    /// Cria o contexto.
    /// </summary>
    /// <param name="dispositivo">Capacete que publicou o evento.</param>
    /// <param name="evento">Evento lido do payload.</param>
    public ContextoEvento(Dispositivo dispositivo, EventoMensagem evento)
    {
        ArgumentNullException.ThrowIfNull(dispositivo);
        ArgumentNullException.ThrowIfNull(evento);

        Dispositivo = dispositivo;
        Evento = evento;
    }

    /// <summary>
    /// Capacete que publicou o evento.
    /// </summary>
    public Dispositivo Dispositivo { get; }

    /// <summary>
    /// Evento lido do payload.
    /// </summary>
    public EventoMensagem Evento { get; }
}
