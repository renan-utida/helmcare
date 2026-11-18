using System;
using System.Collections.Generic;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Contratos.Cofre;

/// <summary>
/// Corpo do <c>200</c> do <c>POST /api/v1/cofre/{qrToken}/acesso</c> (seção 7 do contrato, 1.16): o
/// mínimo da Ficha e os contatos para ligar. Nunca traz os <c>medicamentos</c>, nem o nome, o CPF ou a
/// CNH do entregador, nem o <c>qrToken</c>.
/// </summary>
public sealed class AcessoCofreResposta
{
    /// <summary>
    /// Origem do acesso: <c>SOS_ATIVO</c> ou <c>EMERGENCIA_MANUAL</c>.
    /// </summary>
    public required OrigemAcessoCofre Origem { get; init; }

    /// <summary>
    /// Ocorrência da janela, ou nulo na emergência manual.
    /// </summary>
    public required Guid? OcorrenciaId { get; init; }

    /// <summary>
    /// Instante deste acesso, em UTC.
    /// </summary>
    public required DateTime AcessadoEm { get; init; }

    /// <summary>
    /// Fim da janela de acesso, em UTC. Repetir o pedido não estica o prazo.
    /// </summary>
    public required DateTime ExpiraEm { get; init; }

    /// <summary>
    /// Tipo sanguíneo, ou nulo sem ficha médica.
    /// </summary>
    public required string? TipoSanguineo { get; init; }

    /// <summary>
    /// Alergias, ou nulo sem ficha médica.
    /// </summary>
    public required string? Alergias { get; init; }

    /// <summary>
    /// Condições críticas, ou nulo sem ficha médica.
    /// </summary>
    public required string? CondicoesCriticas { get; init; }

    /// <summary>
    /// Contatos de emergência, com o telefone completo para a ligação.
    /// </summary>
    public required IReadOnlyList<ContatoCofreResposta> Contatos { get; init; }
}
