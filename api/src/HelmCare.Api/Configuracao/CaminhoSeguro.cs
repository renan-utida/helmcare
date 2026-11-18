using System.Text.RegularExpressions;

namespace HelmCare.Api.Configuracao;

/// <summary>
/// Tira o <c>qrToken</c> do caminho antes de ele sair num ProblemDetails (campo <c>instance</c>) ou em
/// qualquer outra saída. O <c>qrToken</c> é credencial (seção 9 do contrato): só aparece no QR Code, no
/// endereço da página e na resposta do <c>POST /dispositivos</c>.
/// </summary>
public static partial class CaminhoSeguro
{
    /// <summary>
    /// Texto que substitui o <c>qrToken</c>.
    /// </summary>
    public const string Marcador = "{qrToken}";

    /// <summary>
    /// Troca o segmento do <c>qrToken</c> de <c>/cofre/{qrToken}</c> e de
    /// <c>/api/v1/cofre/{qrToken}/...</c> pelo marcador. Os outros caminhos (inclusive
    /// <c>/cofre/assets/...</c> e <c>/api/v1/cofre/auditoria/...</c>) ficam como estão.
    /// </summary>
    /// <param name="caminho">Caminho da requisição.</param>
    /// <returns>O caminho sem o <c>qrToken</c>.</returns>
    public static string? SemQrToken(string? caminho)
    {
        return caminho is null ? null : SegmentoDoQrToken().Replace(caminho, "$1" + Marcador);
    }

    [GeneratedRegex("^((?:/api/v1)?/cofre/)(?!assets(?:/|$))(?!auditoria(?:/|$))[^/]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SegmentoDoQrToken();
}
