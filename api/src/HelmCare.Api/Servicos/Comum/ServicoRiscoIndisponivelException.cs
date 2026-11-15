using System;
using System.Collections.Generic;

namespace HelmCare.Api.Servicos.Comum;

/// <summary>
/// A chamada ao Serviço de Risco não deu um resultado válido. No <c>POST /turnos</c>, o índice
/// passa a vir da versão simulada; no <c>GET /trechos-criticos</c>, vira <b>503</b> no
/// <c>TratadorExcecoes</c>.
/// </summary>
/// <remarks>
/// A mensagem, o motivo, o status e os nomes dos campos vão só para o log. Nada aqui leva o corpo
/// da requisição ou da resposta.
/// </remarks>
public sealed class ServicoRiscoIndisponivelException : Exception
{
    /// <summary>
    /// Cria a exceção sem mensagem específica, com o motivo <see cref="MotivoFalhaServicoRisco.ServicoIndisponivel"/>.
    /// </summary>
    public ServicoRiscoIndisponivelException()
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem, com o motivo <see cref="MotivoFalhaServicoRisco.ServicoIndisponivel"/>.
    /// </summary>
    /// <param name="message">Mensagem, só para o log.</param>
    public ServicoRiscoIndisponivelException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Cria a exceção com a mensagem e a exceção que a causou, com o motivo <see cref="MotivoFalhaServicoRisco.ServicoIndisponivel"/>.
    /// </summary>
    /// <param name="message">Mensagem, só para o log.</param>
    /// <param name="innerException">Exceção original.</param>
    public ServicoRiscoIndisponivelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Cria a exceção com o motivo e o status HTTP, quando houve resposta.
    /// </summary>
    /// <param name="motivo">Por que a chamada falhou.</param>
    /// <param name="statusHttp">Status da resposta, ou nulo quando não houve resposta.</param>
    /// <param name="message">Mensagem, só para o log.</param>
    /// <param name="innerException">Exceção original, se houver.</param>
    /// <param name="camposComErro">Nomes dos campos do <c>errors</c> do ProblemDetails de um 4xx (nunca os valores).</param>
    public ServicoRiscoIndisponivelException(
        MotivoFalhaServicoRisco motivo,
        int? statusHttp,
        string message,
        Exception? innerException = null,
        IReadOnlyList<string>? camposComErro = null)
        : base(message, innerException)
    {
        Motivo = motivo;
        StatusHttp = statusHttp;
        CamposComErro = camposComErro ?? [];
    }

    /// <summary>
    /// Por que a chamada falhou.
    /// </summary>
    public MotivoFalhaServicoRisco Motivo { get; } = MotivoFalhaServicoRisco.ServicoIndisponivel;

    /// <summary>
    /// Status HTTP da resposta, ou nulo quando não houve resposta (erro de rede ou tempo esgotado).
    /// </summary>
    public int? StatusHttp { get; }

    /// <summary>
    /// Nomes dos campos recusados num 4xx, para o log apontar o problema sem repetir os valores.
    /// </summary>
    public IReadOnlyList<string> CamposComErro { get; } = [];
}
