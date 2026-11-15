using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.TrechosCriticos;
using HelmCare.Api.Integracoes.ServicoRisco;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Risco;
using HelmCare.Dominio.Comum;
using Microsoft.Extensions.Logging;

namespace HelmCare.Api.Servicos.TrechosCriticos;

/// <summary>
/// Repassa a lista de trechos críticos do Serviço de Risco (seção 7: o app nunca chama o serviço
/// direto). Ao contrário do índice do turno, aqui não há versão simulada: sem o serviço, a rota
/// responde 503.
/// </summary>
public sealed partial class TrechosCriticosServico
{
    /// <summary>
    /// Limite usado quando a requisição não informa <c>limite</c>.
    /// </summary>
    public const int LimitePadrao = 10;

    /// <summary>
    /// Menor <c>limite</c> aceito.
    /// </summary>
    public const int LimiteMinimo = 1;

    /// <summary>
    /// Maior <c>limite</c> aceito.
    /// </summary>
    public const int LimiteMaximo = 50;

    private readonly ClienteServicoRisco _cliente;
    private readonly TimeProvider _relogio;
    private readonly ILogger<TrechosCriticosServico> _logger;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="cliente">Cliente HTTP do Serviço de Risco.</param>
    /// <param name="relogio">Relógio, para medir o tempo da chamada.</param>
    /// <param name="logger">Log da aplicação.</param>
    public TrechosCriticosServico(ClienteServicoRisco cliente, TimeProvider relogio, ILogger<TrechosCriticosServico> logger)
    {
        _cliente = cliente;
        _relogio = relogio;
        _logger = logger;
    }

    /// <summary>
    /// Lista os trechos com mais sinistros graves, na ordem recebida do serviço.
    /// </summary>
    /// <param name="limite">Quantidade de trechos, de 1 a 50.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O período e os trechos.</returns>
    /// <exception cref="ValidacaoDominioException"><c>limite</c> fora de 1 a 50 (400), sem chamar o serviço.</exception>
    /// <exception cref="ServicoRiscoIndisponivelException">O Serviço de Risco falhou (503).</exception>
    public async Task<TrechosCriticosResposta> ListarAsync(int limite, CancellationToken cancellationToken)
    {
        // O controller já recusa pelo [Range]; esta checagem protege quem chamar o serviço por outro caminho.
        if (limite is < LimiteMinimo or > LimiteMaximo)
        {
            throw new ValidacaoDominioException(
                $"O parâmetro limite deve ficar entre {LimiteMinimo.ToString(CultureInfo.InvariantCulture)} e {LimiteMaximo.ToString(CultureInfo.InvariantCulture)}.");
        }

        long inicioMedicao = _relogio.GetTimestamp();

        try
        {
            ListaTrechosCriticos lista = await _cliente.ListarTrechosCriticosAsync(limite, cancellationToken);
            long tempoMs = Milissegundos(inicioMedicao);
            RegistrarSucesso(_logger, tempoMs);
            return TrechosCriticosResposta.De(lista);
        }
        catch (ServicoRiscoIndisponivelException e)
        {
            // Só o motivo, o status e o tempo; o TratadorExcecoes transforma a exceção no 503.
            string status = e.StatusHttp?.ToString(CultureInfo.InvariantCulture) ?? "sem resposta";
            long tempoMs = Milissegundos(inicioMedicao);
            RegistrarFalha(_logger, e.Motivo, status, tempoMs);
            throw;
        }
    }

    private long Milissegundos(long inicioMedicao)
    {
        return (long)_relogio.GetElapsedTime(inicioMedicao).TotalMilliseconds;
    }

    [LoggerMessage(EventId = 610, Level = LogLevel.Information, Message = "Trechos críticos obtidos do Serviço de Risco em {TempoMs} ms.")]
    private static partial void RegistrarSucesso(ILogger logger, long tempoMs);

    [LoggerMessage(
        EventId = 611,
        Level = LogLevel.Warning,
        Message = "Trechos críticos indisponíveis no Serviço de Risco (motivo {Motivo}, status {Status}, {TempoMs} ms). A rota responde 503.")]
    private static partial void RegistrarFalha(ILogger logger, MotivoFalhaServicoRisco motivo, string status, long tempoMs);
}
