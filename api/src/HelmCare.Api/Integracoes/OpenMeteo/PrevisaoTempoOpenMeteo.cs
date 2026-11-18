using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Servicos.Risco;

namespace HelmCare.Api.Integracoes.OpenMeteo;

/// <summary>
/// Previsão do turno pela Open-Meteo: as horas cheias UTC que se sobrepõem ao turno (as mesmas do
/// Serviço de Risco, seção 8.1), agregadas pelas <see cref="RegrasPrevisao"/>. Não tem reserva: quem
/// usa é o <see cref="PrevisaoTempoComReserva"/>.
/// </summary>
public sealed class PrevisaoTempoOpenMeteo : IPrevisaoTempo
{
    private readonly ClienteOpenMeteo _cliente;

    /// <summary>
    /// Cria o provedor.
    /// </summary>
    /// <param name="cliente">Cliente da Open-Meteo.</param>
    public PrevisaoTempoOpenMeteo(ClienteOpenMeteo cliente)
    {
        _cliente = cliente;
    }

    /// <inheritdoc/>
    /// <exception cref="OpenMeteoIndisponivelException">Falha, tempo esgotado, resposta inválida ou horas não cobertas.</exception>
    public async Task<PrevisaoTurno> ObterAsync(DateTime inicio, DateTime fim, CancellationToken cancellationToken)
    {
        List<DateTime> horasDoTurno = ServicoRiscoSimulado.HorasDoTurno(inicio, fim).Select(h => h.InicioUtc).ToList();

        IReadOnlyList<CondicaoTempo> resposta = await _cliente.ObterHorariaAsync(
            DateOnly.FromDateTime(horasDoTurno[0]),
            DateOnly.FromDateTime(horasDoTurno[^1]),
            cancellationToken);

        var porHora = new Dictionary<DateTime, CondicaoTempo>();
        foreach (CondicaoTempo hora in resposta)
        {
            porHora.TryAdd(hora.InstanteUtc, hora);
        }

        var doTurno = new List<CondicaoTempo>(horasDoTurno.Count);
        foreach (DateTime hora in horasDoTurno)
        {
            if (!porHora.TryGetValue(hora, out CondicaoTempo? condicao))
            {
                throw new OpenMeteoIndisponivelException(MotivoFalhaOpenMeteo.HorasNaoCobertas, "A Open-Meteo não cobriu todas as horas do turno.");
            }

            doTurno.Add(condicao);
        }

        return RegrasPrevisao.Agregar(doTurno);
    }
}
