using System;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Contratos.Cofre;

/// <summary>
/// Uma tentativa de acesso ao Cofre, no <c>GET /api/v1/cofre/auditoria/{entregadorId}</c>. Sem o
/// <c>ip</c> e sem dados de saúde.
/// </summary>
public sealed class ItemAuditoriaCofreResposta
{
    /// <summary>
    /// Instante da tentativa, em UTC.
    /// </summary>
    public required DateTime AcessadoEm { get; init; }

    /// <summary>
    /// Fim da janela (concedido), fechamento da janela (expirado) ou o próprio instante (não verificado).
    /// </summary>
    public required DateTime ExpiraEm { get; init; }

    /// <summary>
    /// Origem do acesso.
    /// </summary>
    public required OrigemAcessoCofre Origem { get; init; }

    /// <summary>
    /// Se a ficha foi liberada.
    /// </summary>
    public required bool Concedido { get; init; }

    /// <summary>
    /// Motivo da negação, ou nulo quando concedido.
    /// </summary>
    public required MotivoNegacaoCofre? Motivo { get; init; }

    /// <summary>
    /// Socorrista que tentou o acesso.
    /// </summary>
    public required SocorristaAuditoriaResposta Socorrista { get; init; }
}
