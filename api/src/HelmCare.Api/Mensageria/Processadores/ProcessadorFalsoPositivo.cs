using System;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.Extensions.Logging;

namespace HelmCare.Api.Mensageria.Processadores;

/// <summary>
/// <c>FALSO_POSITIVO</c>: a velocidade de moto voltou durante a observação. O impacto em aberto
/// fica com <c>resultado = FALSO_POSITIVO</c> e o capacete em <c>FALSO_POSITIVO</c>.
/// </summary>
public sealed class ProcessadorFalsoPositivo : ProcessadorImpactoEmAbertoBase
{
    /// <summary>
    /// Cria o processador.
    /// </summary>
    /// <param name="impactos">Repositório de impactos.</param>
    /// <param name="logger">Log da aplicação.</param>
    public ProcessadorFalsoPositivo(IEventoImpactoRepositorio impactos, ILogger<ProcessadorFalsoPositivo> logger)
        : base(impactos, logger)
    {
    }

    /// <inheritdoc/>
    public override TipoEventoMqtt Tipo => TipoEventoMqtt.FalsoPositivo;

    /// <inheritdoc/>
    protected override EstadoDispositivo EstadoDoCapacete => EstadoDispositivo.FalsoPositivo;

    /// <inheritdoc/>
    protected override void AplicarNoImpacto(EventoImpacto impacto, ContextoEvento contexto)
    {
        ArgumentNullException.ThrowIfNull(impacto);
        impacto.MarcarFalsoPositivo();
    }
}
