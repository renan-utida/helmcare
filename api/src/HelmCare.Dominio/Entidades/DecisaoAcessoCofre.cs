using System;
using HelmCare.Dominio.Enums;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Resultado de uma tentativa de acesso ao Cofre, calculado pela <see cref="JanelaAcessoCofre"/> e
/// gravado no <see cref="AcessoCofre"/>.
/// </summary>
/// <param name="Concedido">Se a ficha é liberada.</param>
/// <param name="Motivo">Motivo da negação, ou nulo quando concedido.</param>
/// <param name="Origem">Origem do acesso (na negação, a que valeria naquele momento).</param>
/// <param name="OcorrenciaId">Ocorrência da janela, ou a ativa na negação; nulo na emergência manual.</param>
/// <param name="ExpiraEm">Fim da janela (concedido), fechamento da janela (expirado) ou o próprio instante (não verificado).</param>
public sealed record DecisaoAcessoCofre(
    bool Concedido,
    MotivoNegacaoCofre? Motivo,
    OrigemAcessoCofre Origem,
    Guid? OcorrenciaId,
    DateTime ExpiraEm)
{
    /// <summary>
    /// Acesso concedido.
    /// </summary>
    /// <param name="origem">Origem.</param>
    /// <param name="ocorrenciaId">Ocorrência (nula na emergência manual).</param>
    /// <param name="expiraEm">Fim da janela.</param>
    /// <returns>A decisão.</returns>
    public static DecisaoAcessoCofre Conceder(OrigemAcessoCofre origem, Guid? ocorrenciaId, DateTime expiraEm)
    {
        return new DecisaoAcessoCofre(true, null, origem, ocorrenciaId, expiraEm);
    }

    /// <summary>
    /// Acesso negado.
    /// </summary>
    /// <param name="motivo">Motivo.</param>
    /// <param name="origem">Origem que valeria naquele momento.</param>
    /// <param name="ocorrenciaId">Ocorrência ativa, se houver.</param>
    /// <param name="expiraEm">Fechamento da janela ou o próprio instante.</param>
    /// <returns>A decisão.</returns>
    public static DecisaoAcessoCofre Negar(MotivoNegacaoCofre motivo, OrigemAcessoCofre origem, Guid? ocorrenciaId, DateTime expiraEm)
    {
        return new DecisaoAcessoCofre(false, motivo, origem, ocorrenciaId, expiraEm);
    }
}
