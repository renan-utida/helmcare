using System;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Mensageria.Processadores;

/// <summary>
/// <c>CONTAGEM_INICIADA</c>: guarda <c>contagemTerminaEm = ts + CONTAGEM_CANCELAMENTO_S</c> no
/// impacto em aberto (o app mostra a contagem regressiva com ele). Capacete em <c>CONTAGEM</c>.
/// </summary>
public sealed class ProcessadorContagemIniciada : ProcessadorImpactoEmAbertoBase
{
    private readonly TimeSpan _duracaoContagem;

    /// <summary>
    /// Cria o processador.
    /// </summary>
    /// <param name="impactos">Repositório de impactos.</param>
    /// <param name="opcoes">Constantes da seção 5 (<c>CONTAGEM_CANCELAMENTO_S</c>).</param>
    /// <param name="logger">Log da aplicação.</param>
    public ProcessadorContagemIniciada(
        IEventoImpactoRepositorio impactos,
        IOptions<OpcoesHelmCare> opcoes,
        ILogger<ProcessadorContagemIniciada> logger)
        : base(impactos, logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        _duracaoContagem = TimeSpan.FromSeconds(opcoes.Value.ContagemCancelamentoS);
    }

    /// <inheritdoc/>
    public override TipoEventoMqtt Tipo => TipoEventoMqtt.ContagemIniciada;

    /// <inheritdoc/>
    protected override EstadoDispositivo EstadoDoCapacete => EstadoDispositivo.Contagem;

    /// <inheritdoc/>
    protected override void AplicarNoImpacto(EventoImpacto impacto, ContextoEvento contexto)
    {
        ArgumentNullException.ThrowIfNull(impacto);
        ArgumentNullException.ThrowIfNull(contexto);
        impacto.IniciarContagem(contexto.Evento.Ts, _duracaoContagem);
    }
}
