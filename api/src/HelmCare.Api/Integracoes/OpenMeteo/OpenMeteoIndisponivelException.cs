using System;

namespace HelmCare.Api.Integracoes.OpenMeteo;

/// <summary>
/// A Open-Meteo não deu uma previsão utilizável. No <c>POST /turnos</c>, vira a previsão simulada; no
/// <c>GET /clima</c> sem cache válido, vira o <c>503</c>. A mensagem nunca leva a coordenada nem o corpo.
/// </summary>
public sealed class OpenMeteoIndisponivelException : Exception
{
    /// <summary>
    /// Cria a exceção sem motivo específico (falha).
    /// </summary>
    public OpenMeteoIndisponivelException()
        : this(MotivoFalhaOpenMeteo.Falha, "A Open-Meteo não respondeu.")
    {
    }

    /// <summary>
    /// Cria a exceção com uma mensagem (motivo falha).
    /// </summary>
    /// <param name="message">Mensagem interna.</param>
    public OpenMeteoIndisponivelException(string message)
        : this(MotivoFalhaOpenMeteo.Falha, message)
    {
    }

    /// <summary>
    /// Cria a exceção com uma mensagem e a causa (motivo falha).
    /// </summary>
    /// <param name="message">Mensagem interna.</param>
    /// <param name="innerException">Exceção original.</param>
    public OpenMeteoIndisponivelException(string message, Exception innerException)
        : this(MotivoFalhaOpenMeteo.Falha, message, innerException)
    {
    }

    /// <summary>
    /// Cria a exceção com o motivo.
    /// </summary>
    /// <param name="motivo">Causa da falha.</param>
    /// <param name="message">Mensagem interna, sem coordenada nem corpo.</param>
    /// <param name="innerException">Exceção original, se houver.</param>
    public OpenMeteoIndisponivelException(MotivoFalhaOpenMeteo motivo, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Motivo = motivo;
    }

    /// <summary>
    /// Causa da falha.
    /// </summary>
    public MotivoFalhaOpenMeteo Motivo { get; }
}
