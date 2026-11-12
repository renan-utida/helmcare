using System;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.Extensions.Logging;

namespace HelmCare.Api.Mensageria.Processadores;

/// <summary>
/// <c>CANCELADO</c>: o entregador cancelou durante a contagem (botão do capacete ou comando
/// <c>CANCELAR</c>). O impacto em aberto fica com <c>resultado = CANCELADO_PELO_ENTREGADOR</c> e o
/// capacete em <c>CANCELADO</c>.
/// </summary>
public sealed class ProcessadorCancelado : ProcessadorImpactoEmAbertoBase
{
    /// <summary>
    /// Cria o processador.
    /// </summary>
    /// <param name="impactos">Repositório de impactos.</param>
    /// <param name="logger">Log da aplicação.</param>
    public ProcessadorCancelado(IEventoImpactoRepositorio impactos, ILogger<ProcessadorCancelado> logger)
        : base(impactos, logger)
    {
    }

    /// <inheritdoc/>
    public override TipoEventoMqtt Tipo => TipoEventoMqtt.Cancelado;

    /// <inheritdoc/>
    protected override EstadoDispositivo EstadoDoCapacete => EstadoDispositivo.Cancelado;

    /// <inheritdoc/>
    protected override void AplicarNoImpacto(EventoImpacto impacto, ContextoEvento contexto)
    {
        ArgumentNullException.ThrowIfNull(impacto);
        impacto.MarcarCancelado();
    }
}
