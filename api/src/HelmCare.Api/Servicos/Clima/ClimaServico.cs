using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Clima;
using HelmCare.Api.Integracoes.OpenMeteo;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Risco;
using HelmCare.Dominio.Comum;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Servicos.Clima;

/// <summary>
/// <c>GET /api/v1/clima</c> (seção 7 do contrato, 1.17): as condições atuais da cidade pela Open-Meteo,
/// com o índice de calor e o alerta, para qualquer entregador logado. A última resposta boa fica 10 min
/// no cache; sem a Open-Meteo e sem cache válido, <c>503</c>, sem valor simulado. A previsão simulada
/// forçada do <c>POST /turnos</c> não vale aqui.
/// </summary>
public sealed partial class ClimaServico
{
    private readonly ClienteOpenMeteo _cliente;
    private readonly CacheClima _cache;
    private readonly IAutorizacaoRecursos _autorizacao;
    private readonly TimeProvider _relogio;
    private readonly TimeSpan _validadeCache;
    private readonly double _alertaCalorC;
    private readonly ILogger<ClimaServico> _logger;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="cliente">Cliente da Open-Meteo.</param>
    /// <param name="cache">Cache da última resposta boa.</param>
    /// <param name="autorizacao">Exige o papel ENTREGADOR.</param>
    /// <param name="relogio">Relógio do cache e da medição do tempo.</param>
    /// <param name="opcoesOpenMeteo">Validade do cache.</param>
    /// <param name="opcoes">Constantes do contrato (<c>ALERTA_CALOR_C</c>).</param>
    /// <param name="logger">Log da aplicação.</param>
    public ClimaServico(
        ClienteOpenMeteo cliente,
        CacheClima cache,
        IAutorizacaoRecursos autorizacao,
        TimeProvider relogio,
        IOptions<OpcoesOpenMeteo> opcoesOpenMeteo,
        IOptions<OpcoesHelmCare> opcoes,
        ILogger<ClimaServico> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoesOpenMeteo);
        ArgumentNullException.ThrowIfNull(opcoes);

        _cliente = cliente;
        _cache = cache;
        _autorizacao = autorizacao;
        _relogio = relogio;
        _validadeCache = TimeSpan.FromMinutes(opcoesOpenMeteo.Value.CacheClimaMin);
        _alertaCalorC = opcoes.Value.AlertaCalorC;
        _logger = logger;
    }

    /// <summary>
    /// Condições atuais da cidade.
    /// </summary>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O clima, do cache ou da Open-Meteo.</returns>
    /// <exception cref="NaoAutenticadoException">Sem token (401).</exception>
    /// <exception cref="AcessoNegadoException">Token de socorrista (403).</exception>
    /// <exception cref="PrevisaoIndisponivelException">Open-Meteo fora e cache vencido (503).</exception>
    public async Task<ClimaResposta> ObterAsync(CancellationToken cancellationToken)
    {
        _autorizacao.ExigirEntregador();

        if (_cache.TentarObter(_relogio.GetUtcNow(), _validadeCache, out ClimaResposta? guardada, out TimeSpan idade))
        {
            long idadeS = (long)idade.TotalSeconds;
            RegistrarCache(_logger, idadeS);
            return guardada!;
        }

        long inicioMedicao = _relogio.GetTimestamp();

        try
        {
            CondicaoTempo atual = await _cliente.ObterAtualAsync(cancellationToken);
            ClimaResposta resposta = Montar(atual, _alertaCalorC);
            _cache.Guardar(resposta, _relogio.GetUtcNow());
            long tempoMs = Milissegundos(inicioMedicao);
            RegistrarSucesso(_logger, tempoMs);
            return resposta;
        }
        catch (OpenMeteoIndisponivelException e)
        {
            long tempoMs = Milissegundos(inicioMedicao);
            RegistrarFalha(_logger, e.Motivo, tempoMs);
            throw new PrevisaoIndisponivelException("Open-Meteo indisponível e sem cache válido.", e);
        }
    }

    /// <summary>
    /// Monta o clima: temperatura e chuva com 1 casa, umidade inteira, nas faixas da seção 8; o índice
    /// de calor (seção 6.1) dos valores já arredondados, com 1 casa; e o alerta pelo índice arredondado,
    /// para o cartão nunca mostrar 32,0 °C sem o alerta.
    /// </summary>
    /// <param name="atual">Condições atuais da Open-Meteo.</param>
    /// <param name="alertaCalorC"><c>ALERTA_CALOR_C</c>.</param>
    /// <returns>O corpo do <c>GET /clima</c>.</returns>
    public static ClimaResposta Montar(CondicaoTempo atual, double alertaCalorC)
    {
        ArgumentNullException.ThrowIfNull(atual);

        double temperatura = RegrasPrevisao.Temperatura(atual.TemperaturaC);
        int umidade = RegrasPrevisao.Umidade(atual.UmidadePct);
        double indice = Math.Round(IndiceCalor.CalcularC(temperatura, umidade), 1, MidpointRounding.AwayFromZero);

        return new ClimaResposta
        {
            TemperaturaC = temperatura,
            UmidadePct = umidade,
            ChuvaMmHora = RegrasPrevisao.Chuva(atual.ChuvaMmHora),
            IndiceCalorC = indice,
            AlertaCalor = indice >= alertaCalorC,
            AtualizadoEm = atual.InstanteUtc,
        };
    }

    private long Milissegundos(long inicioMedicao)
    {
        return (long)_relogio.GetElapsedTime(inicioMedicao).TotalMilliseconds;
    }

    [LoggerMessage(EventId = 630, Level = LogLevel.Information, Message = "Clima obtido da Open-Meteo em {TempoMs} ms.")]
    private static partial void RegistrarSucesso(ILogger logger, long tempoMs);

    [LoggerMessage(EventId = 631, Level = LogLevel.Information, Message = "Clima servido do cache (guardado há {IdadeS} s), sem chamar a Open-Meteo.")]
    private static partial void RegistrarCache(ILogger logger, long idadeS);

    [LoggerMessage(EventId = 632, Level = LogLevel.Warning, Message = "Open-Meteo indisponível no clima (motivo {Motivo}, {TempoMs} ms) e sem cache válido: 503.")]
    private static partial void RegistrarFalha(ILogger logger, MotivoFalhaOpenMeteo motivo, long tempoMs);
}
