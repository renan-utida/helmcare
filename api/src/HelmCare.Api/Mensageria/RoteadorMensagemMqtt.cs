using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Mensageria.Mensagens;
using HelmCare.Api.Mensageria.Processadores;
using HelmCare.Api.Persistencia.Modelos;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace HelmCare.Api.Mensageria;

/// <summary>
/// Trata uma mensagem recebida do broker, de ponta a ponta: interpreta o tópico, lê o payload,
/// acha o capacete, aplica a telemetria ou o evento e grava tudo num único <c>SalvarAsync</c>.
/// Tem o escopo de uma mensagem (o assinante abre um escopo de DI por mensagem), então um
/// <c>AppDbContext</c> com alterações de uma mensagem recusada nunca contamina a seguinte.
/// </summary>
/// <remarks>
/// <para>Idempotência (seção 6.2): o evento com o mesmo dispositivo, tipo e <c>ts</c> de um já
/// processado é descartado. O registro em <c>EventosMqttProcessados</c> entra na mesma gravação do
/// efeito: ou os dois ficam, ou nenhum. Se duas cópias chegarem juntas, o índice único barra a
/// segunda (SqlState 23505), tratada como duplicata.</para>
/// <para>Nunca lança: toda recusa e todo erro viram log (código do capacete, tipo e <c>ts</c>,
/// nunca o payload) e um <see cref="ResultadoRoteamento"/>.</para>
/// </remarks>
public sealed partial class RoteadorMensagemMqtt
{
    private readonly IDispositivoRepositorio _dispositivos;
    private readonly IEventoMqttProcessadoRepositorio _processados;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly ProcessadorTelemetria _telemetria;
    private readonly FrozenDictionary<TipoEventoMqtt, IProcessadorEvento> _processadores;
    private readonly TimeProvider _relogio;
    private readonly string _raizTopicos;
    private readonly ILogger<RoteadorMensagemMqtt> _logger;

    /// <summary>
    /// Cria o roteador.
    /// </summary>
    /// <param name="dispositivos">Repositório de capacetes.</param>
    /// <param name="processados">Registro de idempotência.</param>
    /// <param name="unidadeDeTrabalho">Grava o efeito e o registro de idempotência juntos.</param>
    /// <param name="telemetria">Processador da telemetria.</param>
    /// <param name="processadores">Um processador por tipo de evento.</param>
    /// <param name="relogio">Relógio do servidor (só para o <c>recebidoEm</c> do registro).</param>
    /// <param name="opcoes">Raiz dos tópicos.</param>
    /// <param name="logger">Log da aplicação.</param>
    /// <exception cref="InvalidOperationException">Quando algum tipo de evento fica sem processador, ou tem dois.</exception>
    public RoteadorMensagemMqtt(
        IDispositivoRepositorio dispositivos,
        IEventoMqttProcessadoRepositorio processados,
        IUnidadeDeTrabalho unidadeDeTrabalho,
        ProcessadorTelemetria telemetria,
        IEnumerable<IProcessadorEvento> processadores,
        TimeProvider relogio,
        IOptions<OpcoesMqtt> opcoes,
        ILogger<RoteadorMensagemMqtt> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _dispositivos = dispositivos;
        _processados = processados;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _telemetria = telemetria;
        _processadores = MontarProcessadores(processadores);
        _relogio = relogio;
        _raizTopicos = opcoes.Value.RaizTopicos;
        _logger = logger;
    }

    /// <summary>
    /// Indexa os processadores pelo tipo e exige exatamente um para cada um dos 5 tipos do contrato.
    /// </summary>
    /// <param name="processadores">Processadores registrados na injeção de dependência.</param>
    /// <returns>O mapa tipo para processador.</returns>
    /// <exception cref="InvalidOperationException">Quando falta processador para algum tipo, ou algum tipo tem dois.</exception>
    public static FrozenDictionary<TipoEventoMqtt, IProcessadorEvento> MontarProcessadores(
        IEnumerable<IProcessadorEvento> processadores)
    {
        ArgumentNullException.ThrowIfNull(processadores);

        List<IProcessadorEvento> lista = processadores.ToList();

        string[] repetidos = lista.GroupBy(p => p.Tipo)
            .Where(g => g.Count() > 1)
            .Select(g => NomeContrato.Para(g.Key))
            .ToArray();

        if (repetidos.Length > 0)
        {
            throw new InvalidOperationException(
                $"Mais de um processador de evento MQTT registrado para: {string.Join(", ", repetidos)}.");
        }

        string[] faltando = Enum.GetValues<TipoEventoMqtt>()
            .Where(t => !lista.Exists(p => p.Tipo == t))
            .Select(NomeContrato.Para)
            .ToArray();

        if (faltando.Length > 0)
        {
            throw new InvalidOperationException(
                $"Falta registrar o processador de evento MQTT para: {string.Join(", ", faltando)}.");
        }

        return lista.ToFrozenDictionary(p => p.Tipo);
    }

    /// <summary>
    /// Processa uma mensagem recebida. Nunca lança (exceto cancelamento).
    /// </summary>
    /// <param name="mensagem">Tópico e payload.</param>
    /// <param name="cancellationToken">Cancelamento da operação.</param>
    /// <returns>O que foi feito com a mensagem.</returns>
    public async Task<ResultadoRoteamento> ProcessarAsync(MensagemMqttRecebida mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        if (!Topicos.TentarInterpretar(_raizTopicos, mensagem.Topico, out string? codigo, out CanalMqtt canal))
        {
            RegistrarTopicoInvalido(_logger);
            return ResultadoRoteamento.Descartada;
        }

        return canal == CanalMqtt.Telemetria
            ? await ProcessarTelemetriaAsync(codigo, mensagem.Payload, cancellationToken)
            : await ProcessarEventoAsync(codigo, mensagem.Payload, cancellationToken);
    }

