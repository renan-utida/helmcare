using System;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Auditoria de uma tentativa de acesso à ficha médica pelo Cofre (seção 3 do contrato). Toda
/// tentativa de um socorrista autenticado, com um <c>qrToken</c> existente, gera um registro,
/// concedido ou negado, e o entregador pode consultar o histórico.
/// </summary>
public sealed class AcessoCofre : Entidade
{
    /// <summary>
    /// Tamanho máximo do IP (45 caracteres cabem um IPv6 completo).
    /// </summary>
    public const int TamanhoMaximoIp = 45;

    // Construtor usado pelas fábricas e pelo EF Core.
    private AcessoCofre()
    {
    }

    /// <summary>
    /// Entregador dono da ficha acessada (campo interno da v1.3, usado na auditoria).
    /// </summary>
    public Guid EntregadorId { get; private set; }

    /// <summary>
    /// Ocorrência ativa que justificou o acesso (ou que estava ativa na negação), ou nulo na
    /// emergência manual.
    /// </summary>
    public Guid? OcorrenciaId { get; private set; }

    /// <summary>
    /// Socorrista que tentou o acesso.
    /// </summary>
    public Guid SocorristaId { get; private set; }

    /// <summary>
    /// Origem do acesso (na negação, a que valeria naquele momento).
    /// </summary>
    public OrigemAcessoCofre Origem { get; private set; }

    /// <summary>
    /// Verdadeiro quando a ficha foi liberada.
    /// </summary>
    public bool Concedido { get; private set; }

    /// <summary>
    /// Motivo da negação, ou nulo quando o acesso foi concedido.
    /// </summary>
    public MotivoNegacaoCofre? Motivo { get; private set; }

    /// <summary>
    /// Instante da tentativa, em UTC.
    /// </summary>
    public DateTime AcessadoEm { get; private set; }

    /// <summary>
    /// Fim da janela de acesso. Numa concessão, o da janela (a primeira concessão mais o TTL); no
    /// <c>ACESSO_EXPIRADO</c>, o instante em que a janela fechou; no <c>SOCORRISTA_NAO_VERIFICADO</c>,
    /// igual ao <see cref="AcessadoEm"/>.
    /// </summary>
    public DateTime ExpiraEm { get; private set; }

    /// <summary>
    /// IP de origem da tentativa (depois dos cabeçalhos encaminhados). Fica só no banco: nenhuma
    /// resposta da API o devolve.
    /// </summary>
    public string? Ip { get; private set; }

    /// <summary>
    /// Registra um acesso concedido que abre uma janela nova, válida por <paramref name="ttl"/>.
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
        if (ttl <= TimeSpan.Zero)
        {
            throw new ValidacaoDominioException("A validade do acesso ao Cofre deve ser maior que zero.");
        }

        var decisao = DecisaoAcessoCofre.Conceder(origem, ocorrenciaId, Garantir.Utc(agora, "acessadoEm") + ttl);
        return Registrar(entregadorId, socorristaId, decisao, agora, ip);
    }

    /// <summary>
    /// Registra a tentativa conforme a decisão da <see cref="JanelaAcessoCofre"/>: concedida (com o
    /// <c>expiraEm</c> da janela) ou negada (com o motivo).
    /// </summary>
    /// <param name="entregadorId">Entregador dono da ficha.</param>
    /// <param name="socorristaId">Socorrista que tentou.</param>
    /// <param name="decisao">Decisão da janela de acesso.</param>
    /// <param name="agora">Instante da tentativa, em UTC.</param>
    /// <param name="ip">IP de origem, opcional.</param>
    /// <returns>O registro de auditoria.</returns>
    /// <exception cref="ValidacaoDominioException">Quando a combinação de origem, ocorrência e prazo é inválida.</exception>
    public static AcessoCofre Registrar(
        Guid entregadorId,
        Guid socorristaId,
        DecisaoAcessoCofre decisao,
        DateTime agora,
        string? ip)
    {
        ArgumentNullException.ThrowIfNull(decisao);
        Garantir.Definido(decisao.Origem, "origem");
        Garantir.Utc(agora, "acessadoEm");

        if (decisao.Origem == OrigemAcessoCofre.SosAtivo && decisao.OcorrenciaId is null)
        {
            throw new ValidacaoDominioException("O acesso com origem SOS_ATIVO exige ocorrenciaId.");
        }

        if (decisao.Origem == OrigemAcessoCofre.EmergenciaManual && decisao.OcorrenciaId is not null)
        {
            throw new ValidacaoDominioException("O acesso com origem EMERGENCIA_MANUAL não pode ter ocorrenciaId.");
        }

        // Concedido vale até depois de agora; negado já venceu (expirado) ou nunca valeu (não verificado).
        if (decisao.Concedido ? decisao.ExpiraEm <= agora : decisao.ExpiraEm > agora)
        {
            throw new ValidacaoDominioException("O expiraEm não combina com o resultado do acesso ao Cofre.");
        }

        string ipValidado = Garantir.TextoOpcional(ip, "ip", TamanhoMaximoIp);

        return new AcessoCofre
        {
            EntregadorId = Garantir.IdInformado(entregadorId, "entregadorId"),
            OcorrenciaId = decisao.OcorrenciaId is null ? null : Garantir.IdInformado(decisao.OcorrenciaId.Value, "ocorrenciaId"),
            SocorristaId = Garantir.IdInformado(socorristaId, "socorristaId"),
            Origem = decisao.Origem,
            Concedido = decisao.Concedido,
            Motivo = decisao.Motivo,
            AcessadoEm = agora,
            ExpiraEm = decisao.ExpiraEm,
            Ip = ipValidado.Length == 0 ? null : ipValidado,
        };
    }

    /// <summary>
    /// Diz se o acesso concedido ainda vale pelo prazo (não considera o fim da ocorrência, que é da
    /// <see cref="JanelaAcessoCofre"/>). Uma negação nunca vale.
    /// </summary>
    /// <param name="agora">Instante atual, em UTC.</param>
    /// <returns><see langword="true"/> enquanto o prazo não venceu.</returns>
    public bool EstaValido(DateTime agora)
    {
        return Concedido && Garantir.Utc(agora, "agora") < ExpiraEm;
    }
}
