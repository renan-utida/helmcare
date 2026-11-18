using System;
using System.Collections.Generic;
using System.Linq;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Janela de acesso ao Cofre de um socorrista para um entregador (seção 7 do contrato, 1.16). Abre
/// na primeira concessão e vale até o <c>expiraEm</c> (a concessão mais <c>TTL_ACESSO_COFRE_MIN</c>);
/// na origem <c>SOS_ATIVO</c>, também fecha quando a ocorrência que a abriu termina. Depois de fechar,
/// o socorrista fica bloqueado para esse entregador por mais um TTL, exceto se o entregador tiver uma
/// ocorrência nova (outro SOS), que abre uma janela nova na hora.
/// </summary>
/// <remarks>
/// A janela é calculada a partir da última concessão do par: dentro da janela, cada concessão repete o
/// <c>expiraEm</c>, a origem e a ocorrência da que a abriu, então a última concessão descreve a janela
/// atual. Sem o bloqueio, o fim do SOS levaria direto a uma emergência manual, e o acesso nunca
/// expiraria de fato.
/// </remarks>
public sealed class JanelaAcessoCofre
{
    private JanelaAcessoCofre(AcessoCofre ultimaConcessao, DateTime? fimDaOcorrencia, TimeSpan ttl)
    {
        Origem = ultimaConcessao.Origem;
        OcorrenciaId = ultimaConcessao.OcorrenciaId;
        ExpiraEm = ultimaConcessao.ExpiraEm;

        // O fim da ocorrência só fecha a janela do SOS_ATIVO, e só se vier antes do TTL.
        FechaEm = Origem == OrigemAcessoCofre.SosAtivo && fimDaOcorrencia is { } fim && fim < ExpiraEm
            ? fim
            : ExpiraEm;
        BloqueadaAte = FechaEm + ttl;
    }

    /// <summary>
    /// Origem da janela.
    /// </summary>
    public OrigemAcessoCofre Origem { get; }

    /// <summary>
    /// Ocorrência que abriu a janela, ou nulo na emergência manual.
    /// </summary>
    public Guid? OcorrenciaId { get; }

    /// <summary>
    /// Fim da janela pelo TTL (o <c>expiraEm</c> devolvido em cada concessão).
    /// </summary>
    public DateTime ExpiraEm { get; }

    /// <summary>
    /// Instante em que a janela fecha de fato: o <see cref="ExpiraEm"/> ou, antes dele, o fim da
    /// ocorrência que abriu uma janela <c>SOS_ATIVO</c>.
    /// </summary>
    public DateTime FechaEm { get; }

    /// <summary>
    /// Fim do bloqueio depois do fechamento (<see cref="FechaEm"/> mais o TTL).
    /// </summary>
    public DateTime BloqueadaAte { get; }

    /// <summary>
    /// Monta a janela do par a partir dos acessos concedidos dele.
    /// </summary>
    /// <param name="concedidosDoPar">Acessos concedidos desse socorrista para esse entregador (negados são ignorados).</param>
    /// <param name="fimDaOcorrencia">
    /// <c>encerradaEm</c> da ocorrência da última concessão, se ela já terminou (<c>ENCERRADA</c> ou
    /// <c>CANCELADA</c>); nulo se ainda está ativa ou se a janela é de emergência manual.
    /// </param>
    /// <param name="ttl"><c>TTL_ACESSO_COFRE_MIN</c>, maior que zero.</param>
    /// <returns>A janela, ou nulo se o par nunca teve acesso concedido.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o TTL não é positivo.</exception>
    public static JanelaAcessoCofre? DoPar(IEnumerable<AcessoCofre> concedidosDoPar, DateTime? fimDaOcorrencia, TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(concedidosDoPar);
        ExigirTtl(ttl);

        AcessoCofre? ultima = concedidosDoPar
            .Where(a => a.Concedido)
            .OrderByDescending(a => a.AcessadoEm)
            .FirstOrDefault();

        return ultima is null ? null : new JanelaAcessoCofre(ultima, fimDaOcorrencia, ttl);
    }

