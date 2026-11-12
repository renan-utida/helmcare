using System;
using System.Globalization;
using System.Text;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Mensageria.Mensagens;
using HelmCare.Api.Mensageria.Processadores;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace HelmCare.Testes.Construtores;

/// <summary>
/// Monta o roteador e os processadores com fakes, do mesmo jeito que o Program.cs, e gera os
/// payloads do capacete. Um cenário por teste (os testes rodam em paralelo).
/// </summary>
internal sealed class CenarioMqtt
{
    public const string Raiz = "helmcare-rdu/v1";

    public const string Codigo = "HC-0001";

    // Relógio do servidor, de propósito 3 horas à frente do ts dos eventos (o Wokwi atrasa).
    public static readonly DateTimeOffset AgoraServidor = new(2026, 11, 12, 22, 0, 0, TimeSpan.Zero);

    public static readonly DateTime TsImpacto = new(2026, 11, 12, 19, 0, 0, DateTimeKind.Utc);

    public CenarioMqtt()
    {
        Relogio = new FakeTimeProvider(AgoraServidor);
        Dispositivo = new Dispositivo(Codigo, Guid.NewGuid());
        Dispositivos.Semear(Dispositivo);
    }

    public FakeTimeProvider Relogio { get; }

    public Dispositivo Dispositivo { get; }

    public FakeDispositivoRepositorio Dispositivos { get; } = new();

    public FakeEventoImpactoRepositorio Impactos { get; } = new();

    public FakeOcorrenciaRepositorio Ocorrencias { get; } = new();

    public FakeEventoMqttProcessadoRepositorio Processados { get; } = new();

    public FakeUnidadeDeTrabalho Unidade { get; } = new();

    public OpcoesHelmCare OpcoesHelmCare { get; } = new();

    public static ReadOnlyMemory<byte> Bytes(string json)
    {
        return Encoding.UTF8.GetBytes(json);
    }

    public static string Iso(DateTime ts)
    {
        return ts.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    public static string EventoJson(string tipo, DateTime ts, bool pilotandoAntes = true, bool presencaAntes = true)
    {
        string pilotando = pilotandoAntes ? "true" : "false";
        string presenca = presencaAntes ? "true" : "false";
        return $$"""
            {"dispositivo":"{{Codigo}}","ts":"{{Iso(ts)}}","tipo":"{{tipo}}","picoG":2.54,"orientacaoAnormal":true,"pilotandoAntes":{{pilotando}},"presencaAntes":{{presenca}},"latitude":-23.5641,"longitude":-46.6525}
            """;
    }

    public static string TelemetriaJson(string estado, DateTime ts, string indiceCalor = "39.8")
    {
        return $$"""
            {"dispositivo":"{{Codigo}}","ts":"{{Iso(ts)}}","estado":"{{estado}}","temperaturaC":33.4,"umidadePct":58,"indiceCalorC":{{indiceCalor}},"presenca":true,"velocidadeKmh":0}
            """;
    }

    public static MensagemMqttRecebida Evento(string tipo, DateTime ts, bool pilotandoAntes = true, bool presencaAntes = true)
    {
        return new MensagemMqttRecebida($"{Raiz}/{Codigo}/evento", Bytes(EventoJson(tipo, ts, pilotandoAntes, presencaAntes)));
    }

    public static MensagemMqttRecebida Telemetria(string estado, DateTime ts)
    {
        return new MensagemMqttRecebida($"{Raiz}/{Codigo}/telemetria", Bytes(TelemetriaJson(estado, ts)));
    }

    public static EventoMensagem EventoLido(TipoEventoMqtt tipo, DateTime ts, bool pilotandoAntes = true, bool presencaAntes = true)
    {
        return new EventoMensagem
        {
            Dispositivo = Codigo,
            Ts = ts,
            Tipo = tipo,
            PicoG = 2.54,
            OrientacaoAnormal = true,
            PilotandoAntes = pilotandoAntes,
            PresencaAntes = presencaAntes,
            Latitude = -23.5641,
            Longitude = -46.6525,
        };
    }

    public ContextoEvento Contexto(TipoEventoMqtt tipo, DateTime ts, bool pilotandoAntes = true, bool presencaAntes = true)
    {
        return new ContextoEvento(Dispositivo, EventoLido(tipo, ts, pilotandoAntes, presencaAntes));
    }

    public EventoImpacto SemearImpactoEmAberto(DateTime? ts = null)
    {
        var impacto = EventoImpacto.Registrar(Dispositivo.Id, ts ?? TsImpacto, 2.54, true, false, true);
        Impactos.Semear(impacto);
        return impacto;
    }

    public IProcessadorEvento[] Processadores()
    {
        IOptions<OpcoesHelmCare> opcoes = Options.Create(OpcoesHelmCare);

        return
        [
            new ProcessadorImpactoDetectado(Impactos),
            new ProcessadorContagemIniciada(Impactos, opcoes, NullLogger<ProcessadorContagemIniciada>.Instance),
            new ProcessadorFalsoPositivo(Impactos, NullLogger<ProcessadorFalsoPositivo>.Instance),
            new ProcessadorCancelado(Impactos, NullLogger<ProcessadorCancelado>.Instance),
            new ProcessadorSos(Impactos, Ocorrencias, Relogio, opcoes, NullLogger<ProcessadorSos>.Instance),
        ];
    }

    public IProcessadorEvento Processador(TipoEventoMqtt tipo)
    {
        return Array.Find(Processadores(), p => p.Tipo == tipo)!;
    }

    public RoteadorMensagemMqtt Roteador()
    {
        return new RoteadorMensagemMqtt(
            Dispositivos,
            Processados,
            Unidade,
            new ProcessadorTelemetria(),
            Processadores(),
            Relogio,
            Options.Create(new OpcoesMqtt()),
            NullLogger<RoteadorMensagemMqtt>.Instance);
    }
}
