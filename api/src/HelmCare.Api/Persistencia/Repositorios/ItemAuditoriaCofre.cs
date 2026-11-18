using System;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Uma linha da auditoria do Cofre, já com o socorrista, sem o <c>ip</c> e sem dados de saúde.
/// </summary>
/// <param name="AcessadoEm">Instante da tentativa.</param>
/// <param name="ExpiraEm">Fim da janela (ver <see cref="Dominio.Entidades.AcessoCofre.ExpiraEm"/>).</param>
/// <param name="Origem">Origem do acesso.</param>
/// <param name="Concedido">Se a ficha foi liberada.</param>
/// <param name="Motivo">Motivo da negação, ou nulo.</param>
/// <param name="SocorristaNome">Nome do socorrista.</param>
/// <param name="SocorristaOrganizacao">Organização do socorrista.</param>
public sealed record ItemAuditoriaCofre(
    DateTime AcessadoEm,
    DateTime ExpiraEm,
    OrigemAcessoCofre Origem,
    bool Concedido,
    MotivoNegacaoCofre? Motivo,
    string SocorristaNome,
    string SocorristaOrganizacao);
