using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Integracoes.ServicoRisco.Dtos;

/// <summary>
/// Resposta 200 do <c>POST /risco/v1/indice-turno</c>. Os membros <c>required</c> fazem o
/// System.Text.Json recusar uma resposta sem algum campo.
/// </summary>
internal sealed class IndiceTurnoServicoRiscoResposta
{
    /// <summary>
    /// Índice, de 0 a 100.
    /// </summary>
    public required int IndiceRisco { get; init; }

    /// <summary>
    /// Faixa do índice, no nome do contrato (<c>ALTO</c>).
    /// </summary>
    public required FaixaRisco FaixaRisco { get; init; }

    /// <summary>
    /// Os três componentes, de 0 a 100.
    /// </summary>
    public required ComponentesServicoRiscoResposta? Componentes { get; init; }

    /// <summary>
    /// Explicação para o entregador.
    /// </summary>
    public required string? Explicacao { get; init; }
}
