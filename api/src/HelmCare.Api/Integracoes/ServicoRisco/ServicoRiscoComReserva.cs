using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Risco;
using Microsoft.Extensions.Logging;

namespace HelmCare.Api.Integracoes.ServicoRisco;

/// <summary>
/// <see cref="IServicoRisco"/> usado pelo <c>TurnoServico</c> (padrão decorador): tenta o
/// <see cref="ClienteServicoRisco"/> e, se ele falhar, devolve o resultado do
/// <see cref="ServicoRiscoSimulado"/>, que segue a mesma regra e dá o mesmo índice.
/// </summary>
/// <remarks>
/// O turno nunca falha por causa do Serviço de Risco (seção 8, "Degradação"): com 5xx, erro de rede
/// ou tempo esgotado, o log leva um aviso; com 4xx ou resposta fora do formato, que indicam erro de
/// integração, o log leva um erro. Nos dois casos o turno abre com o índice da simulada. O
/// cancelamento de quem chamou não é falha do serviço e nunca cai na simulada. O log leva só o
/// motivo, o status HTTP e o tempo; nunca o corpo da requisição ou da resposta.
/// </remarks>
public sealed partial class ServicoRiscoComReserva : IServicoRisco
{
    private readonly ClienteServicoRisco _cliente;
    private readonly ServicoRiscoSimulado _simulado;
    private readonly TimeProvider _relogio;
    private readonly ILogger<ServicoRiscoComReserva> _logger;

    /// <summary>
    /// Cria o decorador.
    /// </summary>
    /// <param name="cliente">Cliente HTTP do Serviço de Risco.</param>
    /// <param name="simulado">Versão simulada, usada como reserva.</param>
    /// <param name="relogio">Relógio, para medir o tempo da chamada.</param>
    /// <param name="logger">Log da aplicação.</param>
    public ServicoRiscoComReserva(
        ClienteServicoRisco cliente,
        ServicoRiscoSimulado simulado,
        TimeProvider relogio,
        ILogger<ServicoRiscoComReserva> logger)
    {
        _cliente = cliente;
        _simulado = simulado;
        _relogio = relogio;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ResultadoRisco> CalcularAsync(
        DateTime inicio,
        DateTime fim,
        PrevisaoTurno previsao,
        CancellationToken cancellationToken)
    {
        long inicioMedicao = _relogio.GetTimestamp();

        try
        {
            ResultadoRisco resultado = await _cliente.CalcularIndiceAsync(inicio, fim, previsao, cancellationToken);
            long tempoMs = Milissegundos(inicioMedicao);
            RegistrarSucesso(_logger, tempoMs);
            return resultado;
        }
        catch (ServicoRiscoIndisponivelException e)
        {
            Registrar(e, Milissegundos(inicioMedicao));
            return await _simulado.CalcularAsync(inicio, fim, previsao, cancellationToken);
        }
    }

    private static string Status(int? statusHttp)
    {
        return statusHttp?.ToString(CultureInfo.InvariantCulture) ?? "sem resposta";
    }

    private static string Campos(IReadOnlyList<string> campos)
    {
        return campos.Count == 0 ? "nenhum" : string.Join(", ", campos);
    }

    private void Registrar(ServicoRiscoIndisponivelException e, long tempoMs)
    {
        if (e.Motivo is MotivoFalhaServicoRisco.ErroDeIntegracao or MotivoFalhaServicoRisco.RespostaInvalida)
        {
            RegistrarErroIntegracao(_logger, e.Motivo, Status(e.StatusHttp), Campos(e.CamposComErro), tempoMs);
        }
        else
        {
            RegistrarServicoIndisponivel(_logger, e.Motivo, Status(e.StatusHttp), tempoMs);
        }
    }

    private long Milissegundos(long inicioMedicao)
    {
        return (long)_relogio.GetElapsedTime(inicioMedicao).TotalMilliseconds;
    }

    [LoggerMessage(EventId = 600, Level = LogLevel.Information, Message = "Índice de Risco calculado pelo Serviço de Risco em {TempoMs} ms.")]
    private static partial void RegistrarSucesso(ILogger logger, long tempoMs);

    [LoggerMessage(
        EventId = 601,
        Level = LogLevel.Warning,
        Message = "Serviço de Risco indisponível (motivo {Motivo}, status {Status}, {TempoMs} ms). Índice de Risco calculado pela versão simulada.")]
    private static partial void RegistrarServicoIndisponivel(ILogger logger, MotivoFalhaServicoRisco motivo, string status, long tempoMs);

    [LoggerMessage(
        EventId = 602,
        Level = LogLevel.Error,
        Message = "Erro de integração com o Serviço de Risco (motivo {Motivo}, status {Status}, campos {Campos}, {TempoMs} ms). Índice de Risco calculado pela versão simulada.")]
    private static partial void RegistrarErroIntegracao(ILogger logger, MotivoFalhaServicoRisco motivo, string status, string campos, long tempoMs);
}
