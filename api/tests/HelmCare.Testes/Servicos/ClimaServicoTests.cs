using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Clima;
using HelmCare.Api.Integracoes.OpenMeteo;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Clima;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <c>GET /clima</c> (<see cref="ClimaServico"/>, seção 7 do contrato, 1.17), sem rede: o
/// índice de calor e o alerta, o papel, o cache de 10 min com o <see cref="FakeTimeProvider"/> e o 503.
/// </summary>
[TestClass]
public sealed class ClimaServicoTests : IDisposable
{
    private readonly FakeHttpMessageHandler _handler = new();
    private readonly HttpClient _http;
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 18, 18, 5, 0, TimeSpan.Zero));
    private readonly CacheClima _cache = new();
    private readonly FakeLogger<ClimaServico> _logger = new();

    /// <summary>
    /// Monta o HttpClient sobre o handler falso.
    /// </summary>
    public ClimaServicoTests()
    {
        _http = RespostasOpenMeteo.Http(_handler);
    }

    /// <summary>
    /// Descarta o HttpClient e o handler.
    /// </summary>
    public void Dispose()
    {
        _http.Dispose();
        _handler.Dispose();
    }

    /// <summary>
    /// Garante o exemplo do contrato: 29,0 °C com 55% dá o índice 30,3 e alertaCalor falso, com o horário
    /// da medição em UTC e a fonte.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_ExemploDoContrato_Indice30ponto3SemAlerta()
    {
        // Arrange
        _handler.Responder(HttpStatusCode.OK, RespostasOpenMeteo.Atual("2026-11-18T18:00", 29.0, 55, 0.0));

        // Act
        ClimaResposta clima = await Servico(Entregador()).ObterAsync(CancellationToken.None);

        // Assert
        Assert.AreEqual(29.0, clima.TemperaturaC);
        Assert.AreEqual(55, clima.UmidadePct);
        Assert.AreEqual(0.0, clima.ChuvaMmHora);
        Assert.AreEqual(30.3, clima.IndiceCalorC);
        Assert.IsFalse(clima.AlertaCalor);
        Assert.AreEqual(new DateTime(2026, 11, 18, 18, 0, 0, DateTimeKind.Utc), clima.AtualizadoEm);
        Assert.AreEqual(DateTimeKind.Utc, clima.AtualizadoEm.Kind);
        Assert.AreEqual("Open-Meteo", clima.Fonte);
        Assert.Contains("current=temperature_2m,relative_humidity_2m,precipitation", _handler.Requisicoes[0].Endereco!.Query);
    }

    /// <summary>
    /// Garante o alerta no limite, pelo índice já arredondado: 30,6 °C com 50% dá 31,968, que vira 32,0
    /// (alerta); 30,0 °C com 55% dá 31,887, que vira 31,9 (sem alerta).
    /// </summary>
    /// <param name="t">Temperatura.</param>
    /// <param name="u">Umidade.</param>
    /// <param name="indice">Índice esperado.</param>
    /// <param name="alerta">Alerta esperado.</param>
    [TestMethod]
    [DataRow(30.6, 50.0, 32.0, true)]
    [DataRow(30.0, 55.0, 31.9, false)]
    public void Montar_LimiteDoAlerta_PeloIndiceArredondado(double t, double u, double indice, bool alerta)
    {
        // Act
        ClimaResposta clima = ClimaServico.Montar(new CondicaoTempo(DateTime.UtcNow, t, u, 0), 32);

        // Assert
        Assert.AreEqual(indice, clima.IndiceCalorC);
        Assert.AreEqual(alerta, clima.AlertaCalor);
    }

    /// <summary>
    /// Garante os arredondamentos e as faixas também no clima.
    /// </summary>
    [TestMethod]
    public void Montar_ValoresForaDaFaixa_Limitados()
    {
        // Act
        ClimaResposta clima = ClimaServico.Montar(new CondicaoTempo(DateTime.UtcNow, 18.25, 104.4, 0.04), 32);

        // Assert
        Assert.AreEqual(18.3, clima.TemperaturaC);
        Assert.AreEqual(100, clima.UmidadePct);
        Assert.AreEqual(0.0, clima.ChuvaMmHora);
    }

    /// <summary>
    /// Garante o papel: sem token, 401; com token de socorrista, 403; nos dois, sem chamar a Open-Meteo.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_SemTokenOuSocorrista_401E403()
    {
        // Act e Assert
        await Assert.ThrowsExactlyAsync<NaoAutenticadoException>(() => Servico(FakeUsuarioAtual.Anonimo()).ObterAsync(CancellationToken.None));
        await Assert.ThrowsExactlyAsync<AcessoNegadoException>(() => Servico(FakeUsuarioAtual.Socorrista()).ObterAsync(CancellationToken.None));
        Assert.IsEmpty(_handler.Requisicoes);
    }

    /// <summary>
    /// Garante o cache: duas consultas dentro dos 10 min fazem uma só chamada à Open-Meteo, e a segunda
    /// devolve a mesma resposta.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_DentroDos10Minutos_UmaSoChamada()
    {
        // Arrange
        _handler.Responder(HttpStatusCode.OK, RespostasOpenMeteo.Atual("2026-11-18T18:00", 29.0, 55, 0.0));
        ClimaServico servico = Servico(Entregador());

        // Act
        ClimaResposta primeira = await servico.ObterAsync(CancellationToken.None);
        _relogio.Advance(TimeSpan.FromMinutes(9) + TimeSpan.FromSeconds(59));
        _handler.Responder(HttpStatusCode.OK, RespostasOpenMeteo.Atual("2026-11-18T18:15", 35.0, 80, 5.0));
        ClimaResposta segunda = await servico.ObterAsync(CancellationToken.None);

        // Assert
        Assert.HasCount(1, _handler.Requisicoes);
        Assert.AreSame(primeira, segunda);
        Assert.Contains("cache", _logger.Registros[1].Mensagem);
    }

    /// <summary>
    /// Garante que, com o cache vencido (10 min exatos) e a Open-Meteo fora, a resposta é o 503, sem
    /// valor velho nem simulado; e que o log leva só o motivo e o tempo.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_CacheVencidoEOpenMeteoFora_503()
    {
        // Arrange
        _handler.Responder(HttpStatusCode.OK, RespostasOpenMeteo.Atual("2026-11-18T18:00", 29.0, 55, 0.0));
        ClimaServico servico = Servico(Entregador());
        await servico.ObterAsync(CancellationToken.None);
        _relogio.Advance(TimeSpan.FromMinutes(10));
        _handler.LancarErroDeRede();

        // Act e Assert
        await Assert.ThrowsExactlyAsync<PrevisaoIndisponivelException>(() => servico.ObterAsync(CancellationToken.None));
        Assert.HasCount(2, _handler.Requisicoes);
        string aviso = _logger.Registros[^1].Mensagem;
        Assert.Contains("motivo Falha", aviso);
        Assert.DoesNotContain("-23.5641", aviso);
    }

    /// <summary>
    /// Garante que a previsão simulada forçada não vale para o clima: o serviço chama a Open-Meteo e,
    /// sem ela, responde 503 (nunca a simulada).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_ComAForcadaLigada_ContinuaNaOpenMeteo()
    {
        // Arrange: o serviço nem recebe a opção da forçada; a Open-Meteo está fora.
        _handler.Responder(HttpStatusCode.ServiceUnavailable, "{}");

        // Act e Assert
        await Assert.ThrowsExactlyAsync<PrevisaoIndisponivelException>(() => Servico(Entregador()).ObterAsync(CancellationToken.None));
        Assert.HasCount(1, _handler.Requisicoes);
    }

    private static FakeUsuarioAtual Entregador()
    {
        return FakeUsuarioAtual.Entregador(Guid.NewGuid());
    }

    private ClimaServico Servico(FakeUsuarioAtual usuario)
    {
        return new ClimaServico(
            RespostasOpenMeteo.Cliente(_http),
            _cache,
            new AutorizacaoRecursos(usuario, new FakeVoluntarioRepositorio()),
            _relogio,
            Options.Create(new OpcoesOpenMeteo()),
            Options.Create(new OpcoesHelmCare { PrevisaoSimuladaForcada = true }),
            _logger);
    }
}