    private async Task<ResultadoRoteamento> ProcessarTelemetriaAsync(
        string codigo,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!LeitorMensagemMqtt.TentarLerTelemetria(codigo, payload, out TelemetriaMensagem? telemetria, out string motivo))
        {
            RegistrarMensagemRecusada(_logger, codigo, CanalMqtt.Telemetria, motivo);
            return ResultadoRoteamento.Descartada;
        }

        try
        {
            Dispositivo? dispositivo = await _dispositivos.ObterPorCodigoAsync(codigo, cancellationToken);

            if (dispositivo is null)
            {
                RegistrarCapaceteDesconhecido(_logger, codigo, CanalMqtt.Telemetria);
                return ResultadoRoteamento.Descartada;
            }

            if (!_telemetria.Processar(dispositivo, telemetria))
            {
                return ResultadoRoteamento.Ignorada;
            }

            await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
            return ResultadoRoteamento.Processada;
        }
        catch (DominioException ex)
        {
            RegistrarRecusaDoDominio(_logger, codigo, "TELEMETRIA", telemetria.Ts, ex.Message);
            return ResultadoRoteamento.Descartada;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RegistrarFalha(_logger, ex.GetType().Name, codigo, "TELEMETRIA", telemetria.Ts);
            return ResultadoRoteamento.Falhou;
        }
    }

    private async Task<ResultadoRoteamento> ProcessarEventoAsync(
        string codigo,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!LeitorMensagemMqtt.TentarLerEvento(codigo, payload, out EventoMensagem? evento, out string motivo))
        {
            RegistrarMensagemRecusada(_logger, codigo, CanalMqtt.Evento, motivo);
            return ResultadoRoteamento.Descartada;
        }

        string tipo = NomeContrato.Para(evento.Tipo);

        try
        {
            Dispositivo? dispositivo = await _dispositivos.ObterPorCodigoAsync(codigo, cancellationToken);

            if (dispositivo is null)
            {
                RegistrarCapaceteDesconhecido(_logger, codigo, CanalMqtt.Evento);
                return ResultadoRoteamento.Descartada;
            }

            if (await _processados.JaProcessadoAsync(codigo, evento.Tipo, evento.Ts, cancellationToken))
            {
                RegistrarDuplicata(_logger, codigo, tipo, evento.Ts);
                return ResultadoRoteamento.Duplicada;
            }

            await _processadores[evento.Tipo].ProcessarAsync(new ContextoEvento(dispositivo, evento), cancellationToken);

            await _processados.AdicionarAsync(
                new EventoMqttProcessado(codigo, evento.Tipo, evento.Ts, _relogio.GetUtcNow().UtcDateTime),
                cancellationToken);

            await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
            return ResultadoRoteamento.Processada;
        }
        catch (DominioException ex)
        {
            // Ex.: CONTAGEM_INICIADA repetida com outro ts. A mensagem do domínio não tem dado pessoal.
            RegistrarRecusaDoDominio(_logger, codigo, tipo, evento.Ts, ex.Message);
            return ResultadoRoteamento.Descartada;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Outra cópia do mesmo evento foi gravada entre a consulta e a gravação.
            RegistrarDuplicata(_logger, codigo, tipo, evento.Ts);
            return ResultadoRoteamento.Duplicada;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RegistrarFalha(_logger, ex.GetType().Name, codigo, tipo, evento.Ts);
            return ResultadoRoteamento.Falhou;
        }
    }

    [LoggerMessage(EventId = 200, Level = LogLevel.Warning, Message = "Mensagem MQTT descartada: tópico fora do padrão {{raiz}}/{{codigo}}/{{telemetria|evento}}.")]
    private static partial void RegistrarTopicoInvalido(ILogger logger);

    [LoggerMessage(EventId = 201, Level = LogLevel.Warning, Message = "Mensagem MQTT de {Codigo} ({Canal}) descartada: {Motivo}.")]
    private static partial void RegistrarMensagemRecusada(ILogger logger, string codigo, CanalMqtt canal, string motivo);

    [LoggerMessage(EventId = 202, Level = LogLevel.Warning, Message = "Mensagem MQTT ({Canal}) de capacete desconhecido {Codigo} descartada.")]
    private static partial void RegistrarCapaceteDesconhecido(ILogger logger, string codigo, CanalMqtt canal);

    [LoggerMessage(EventId = 203, Level = LogLevel.Debug, Message = "Evento {Tipo} do capacete {Codigo} (ts {Ts:o}) já processado; descartado.")]
    private static partial void RegistrarDuplicata(ILogger logger, string codigo, string tipo, DateTime ts);

    [LoggerMessage(EventId = 204, Level = LogLevel.Warning, Message = "Mensagem {Tipo} do capacete {Codigo} (ts {Ts:o}) recusada pelo domínio: {Motivo}")]
    private static partial void RegistrarRecusaDoDominio(ILogger logger, string codigo, string tipo, DateTime ts, string motivo);

    [LoggerMessage(EventId = 205, Level = LogLevel.Error, Message = "Falha {TipoExcecao} ao processar {Tipo} do capacete {Codigo} (ts {Ts:o}); nada foi gravado.")]
    private static partial void RegistrarFalha(ILogger logger, string tipoExcecao, string codigo, string tipo, DateTime ts);
}
