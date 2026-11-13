using System;
using System.Diagnostics.CodeAnalysis;

namespace HelmCare.Api.Mensageria;

/// <summary>
/// Monta e interpreta os tópicos da seção 6 do contrato: <c>{raiz}/{codigoDispositivo}/{canal}</c>.
/// A raiz vem de <c>OpcoesMqtt.RaizTopicos</c> (<c>helmcare-rdu/v1</c>).
/// </summary>
public static class Topicos
{
    private const string Telemetria = "telemetria";
    private const string Evento = "evento";
    private const string Comando = "comando";

    /// <summary>
    /// Filtro que recebe a telemetria de todos os capacetes: <c>{raiz}/+/telemetria</c>.
    /// </summary>
    /// <param name="raiz">Raiz dos tópicos.</param>
    /// <returns>O filtro de assinatura.</returns>
    public static string FiltroTelemetria(string raiz)
    {
        return $"{raiz}/+/{Telemetria}";
    }

    /// <summary>
    /// Filtro que recebe os eventos de todos os capacetes: <c>{raiz}/+/evento</c>.
    /// </summary>
    /// <param name="raiz">Raiz dos tópicos.</param>
    /// <returns>O filtro de assinatura.</returns>
    public static string FiltroEvento(string raiz)
    {
        return $"{raiz}/+/{Evento}";
    }

    /// <summary>
    /// Tópico de comando de um capacete: <c>{raiz}/{codigo}/comando</c> (seção 6.3).
    /// </summary>
    /// <param name="raiz">Raiz dos tópicos.</param>
    /// <param name="codigoDispositivo">Código do capacete (ex.: <c>HC-0001</c>).</param>
    /// <returns>O tópico de publicação.</returns>
    public static string ComandoDe(string raiz, string codigoDispositivo)
    {
        return $"{raiz}/{codigoDispositivo}/{Comando}";
    }

    /// <summary>
    /// Extrai o código do capacete e o canal de um tópico recebido.
    /// </summary>
    /// <param name="raiz">Raiz dos tópicos.</param>
    /// <param name="topico">Tópico recebido.</param>
    /// <param name="codigoDispositivo">Código do capacete, quando o tópico é válido.</param>
    /// <param name="canal">Canal, quando o tópico é válido.</param>
    /// <returns><see langword="true"/> se o tópico é <c>{raiz}/{codigo}/telemetria</c> ou <c>{raiz}/{codigo}/evento</c>.</returns>
    public static bool TentarInterpretar(
        string raiz,
        string? topico,
        [NotNullWhen(true)] out string? codigoDispositivo,
        out CanalMqtt canal)
    {
        ArgumentNullException.ThrowIfNull(raiz);

        codigoDispositivo = null;
        canal = default;
        string prefixo = raiz + "/";

        if (topico is null || !topico.StartsWith(prefixo, StringComparison.Ordinal))
        {
            return false;
        }

        string[] partes = topico[prefixo.Length..].Split('/');

        if (partes.Length != 2 || partes[0].Length == 0 || partes[0].AsSpan().ContainsAny('+', '#'))
        {
            return false;
        }

        switch (partes[1])
        {
            case Telemetria:
                canal = CanalMqtt.Telemetria;
                break;
            case Evento:
                canal = CanalMqtt.Evento;
                break;
            default:
                return false;
        }

        codigoDispositivo = partes[0];
        return true;
    }
}
