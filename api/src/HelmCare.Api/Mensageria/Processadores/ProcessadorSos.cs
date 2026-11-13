using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Dominio.ObjetosDeValor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Mensageria.Processadores;

/// <summary>
/// <c>SOS</c>: fim da contagem sem cancelamento. Abre a <see cref="Ocorrencia"/> em
/// <c>SOS_CAMADA_1</c> a partir do impacto em aberto (que vai para <c>SOS_ENVIADO</c>) e deixa o
/// capacete em <c>SOS_ENVIADO</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Sem impacto em aberto (o <c>IMPACTO_DETECTADO</c> se perdeu em QoS 0), cria o impacto com
/// os dados do próprio SOS e <c>ocorridoEm = ts - (JANELA_OBSERVACAO_S + CONTAGEM_CANCELAMENTO_S)</c>,
/// marcado como estimado, para nem o SOS nem o indicador se perderem.</item>
/// <item>Se o entregador já tem ocorrência ativa, não abre outra (um socorro por vez). Isso é
/// conferido antes de mexer no impacto, para não deixar um impacto estimado sem ocorrência.</item>
/// <item>O <c>criadaEm</c> vem do relógio do servidor (TimeProvider), que é o relógio das regras de
/// escalonamento; o <c>ts</c> do capacete fica no impacto.</item>
/// </list>
/// </remarks>
public sealed partial class ProcessadorSos : IProcessadorEvento
{
    private readonly IEventoImpactoRepositorio _impactos;
    private readonly IOcorrenciaRepositorio _ocorrencias;
    private readonly TimeProvider _relogio;
    private readonly TimeSpan _antecedenciaEstimada;
    private readonly ILogger<ProcessadorSos> _logger;

    /// <summary>
    /// Cria o processador.
    /// </summary>
    /// <param name="impactos">Repositório de impactos.</param>
    /// <param name="ocorrencias">Repositório de ocorrências.</param>
    /// <param name="relogio">Relógio do servidor, para o <c>criadaEm</c>.</param>
    /// <param name="opcoes">Constantes da seção 5 (janela de observação e contagem).</param>
    /// <param name="logger">Log da aplicação.</param>
    public ProcessadorSos(
        IEventoImpactoRepositorio impactos,
        IOcorrenciaRepositorio ocorrencias,
        TimeProvider relogio,
        IOptions<OpcoesHelmCare> opcoes,
        ILogger<ProcessadorSos> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _impactos = impactos;
        _ocorrencias = ocorrencias;
        _relogio = relogio;
        _logger = logger;
        _antecedenciaEstimada = TimeSpan.FromSeconds(
            opcoes.Value.JanelaObservacaoS + opcoes.Value.ContagemCancelamentoS);
    }

    /// <inheritdoc/>
    public TipoEventoMqtt Tipo => TipoEventoMqtt.Sos;

    /// <inheritdoc/>
    public async Task ProcessarAsync(ContextoEvento contexto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        Dispositivo dispositivo = contexto.Dispositivo;
        DateTime ts = contexto.Evento.Ts;

        // Valida as coordenadas antes de qualquer alteração.
        var local = new Coordenada(contexto.Evento.Latitude, contexto.Evento.Longitude);

        dispositivo.RegistrarEstado(EstadoDispositivo.SosEnviado, ts);

        if (await _ocorrencias.ExisteAtivaDoEntregadorAsync(dispositivo.EntregadorId, cancellationToken))
        {
            RegistrarOcorrenciaJaAtiva(_logger, dispositivo.Codigo, ts);
            return;
        }

        EventoImpacto? impacto = await _impactos.ObterEmAbertoMaisRecenteAsync(dispositivo.Id, cancellationToken);

        if (impacto is null)
        {
            impacto = EventoImpacto.RegistrarEstimadoPorSos(
                dispositivo.Id,
                ts,
                _antecedenciaEstimada,
                contexto.Evento.PicoG,
                contexto.Evento.OrientacaoAnormal,
                contexto.Evento.PilotandoAntes,
                contexto.Evento.PresencaAntes);

            await _impactos.AdicionarAsync(impacto, cancellationToken);
        }

        var ocorrencia = Ocorrencia.Abrir(dispositivo.EntregadorId, impacto, local, _relogio.GetUtcNow().UtcDateTime);
        await _ocorrencias.AdicionarAsync(ocorrencia, cancellationToken);

        RegistrarOcorrenciaAberta(_logger, dispositivo.Codigo, ocorrencia.Id, impacto.OcorridoEmEstimado);
    }

    [LoggerMessage(
        EventId = 220,
        Level = LogLevel.Information,
        Message = "SOS do capacete {Codigo}: ocorrência {OcorrenciaId} aberta em SOS_CAMADA_1 (impacto estimado: {Estimado}).")]
    private static partial void RegistrarOcorrenciaAberta(ILogger logger, string codigo, Guid ocorrenciaId, bool estimado);

    [LoggerMessage(
        EventId = 221,
        Level = LogLevel.Warning,
        Message = "SOS do capacete {Codigo} (ts {Ts:o}) ignorado: o entregador já tem uma ocorrência ativa.")]
    private static partial void RegistrarOcorrenciaJaAtiva(ILogger logger, string codigo, DateTime ts);
}
