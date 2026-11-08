using System;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Auditoria de um acesso à ficha médica pelo Cofre. Todo acesso gera um registro, com
/// prazo de validade (<c>TTL_ACESSO_COFRE_MIN</c>), e o entregador pode consultar o histórico.
/// </summary>
public sealed class AcessoCofre : Entidade
{
    /// <summary>
    /// Tamanho máximo do IP (45 caracteres cabem um IPv6 completo).
    /// </summary>
    public const int TamanhoMaximoIp = 45;

    // Construtor usado pela fábrica Conceder e pelo EF Core.
    private AcessoCofre()
    {
    }

    /// <summary>
    /// Entregador dono da ficha acessada (campo interno da v1.3, usado na auditoria).
    /// </summary>
    public Guid EntregadorId { get; private set; }

    /// <summary>
    /// Ocorrência ativa que justificou o acesso, ou nulo na emergência manual.
    /// </summary>
    public Guid? OcorrenciaId { get; private set; }

    /// <summary>
    /// Socorrista que acessou.
    /// </summary>
    public Guid SocorristaId { get; private set; }

    /// <summary>
    /// Origem do acesso.
    /// </summary>
    public OrigemAcessoCofre Origem { get; private set; }

    /// <summary>
    /// Instante do acesso, em UTC.
    /// </summary>
    public DateTime AcessadoEm { get; private set; }

    /// <summary>
    /// Instante em que o acesso deixa de valer, em UTC.
    /// </summary>
    public DateTime ExpiraEm { get; private set; }

    /// <summary>
    /// IP de onde veio o acesso, ou nulo se não informado.
    /// </summary>
    public string? Ip { get; private set; }

    /// <summary>
    /// Concede e registra um acesso ao Cofre.
    /// </summary>
    /// <param name="entregadorId">Entregador dono da ficha.</param>
    /// <param name="ocorrenciaId">Ocorrência ativa (obrigatória em <c>SOS_ATIVO</c>, nula em <c>EMERGENCIA_MANUAL</c>).</param>
    /// <param name="socorristaId">Socorrista que acessa.</param>
    /// <param name="origem">Origem do acesso.</param>
    /// <param name="agora">Instante do acesso, em UTC.</param>
    /// <param name="ttl">Validade do acesso, maior que zero.</param>
    /// <param name="ip">IP de origem, opcional.</param>
    /// <returns>O acesso registrado.</returns>
    /// <exception cref="ValidacaoDominioException">Quando a combinação de origem e ocorrência ou algum dado é inválido.</exception>
    public static AcessoCofre Conceder(
        Guid entregadorId,
        Guid? ocorrenciaId,
        Guid socorristaId,
        OrigemAcessoCofre origem,
        DateTime agora,
        TimeSpan ttl,
        string? ip)
    {
        Garantir.Definido(origem, "origem");

        if (origem == OrigemAcessoCofre.SosAtivo && ocorrenciaId is null)
        {
            throw new ValidacaoDominioException("O acesso com origem SOS_ATIVO exige ocorrenciaId.");
        }

        if (origem == OrigemAcessoCofre.EmergenciaManual && ocorrenciaId is not null)
        {
            throw new ValidacaoDominioException("O acesso com origem EMERGENCIA_MANUAL não pode ter ocorrenciaId.");
        }

        if (ttl <= TimeSpan.Zero)
        {
            throw new ValidacaoDominioException("A validade do acesso ao Cofre deve ser maior que zero.");
        }

        string ipValidado = Garantir.TextoOpcional(ip, "ip", TamanhoMaximoIp);

        return new AcessoCofre
        {
            EntregadorId = Garantir.IdInformado(entregadorId, "entregadorId"),
            OcorrenciaId = ocorrenciaId is null ? null : Garantir.IdInformado(ocorrenciaId.Value, "ocorrenciaId"),
            SocorristaId = Garantir.IdInformado(socorristaId, "socorristaId"),
            Origem = origem,
            AcessadoEm = Garantir.Utc(agora, "acessadoEm"),
            ExpiraEm = agora + ttl,
            Ip = ipValidado.Length == 0 ? null : ipValidado,
        };
    }

    /// <summary>
    /// Indica se o acesso ainda vale. O limite é exclusivo: em <see cref="ExpiraEm"/> já expirou.
    /// </summary>
    /// <param name="agora">Instante atual, em UTC.</param>
    /// <returns><see langword="true"/> se <paramref name="agora"/> é anterior a <see cref="ExpiraEm"/>.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o instante não está em UTC.</exception>
    public bool EstaValido(DateTime agora)
    {
        return Garantir.Utc(agora, "agora") < ExpiraEm;
    }
}
