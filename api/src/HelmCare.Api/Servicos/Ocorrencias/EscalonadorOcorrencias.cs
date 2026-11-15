using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Servicos.Ocorrencias;

/// <summary>
/// Uma rodada do escalonamento (seção 7, "Regras do SOS na API"): toda ocorrência em
/// <c>SOS_CAMADA_1</c> sem aceite há <c>TEMPO_ACEITE_CAMADA_1_S</c> ou mais (60 s exatos já escalam)
/// passa para <c>SOS_CAMADA_2</c>, a rede toda. Quem chama a rodada a cada intervalo é o
/// <see cref="ServicoEscalonamentoSos"/>; esta classe tem o escopo de uma rodada.
/// </summary>
public sealed partial class EscalonadorOcorrencias
{
    private readonly IOcorrenciaRepositorio _ocorrencias;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly TimeProvider _relogio;
    private readonly TimeSpan _tempoAceiteCamada1;
    private readonly ILogger<EscalonadorOcorrencias> _logger;

    /// <summary>
    /// Cria o escalonador.
    /// </summary>
    /// <param name="ocorrencias">Repositório de ocorrências.</param>
    /// <param name="unidadeDeTrabalho">Grava cada ocorrência escalada.</param>
    /// <param name="relogio">Relógio do servidor (o mesmo do <c>criadaEm</c>).</param>
    /// <param name="opcoes">Constantes do contrato (<c>TEMPO_ACEITE_CAMADA_1_S</c>).</param>
    /// <param name="logger">Log da aplicação.</param>
    public EscalonadorOcorrencias(
        IOcorrenciaRepositorio ocorrencias,
        IUnidadeDeTrabalho unidadeDeTrabalho,
        TimeProvider relogio,
        IOptions<OpcoesHelmCare> opcoes,
        ILogger<EscalonadorOcorrencias> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _ocorrencias = ocorrencias;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _relogio = relogio;
        _tempoAceiteCamada1 = TimeSpan.FromSeconds(opcoes.Value.TempoAceiteCamada1S);
        _logger = logger;
    }

    /// <summary>
    /// Escala as ocorrências da camada 1 com o tempo de aceite esgotado, gravando uma por vez.
    /// </summary>
    /// <remarks>
    /// Um aceite gravado ao mesmo tempo troca a versão da linha (<c>xmin</c>), e a gravação daquela
    /// ocorrência falha com <see cref="DbUpdateConcurrencyException"/>. Isso é esperado (quem aceitou
    /// ganha): registra em Debug, solta a ocorrência do contexto para ela não entrar de novo na
    /// próxima gravação e segue para as outras.
    /// </remarks>
    /// <param name="cancellationToken">Cancelamento (parada da API).</param>
    /// <returns>Quantas ocorrências escalaram nesta rodada.</returns>
    public async Task<int> EscalarVencidasAsync(CancellationToken cancellationToken)
    {
        DateTime agora = _relogio.GetUtcNow().UtcDateTime;

        IReadOnlyList<Ocorrencia> vencidas =
            await _ocorrencias.ListarCamada1VencidasAsync(agora - _tempoAceiteCamada1, cancellationToken);

        int escaladas = 0;

        foreach (Ocorrencia ocorrencia in vencidas)
        {
            // A consulta já filtrou, mas a regra é do domínio: só escala o que ele confirmar.
            if (!ocorrencia.DeveEscalar(agora, _tempoAceiteCamada1))
            {
                continue;
            }

            ocorrencia.EscalarParaCamada2(agora, _tempoAceiteCamada1);

            try
            {
                await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (EntityEntry entrada in ex.Entries)
                {
                    entrada.State = EntityState.Detached;
                }

                RegistrarConflito(_logger, ocorrencia.Id);
                continue;
            }

            escaladas++;
            RegistrarEscalada(_logger, ocorrencia.Id);
        }

        return escaladas;
    }

    [LoggerMessage(EventId = 720, Level = LogLevel.Information, Message = "Ocorrência {OcorrenciaId} escalada para SOS_CAMADA_2 (sem aceite no tempo da camada 1).")]
    private static partial void RegistrarEscalada(ILogger logger, Guid ocorrenciaId);

    [LoggerMessage(EventId = 721, Level = LogLevel.Debug, Message = "Ocorrência {OcorrenciaId} não escalou: foi alterada ao mesmo tempo (provavelmente aceita).")]
    private static partial void RegistrarConflito(ILogger logger, Guid ocorrenciaId);
}
