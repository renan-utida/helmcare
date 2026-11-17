using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelmCare.Api.Servicos.Ocorrencias;

/// <summary>
/// Serviço em segundo plano que escala para a camada 2, sem depender de nenhuma requisição, as
/// ocorrências sem aceite no tempo da camada 1, e encerra as que passaram de
/// <c>TEMPO_ENCERRAMENTO_AUTOMATICO_MIN</c>. Roda o <see cref="EscalonadorOcorrencias"/> e o
/// <see cref="EncerradorAutomaticoOcorrencias"/> uma vez na subida (assim, uma ocorrência antiga escala
/// logo) e depois a cada <c>IntervaloEscalonamentoS</c>.
/// </summary>
/// <remarks>
/// <para>Com o intervalo de 1 s, a ocorrência escala até 1 s depois dos 60 s
/// (<c>TEMPO_ACEITE_CAMADA_1_S</c>), como diz a seção 7 do contrato.</para>
/// <para>O <see cref="PeriodicTimer"/> é criado com o <see cref="TimeProvider"/> injetado, então os
/// testes avançam o <c>FakeTimeProvider</c> em vez de esperar. Cada rodada abre o próprio escopo de DI
/// (um <c>AppDbContext</c> novo), e nenhuma falha de uma rodada derruba o laço: vira log.</para>
/// </remarks>
public sealed partial class ServicoEscalonamentoSos : BackgroundService
{
    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;
    private readonly TimeSpan _intervalo;
    private readonly ILogger<ServicoEscalonamentoSos> _logger;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="escopos">Fábrica de escopos de DI (um por rodada).</param>
    /// <param name="relogio">Relógio do <see cref="PeriodicTimer"/>.</param>
    /// <param name="opcoes">Intervalo entre as rodadas (<c>IntervaloEscalonamentoS</c>).</param>
    /// <param name="logger">Log da aplicação.</param>
    public ServicoEscalonamentoSos(
        IServiceScopeFactory escopos,
        TimeProvider relogio,
        IOptions<OpcoesHelmCare> opcoes,
        ILogger<ServicoEscalonamentoSos> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _escopos = escopos;
        _relogio = relogio;
        _intervalo = TimeSpan.FromSeconds(opcoes.Value.IntervaloEscalonamentoS);
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo, _relogio);

        try
        {
            // Primeira rodada já na subida; depois, uma por tick.
            do
            {
                await RodarAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Parada normal da aplicação.
        }
    }

    private async Task RodarAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using AsyncServiceScope escopo = _escopos.CreateAsyncScope();
            EscalonadorOcorrencias escalonador = escopo.ServiceProvider.GetRequiredService<EscalonadorOcorrencias>();
            await escalonador.EscalarVencidasAsync(stoppingToken);

            // Mesmo laço, mesma rodada: depois de escalar, encerra as que passaram do tempo (1.15).
            EncerradorAutomaticoOcorrencias encerrador =
                escopo.ServiceProvider.GetRequiredService<EncerradorAutomaticoOcorrencias>();
            await encerrador.EncerrarVencidasAsync(stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // Ex.: banco fora do ar. A próxima rodada tenta de novo.
            RegistrarFalha(_logger, ex.GetType().Name);
        }
    }

    [LoggerMessage(EventId = 725, Level = LogLevel.Error, Message = "Falha {TipoExcecao} na rodada do escalonamento (camada 2 e encerramento automático); nova tentativa na próxima rodada.")]
    private static partial void RegistrarFalha(ILogger logger, string tipoExcecao);
}
