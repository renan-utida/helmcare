using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Ocorrencias;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.ObjetosDeValor;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Servicos.Ocorrencias;

/// <summary>
/// Consulta de uma ocorrência (<c>GET /ocorrencias/{id}?voluntarioId=</c>): status, localização
/// e o cronômetro desde a queda. Nenhum dado do entregador sai daqui.
/// </summary>
public sealed class OcorrenciaServico
{
    private readonly IOcorrenciaRepositorio _ocorrencias;
    private readonly IVoluntarioRepositorio _voluntarios;
    private readonly TimeProvider _relogio;
    private readonly int _metaTempoAceiteS;
    private readonly TimeSpan _trechoCapacetePadrao;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="ocorrencias">Repositório de ocorrências.</param>
    /// <param name="voluntarios">Repositório de voluntários.</param>
    /// <param name="relogio">Relógio do servidor, para o cronômetro que ainda corre.</param>
    /// <param name="opcoes">Constantes do contrato (<c>META_TEMPO_ACEITE_S</c>, <c>JANELA_OBSERVACAO_S</c> e <c>CONTAGEM_CANCELAMENTO_S</c>).</param>
    public OcorrenciaServico(
        IOcorrenciaRepositorio ocorrencias,
        IVoluntarioRepositorio voluntarios,
        TimeProvider relogio,
        IOptions<OpcoesHelmCare> opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _ocorrencias = ocorrencias;
        _voluntarios = voluntarios;
        _relogio = relogio;
        _metaTempoAceiteS = opcoes.Value.MetaTempoAceiteS;
        _trechoCapacetePadrao = opcoes.Value.TrechoCapacetePadrao;
    }

    /// <summary>
    /// Devolve o status da ocorrência. A localização só vem exata quando o
    /// <paramref name="voluntarioId"/> é o do voluntário que aceitou; sem ele, ou com outro
    /// voluntário, vem arredondada para 2 casas (seção 9 do contrato).
    /// </summary>
    /// <param name="id">Id da ocorrência.</param>
    /// <param name="voluntarioId">Voluntário que consulta (opcional).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O status e o cronômetro.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Ocorrência inexistente, ou voluntário informado e inexistente (404).</exception>
    public async Task<OcorrenciaResposta> ObterAsync(Guid id, Guid? voluntarioId, CancellationToken cancellationToken)
    {
        Ocorrencia ocorrencia = await _ocorrencias.ObterParaConsultaAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Ocorrência não encontrada.");

        if (voluntarioId is { } informado && !await _voluntarios.ExisteAsync(informado, cancellationToken))
        {
            throw new RecursoNaoEncontradoException("Voluntário não encontrado.");
        }

        Guid? quemAceitou = ocorrencia.Aceites.FirstOrDefault()?.VoluntarioId;
        bool exata = voluntarioId is not null && voluntarioId == quemAceitou;

        var local = new Coordenada(ocorrencia.Latitude, ocorrencia.Longitude);
        Coordenada mostrada = exata ? local : local.Aproximada();

        // O cronômetro para no aceite; sem aceite, no encerramento; senão, corre até agora. A conta
        // é a mesma do indicador (dois trechos, cada um no próprio relógio).
        bool parado = ocorrencia.AceitaEm is not null || ocorrencia.EncerradaEm is not null;
        DateTime fim = ocorrencia.AceitaEm ?? ocorrencia.EncerradaEm ?? _relogio.GetUtcNow().UtcDateTime;

        return new OcorrenciaResposta
        {
            Id = ocorrencia.Id,
            Estado = ocorrencia.Estado,
            Camada = ocorrencia.Camada,
            CriadaEm = ocorrencia.CriadaEm,
            AceitaEm = ocorrencia.AceitaEm,
            EncerradaEm = ocorrencia.EncerradaEm,
            TempoAteAceiteSegundos = ocorrencia.TempoAteAceiteSegundos,
            Latitude = mostrada.Latitude,
            Longitude = mostrada.Longitude,
            LocalizacaoExata = exata,
            Cronometro = new CronometroResposta
            {
                SegundosDesdeQueda = ocorrencia.SegundosDesdeAQueda(fim, _trechoCapacetePadrao),
                MetaSegundos = _metaTempoAceiteS,
                Parado = parado,
            },
        };
    }
}