    /// <summary>
    /// Decide uma tentativa de acesso, na ordem do contrato: socorrista não verificado é negado; dentro
    /// da janela, concede com o mesmo prazo e a mesma origem; janela fechada e ainda no bloqueio (sem
    /// ocorrência nova), nega como expirado; senão, abre uma janela nova (<c>SOS_ATIVO</c> com ocorrência
    /// ativa, <c>EMERGENCIA_MANUAL</c> sem ela).
    /// </summary>
    /// <param name="janela">Janela atual do par, ou nulo se nunca houve concessão.</param>
    /// <param name="socorristaVerificado">Se o socorrista é verificado.</param>
    /// <param name="ocorrenciaAtivaId">Ocorrência ativa do entregador agora, ou nulo.</param>
    /// <param name="agora">Instante da tentativa, em UTC.</param>
    /// <param name="ttl"><c>TTL_ACESSO_COFRE_MIN</c>, maior que zero.</param>
    /// <returns>A decisão, pronta para o <see cref="AcessoCofre.Registrar"/>.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o instante não está em UTC ou o TTL não é positivo.</exception>
    public static DecisaoAcessoCofre Decidir(
        JanelaAcessoCofre? janela,
        bool socorristaVerificado,
        Guid? ocorrenciaAtivaId,
        DateTime agora,
        TimeSpan ttl)
    {
        Garantir.Utc(agora, "agora");
        ExigirTtl(ttl);

        OrigemAcessoCofre origemAgora = ocorrenciaAtivaId is null ? OrigemAcessoCofre.EmergenciaManual : OrigemAcessoCofre.SosAtivo;

        if (!socorristaVerificado)
        {
            return DecisaoAcessoCofre.Negar(MotivoNegacaoCofre.SocorristaNaoVerificado, origemAgora, ocorrenciaAtivaId, agora);
        }

        if (janela is not null && janela.EstaAberta(agora))
        {
            return DecisaoAcessoCofre.Conceder(janela.Origem, janela.OcorrenciaId, janela.ExpiraEm);
        }

        if (janela is not null && janela.BloqueiaNovaJanela(agora, ocorrenciaAtivaId))
        {
            return DecisaoAcessoCofre.Negar(MotivoNegacaoCofre.AcessoExpirado, origemAgora, ocorrenciaAtivaId, janela.FechaEm);
        }

        return DecisaoAcessoCofre.Conceder(origemAgora, ocorrenciaAtivaId, agora + ttl);
    }

    /// <summary>
    /// Diz se a janela ainda está aberta.
    /// </summary>
    /// <param name="agora">Instante, em UTC.</param>
    /// <returns><see langword="true"/> antes do <see cref="FechaEm"/>.</returns>
    public bool EstaAberta(DateTime agora)
    {
        return agora < FechaEm;
    }

    /// <summary>
    /// Diz se a janela, já fechada, ainda impede uma janela nova: até o <see cref="BloqueadaAte"/>,
    /// a menos que o entregador tenha uma ocorrência ativa diferente da que abriu esta janela.
    /// </summary>
    /// <param name="agora">Instante, em UTC.</param>
    /// <param name="ocorrenciaAtivaId">Ocorrência ativa do entregador agora, ou nulo.</param>
    /// <returns><see langword="true"/> se a tentativa deve ser negada como expirada.</returns>
    public bool BloqueiaNovaJanela(DateTime agora, Guid? ocorrenciaAtivaId)
    {
        bool ocorrenciaNova = ocorrenciaAtivaId is { } ativa && ativa != OcorrenciaId;
        return !EstaAberta(agora) && agora < BloqueadaAte && !ocorrenciaNova;
    }

    private static void ExigirTtl(TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ValidacaoDominioException("A validade do acesso ao Cofre deve ser maior que zero.");
        }
    }
}
