using System;
using System.Buffers;
using System.Globalization;
using System.Text.Json;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Mensageria.Mensagens;

/// <summary>
/// Comando da API para o capacete (seção 6.3 do contrato):
/// <c>{ "tipo": "SOCORRO_A_CAMINHO", "ts": "2026-11-12T21:16:40Z" }</c>.
/// </summary>
public sealed class ComandoMensagem
{
    /// <summary>
    /// <c>tipo</c>: comando.
    /// </summary>
    public required TipoComandoMqtt Tipo { get; init; }

    /// <summary>
    /// <c>ts</c>: instante do envio, em UTC.
    /// </summary>
    public required DateTime Ts { get; init; }

    /// <summary>
    /// Serializa o comando no formato do contrato. O <c>ts</c> sai em ISO 8601 UTC com segundos
    /// inteiros (<c>yyyy-MM-ddTHH:mm:ssZ</c>), o mesmo formato que o capacete usa.
    /// </summary>
    /// <returns>O payload em UTF-8.</returns>
    public byte[] ParaPayload()
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var escritor = new Utf8JsonWriter(buffer))
        {
            escritor.WriteStartObject();
            escritor.WriteString("tipo", NomeContrato.Para(Tipo));
            escritor.WriteString(
                "ts",
                Ts.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            escritor.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }
}
