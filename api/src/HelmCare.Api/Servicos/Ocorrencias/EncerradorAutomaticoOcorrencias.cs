using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Servicos.Ocorrencias;

/// <summary>
/// Encerramento automático (seção 7 do contrato): toda ocorrência ainda ativa (<c>SOS_CAMADA_1</c>,
/// <c>SOS_CAMADA_2</c> ou <c>SOCORRO_A_CAMINHO</c>) com <c>TEMPO_ENCERRAMENTO_AUTOMATICO_MIN</c> ou mais
/// desde o <c>criadaEm</c> (inclusivo) vai para <c>ENCERRADA</c>, com <c>encerradaAutomaticamente</c>.
/// Roda no mesmo laço do escalonamento (<see cref="ServicoEscalonamentoSos"/>), sem usuário e sem
/// autorização.
/// </summary>
/// <remarks>
/// Cada ocorrência é gravada sozinha: um conflito de concorrência com uma rota (aceite, cancelar ou
/// encerrar ao mesmo tempo) deixa aquela ocorrência para a rodada seguinte, sem impedir as outras. O
/// <c>ENCERRAR</c> sai pelo mesmo ponto do encerramento comum, depois de gravar e sem bloquear.
/// </remarks>
public sealed partial class EncerradorAutomaticoOcorrencias
{
    private readonly IOcorrenciaRepositorio _ocorrencias;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly PublicadorComandosOcorrencia _comandos;
    private readonly TimeProvider _relogio;
    private readonly TimeSpan _tempoEncerramento;
    private readonly ILogger<EncerradorAutomaticoOcorrencias> _logger;

    /// <summary>
    /// Cria o encerrador.
    /// </summary>
    /// <param name="ocorrencias">Repositório de ocorrências.</param>
    /// <param name="unidadeDeTrabalho">Grava cada ocorrência encerrada.</param>
    /// <param name="comandos">Publicação do <c>ENCERRAR</c> sem bloquear.</param>
    /// <param name="relogio">Relógio do servidor.</param>
    /// <param name="opcoes">Constantes do contrato (<c>TEMPO_ENCERRAMENTO_AUTOMATICO_MIN</c>).</param>
    /// <param name="logger">Log da aplicação.</param>
    public EncerradorAutomaticoOcorrencias(
        IOcorrenciaRepositorio ocorrencias,
        IUnidadeDeTrabalho unidadeDeTrabalho,
        PublicadorComandosOcorrencia comandos,
        TimeProvider relogio,
        IOptions<OpcoesHelmCare> opcoes,
        ILogger<EncerradorAutomaticoOcorrencias> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _ocorrencias = ocorrencias;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _comandos = comandos;
        _relogio = relogio;
        _tempoEncerramento = opcoes.Value.TempoEncerramentoAutomatico;
        _logger = logger;
    }

    /// <summary>
    /// Encerra, uma por vez, as ocorrências ativas que passaram do tempo.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento (parada da aplicação).</param>
    /// <returns>Quantas ocorrências foram encerradas nesta rodada.</returns>
    public async Task<int> EncerrarVencidasAsync(CancellationToken cancellationToken)
    {
        DateTime agora = _relogio.GetUtcNow().UtcDateTime;

        IReadOnlyList<Ocorrencia> vencidas =
            await _ocorrencias.ListarAtivasCriadasAteAsync(agora - _tempoEncerramento, cancellationToken);

        int encerradas = 0;

        foreach (Ocorrencia ocorrencia in vencidas)
        {
            // A consulta já filtrou, mas a regra é do domínio: só encerra o que ele confirmar.
            if (!ocorrencia.DeveEncerrarAutomaticamente(agora, _tempoEncerramento))
            {
                continue;
            }

            ocorrencia.EncerrarAutomaticamente(agora, _tempoEncerramento);

            try
            {
                await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Esquece só a ocorrência em conflito; ela volta, relida, na próxima rodada.
                foreach (EntityEntry entrada in ex.Entries)
                {
                    entrada.State = EntityState.Detached;
                }

                RegistrarConflito(_logger, ocorrencia.Id);
                continue;
            }

            encerradas++;
            RegistrarEncerrada(_logger, ocorrencia.Id);
            await _comandos.PublicarSemBloquearAsync(ocorrencia, TipoComandoMqtt.Encerrar, cancellationToken);
        }

        return encerradas;
    }

    [LoggerMessage(EventId = 750, Level = LogLevel.Information, Message = "Ocorrência {OcorrenciaId} encerrada automaticamente (tempo de encerramento automático atingido).")]
    private static partial void RegistrarEncerrada(ILogger logger, Guid ocorrenciaId);

    [LoggerMessage(EventId = 751, Level = LogLevel.Debug, Message = "Ocorrência {OcorrenciaId} não foi encerrada automaticamente: foi alterada ao mesmo tempo; fica para a próxima rodada.")]
    private static partial void RegistrarConflito(ILogger logger, Guid ocorrenciaId);
}
