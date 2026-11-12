using System;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Contratos.Dispositivos;

/// <summary>
/// Capacete vinculado, devolvido pelo <c>POST /api/v1/dispositivos</c>.
/// </summary>
/// <remarks>
/// O <see cref="QrToken"/> só sai nesta resposta, uma única vez, para imprimir o QR Code do
/// capacete. Nenhuma outra rota o devolve: quem tem o token abre a página do Cofre.
/// </remarks>
public sealed class DispositivoResposta
{
    /// <summary>
    /// Id do dispositivo.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Código do capacete (ex.: <c>HC-0001</c>).
    /// </summary>
    public required string Codigo { get; init; }

    /// <summary>
    /// Id do entregador dono do capacete.
    /// </summary>
    public required Guid EntregadorId { get; init; }

    /// <summary>
    /// Token que vai no QR Code do capacete (segredo; só sai nesta resposta).
    /// </summary>
    public required string QrToken { get; init; }

    /// <summary>
    /// Caminho da página do Cofre que o QR Code abre: <c>/cofre/{qrToken}</c>.
    /// </summary>
    public required string UrlCofre { get; init; }

    /// <summary>
    /// Monta a resposta a partir da entidade.
    /// </summary>
    /// <param name="dispositivo">Dispositivo gravado.</param>
    /// <returns>A resposta com o token e a URL do Cofre.</returns>
    public static DispositivoResposta De(Dispositivo dispositivo)
    {
        ArgumentNullException.ThrowIfNull(dispositivo);

        return new DispositivoResposta
        {
            Id = dispositivo.Id,
            Codigo = dispositivo.Codigo,
            EntregadorId = dispositivo.EntregadorId,
            QrToken = dispositivo.QrToken,
            UrlCofre = $"/cofre/{dispositivo.QrToken}",
        };
    }
}
