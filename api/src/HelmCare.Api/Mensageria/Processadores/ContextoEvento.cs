using System;
using HelmCare.Api.Mensageria.Mensagens;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Mensageria.Processadores;

/// <summary>
/// O que um processador recebe: o capacete (já carregado e rastreado pelo EF Core) e o evento lido.
/// </summary>
public sealed class ContextoEvento
{
    /// <summary>
    /// Cria o contexto.
    /// </summary>
    /// <param name="dispositivo">Capacete que publicou o evento.</param>
    /// <param name="evento">Evento lido do payload.</param>
    public ContextoEvento(Dispositivo dispositivo, EventoMensagem evento)
    {
        ArgumentNullException.ThrowIfNull(dispositivo);
        ArgumentNullException.ThrowIfNull(evento);

        Dispositivo = dispositivo;
        Evento = evento;
    }

    /// <summary>
    /// Capacete que publicou o evento.
    /// </summary>
    public Dispositivo Dispositivo { get; }

    /// <summary>
    /// Evento lido do payload.
    /// </summary>
    public EventoMensagem Evento { get; }

    /// <summary>
    /// Preenchida pelo processador do <c>SOS</c> quando ele abriu uma ocorrência nesta mensagem; nula
    /// nos outros casos. O roteador usa para avisar o contato de emergência depois de gravar.
    /// </summary>
    public AberturaOcorrencia? OcorrenciaAberta { get; private set; }

    /// <summary>
    /// Informa que esta mensagem abriu uma ocorrência.
    /// </summary>
    /// <param name="ocorrenciaId">Ocorrência aberta (ainda não gravada).</param>
    /// <param name="quantidadeContatos">Quantos contatos de emergência o entregador tem.</param>
    public void InformarOcorrenciaAberta(Guid ocorrenciaId, int quantidadeContatos)
    {
        OcorrenciaAberta = new AberturaOcorrencia(ocorrenciaId, quantidadeContatos);
    }
}

/// <summary>
/// Ocorrência aberta por uma mensagem <c>SOS</c>, com o que o aviso ao contato precisa (nenhum dado pessoal).
/// </summary>
/// <param name="OcorrenciaId">Ocorrência aberta.</param>
/// <param name="QuantidadeContatos">Quantos contatos de emergência o entregador tem.</param>
public sealed record AberturaOcorrencia(Guid OcorrenciaId, int QuantidadeContatos);
