using System;
using System.Collections.Generic;
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
/// Consultas de ocorrências: o status de uma (<c>GET /ocorrencias/{id}?voluntarioId=</c>) e os
/// chamados que um voluntário pode atender (<c>GET /ocorrencias/proximas?voluntarioId=</c>), com
/// localização e o cronômetro desde a queda. Nenhum dado do entregador sai daqui.
/// </summary>
public sealed class OcorrenciaServico
{
    private readonly IOcorrenciaRepositorio _ocorrencias;
    private readonly IVoluntarioRepositorio _voluntarios;
    private readonly TimeProvider _relogio;
    private readonly int _metaTempoAceiteS;
    private readonly TimeSpan _trechoCapacetePadrao;
    private readonly double _raioCamada1Km;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="ocorrencias">Repositório de ocorrências.</param>
    /// <param name="voluntarios">Repositório de voluntários.</param>
    /// <param name="relogio">Relógio do servidor, para o cronômetro que ainda corre.</param>
    /// <param name="opcoes">Constantes do contrato (<c>META_TEMPO_ACEITE_S</c>, <c>JANELA_OBSERVACAO_S</c>, <c>CONTAGEM_CANCELAMENTO_S</c> e <c>RAIO_CAMADA_1_KM</c>).</param>
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
        _raioCamada1Km = opcoes.Value.RaioCamada1Km;
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

        return MontarResposta(ocorrencia, voluntarioId);
    }

    /// <summary>
    /// Monta o corpo do <c>GET /ocorrencias/{id}</c> a partir da ocorrência já carregada (com o impacto
    /// e os aceites). É o mesmo corpo do aceite (para quem aceitou, com a localização exata), do
    /// cancelar e do encerrar (sem voluntário, com a aproximada).
    /// </summary>
    /// <param name="ocorrencia">Ocorrência com o impacto e os aceites carregados.</param>
    /// <param name="voluntarioId">Voluntário que consulta, ou nulo.</param>
    /// <returns>O status e o cronômetro.</returns>
    public OcorrenciaResposta MontarResposta(Ocorrencia ocorrencia, Guid? voluntarioId)
    {
        ArgumentNullException.ThrowIfNull(ocorrencia);

        Guid? quemAceitou = ocorrencia.Aceites.FirstOrDefault()?.VoluntarioId;
        bool exata = voluntarioId is not null && voluntarioId == quemAceitou;

        var local = new Coordenada(ocorrencia.Latitude, ocorrencia.Longitude);
        Coordenada mostrada = exata ? local : local.Aproximada();

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
            Cronometro = Cronometro(ocorrencia, _relogio.GetUtcNow().UtcDateTime),
        };
    }

    /// <summary>
    /// Devolve os chamados que o voluntário pode atender: na camada 1, a até <c>RAIO_CAMADA_1_KM</c>
    /// (inclusivo); na camada 2, todos; nunca o SOS do próprio entregador. Quem decide é o
    /// <see cref="Voluntario.PodeAtender"/>, com a disponibilidade e a posição do último
    /// <c>PUT /voluntarios/{id}/disponibilidade</c>.
    /// </summary>
    /// <remarks>
    /// Cada chamado leva a localização aproximada (2 casas) e a distância, com 1 casa, da posição do
    /// voluntário até essa localização aproximada (seção 7, "Distância mostrada"). A ordem é a da
    /// distância mostrada e, no empate, a do <c>criadaEm</c>.
    /// </remarks>
    /// <param name="voluntarioId">Voluntário que consulta.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>Os chamados, ou a lista vazia para o voluntário indisponível.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Voluntário inexistente (404).</exception>
    public async Task<IReadOnlyList<OcorrenciaProximaResposta>> ListarProximasAsync(
        Guid voluntarioId,
        CancellationToken cancellationToken)
    {
        Voluntario voluntario = await _voluntarios.ObterParaConsultaAsync(voluntarioId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Voluntário não encontrado.");

        // Indisponível (sem posição) não atende nada: responde vazio sem consultar as ocorrências.
        if (!voluntario.Disponivel || voluntario.Latitude is not { } latitude || voluntario.Longitude is not { } longitude)
        {
            return [];
        }

        IReadOnlyList<Ocorrencia> aguardando =
            await _ocorrencias.ListarAguardandoAceiteDeOutrosAsync(voluntario.EntregadorId, cancellationToken);

        var posicao = new Coordenada(latitude, longitude);
        DateTime agora = _relogio.GetUtcNow().UtcDateTime;

        return aguardando
            .Where(o => voluntario.PodeAtender(o, _raioCamada1Km))
            .Select(o => Chamado(o, posicao, agora))
            .OrderBy(c => c.DistanciaKm)
            .ThenBy(c => c.CriadaEm)
            .ToList();
    }

    private OcorrenciaProximaResposta Chamado(Ocorrencia ocorrencia, Coordenada posicaoVoluntario, DateTime agora)
    {
        Coordenada aproximada = new Coordenada(ocorrencia.Latitude, ocorrencia.Longitude).Aproximada();

        return new OcorrenciaProximaResposta
        {
            OcorrenciaId = ocorrencia.Id,
            Estado = ocorrencia.Estado,
            Camada = ocorrencia.Camada,
            CriadaEm = ocorrencia.CriadaEm,
            Latitude = aproximada.Latitude,
            Longitude = aproximada.Longitude,
            DistanciaKm = Math.Round(posicaoVoluntario.DistanciaKmAte(aproximada), 1, MidpointRounding.AwayFromZero),
            Cronometro = Cronometro(ocorrencia, agora),
        };
    }

    // O cronômetro para no aceite; sem aceite, no encerramento; senão, corre até agora. A conta
    // é a mesma do indicador (dois trechos, cada um no próprio relógio). Serve às duas consultas.
    private CronometroResposta Cronometro(Ocorrencia ocorrencia, DateTime agora)
    {
        bool parado = ocorrencia.AceitaEm is not null || ocorrencia.EncerradaEm is not null;
        DateTime fim = ocorrencia.AceitaEm ?? ocorrencia.EncerradaEm ?? agora;

        return new CronometroResposta
        {
            SegundosDesdeQueda = ocorrencia.SegundosDesdeAQueda(fim, _trechoCapacetePadrao),
            MetaSegundos = _metaTempoAceiteS,
            Parado = parado,
        };
    }
}
