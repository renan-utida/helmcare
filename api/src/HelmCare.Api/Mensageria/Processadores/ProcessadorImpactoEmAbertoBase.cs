using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.Extensions.Logging;

namespace HelmCare.Api.Mensageria.Processadores;

/// <summary>
/// Roteiro comum dos eventos que continuam um impacto já registrado (<c>CONTAGEM_INICIADA</c>,
/// <c>FALSO_POSITIVO</c> e <c>CANCELADO</c>): acha o impacto em aberto mais recente do capacete,
/// aplica o efeito do tipo e registra o estado do capacete. Cada subclasse só diz o efeito e o
/// estado (método-modelo).
/// </summary>
/// <remarks>
/// Sem impacto em aberto (o <c>IMPACTO_DETECTADO</c> se perdeu em QoS 0, ou o impacto já foi
/// encerrado), o evento não tem a que se aplicar: fica um aviso no log, e o estado do capacete
/// ainda é registrado, porque ele continua sendo o último estado informado.
/// </remarks>
public abstract partial class ProcessadorImpactoEmAbertoBase : IProcessadorEvento
{
    private readonly IEventoImpactoRepositorio _impactos;
    private readonly ILogger _logger;

    /// <summary>
    /// Cria o processador.
    /// </summary>
    /// <param name="impactos">Repositório de impactos.</param>
    /// <param name="logger">Log da aplicação.</param>
    protected ProcessadorImpactoEmAbertoBase(IEventoImpactoRepositorio impactos, ILogger logger)
    {
        _impactos = impactos;
        _logger = logger;
    }

    /// <inheritdoc/>
    public abstract TipoEventoMqtt Tipo { get; }

    /// <summary>
    /// Estado em que o capacete fica depois deste evento.
    /// </summary>
    protected abstract EstadoDispositivo EstadoDoCapacete { get; }

    /// <inheritdoc/>
    public async Task ProcessarAsync(ContextoEvento contexto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        EventoImpacto? impacto = await _impactos.ObterEmAbertoMaisRecenteAsync(contexto.Dispositivo.Id, cancellationToken);

        if (impacto is null)
        {
            RegistrarSemImpactoEmAberto(_logger, contexto.Dispositivo.Codigo, Tipo, contexto.Evento.Ts);
        }
        else
        {
            AplicarNoImpacto(impacto, contexto);
        }

        contexto.Dispositivo.RegistrarEstado(EstadoDoCapacete, contexto.Evento.Ts);
    }

    /// <summary>
    /// Efeito do evento no impacto em aberto.
    /// </summary>
    /// <param name="impacto">Impacto em aberto mais recente do capacete.</param>
    /// <param name="contexto">Capacete e evento.</param>
    protected abstract void AplicarNoImpacto(EventoImpacto impacto, ContextoEvento contexto);

    [LoggerMessage(
        EventId = 210,
        Level = LogLevel.Warning,
        Message = "Evento {Tipo} do capacete {Codigo} (ts {Ts:o}) sem impacto em aberto; só o estado do capacete foi registrado.")]
    private static partial void RegistrarSemImpactoEmAberto(ILogger logger, string codigo, TipoEventoMqtt tipo, DateTime ts);
}
