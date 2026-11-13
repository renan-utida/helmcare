using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using HelmCare.Api.Mensageria.Mensagens;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Mensageria;

/// <summary>
/// Transforma o payload recebido do broker em <see cref="TelemetriaMensagem"/> ou
/// <see cref="EventoMensagem"/>, ou explica por que recusou: JSON inválido, campo obrigatório
/// ausente ou de tipo errado, <c>tipo</c> ou <c>estado</c> fora do contrato, <c>ts</c> sem data
/// válida ou <c>dispositivo</c> diferente do código do tópico.
/// </summary>
/// <remarks>
/// O <c>ts</c> vem do relógio do capacete e não é comparado com a hora do servidor: no Wokwi ele
/// anda mais devagar que o relógio real, e um evento legítimo seria recusado (CLAUDE.md, Mensageria).
/// O motivo da recusa cita só o nome do campo, nunca o valor recebido (pode ser coordenada).
/// </remarks>
public static class LeitorMensagemMqtt
{
    /// <summary>
    /// Lê uma telemetria (seção 6.1). <c>temperaturaC</c>, <c>umidadePct</c> e <c>indiceCalorC</c>
    /// podem vir nulos (o capacete manda <c>null</c> enquanto o DHT22 não leu) e <c>bateriaPct</c>
    /// é opcional; os demais campos são obrigatórios.
    /// </summary>
    /// <param name="codigoDoTopico">Código do capacete extraído do tópico.</param>
    /// <param name="payload">Payload em UTF-8.</param>
    /// <param name="mensagem">A telemetria, quando válida.</param>
    /// <param name="motivo">O motivo da recusa, quando inválida (vazio quando válida).</param>
    /// <returns><see langword="true"/> se a telemetria é válida.</returns>
    public static bool TentarLerTelemetria(
        string codigoDoTopico,
        ReadOnlyMemory<byte> payload,
        [NotNullWhen(true)] out TelemetriaMensagem? mensagem,
        out string motivo)
    {
        mensagem = null;

        if (!TentarAbrir(payload, out JsonDocument? documento, out motivo))
        {
            return false;
        }

        using (documento)
        {
            JsonElement raiz = documento.RootElement;

            if (!TentarLerCabecalho(raiz, codigoDoTopico, out string dispositivo, out DateTime ts, out motivo)
                || !TentarLerEnum(raiz, "estado", out EstadoDispositivo estado, out motivo)
                || !TentarLerNumeroOpcional(raiz, "temperaturaC", out double? temperatura, out motivo)
                || !TentarLerNumeroOpcional(raiz, "umidadePct", out double? umidade, out motivo)
                || !TentarLerNumeroOpcional(raiz, "indiceCalorC", out double? indiceCalor, out motivo)
                || !TentarLerBooleano(raiz, "presenca", out bool presenca, out motivo)
                || !TentarLerNumero(raiz, "velocidadeKmh", out double velocidade, out motivo)
                || !TentarLerNumeroOpcional(raiz, "bateriaPct", out double? bateria, out motivo))
            {
                return false;
            }

            mensagem = new TelemetriaMensagem
            {
                Dispositivo = dispositivo,
                Ts = ts,
                Estado = estado,
                TemperaturaC = temperatura,
                UmidadePct = umidade,
                IndiceCalorC = indiceCalor,
                Presenca = presenca,
                VelocidadeKmh = velocidade,
                BateriaPct = bateria is null ? null : (int)Math.Round(bateria.Value),
            };
            return true;
        }
    }

    /// <summary>
    /// Lê um evento (seção 6.2). Todos os campos são obrigatórios.
    /// </summary>
    /// <param name="codigoDoTopico">Código do capacete extraído do tópico.</param>
    /// <param name="payload">Payload em UTF-8.</param>
    /// <param name="mensagem">O evento, quando válido.</param>
    /// <param name="motivo">O motivo da recusa, quando inválido (vazio quando válido).</param>
    /// <returns><see langword="true"/> se o evento é válido.</returns>
    public static bool TentarLerEvento(
        string codigoDoTopico,
        ReadOnlyMemory<byte> payload,
        [NotNullWhen(true)] out EventoMensagem? mensagem,
        out string motivo)
    {
        mensagem = null;

        if (!TentarAbrir(payload, out JsonDocument? documento, out motivo))
        {
            return false;
        }

        using (documento)
        {
            JsonElement raiz = documento.RootElement;

            if (!TentarLerCabecalho(raiz, codigoDoTopico, out string dispositivo, out DateTime ts, out motivo)
                || !TentarLerEnum(raiz, "tipo", out TipoEventoMqtt tipo, out motivo)
                || !TentarLerNumero(raiz, "picoG", out double picoG, out motivo)
                || !TentarLerBooleano(raiz, "orientacaoAnormal", out bool orientacaoAnormal, out motivo)
                || !TentarLerBooleano(raiz, "pilotandoAntes", out bool pilotandoAntes, out motivo)
                || !TentarLerBooleano(raiz, "presencaAntes", out bool presencaAntes, out motivo)
                || !TentarLerNumero(raiz, "latitude", out double latitude, out motivo)
                || !TentarLerNumero(raiz, "longitude", out double longitude, out motivo))
            {
                return false;
            }

            mensagem = new EventoMensagem
            {
                Dispositivo = dispositivo,
                Ts = ts,
                Tipo = tipo,
                PicoG = picoG,
                OrientacaoAnormal = orientacaoAnormal,
                PilotandoAntes = pilotandoAntes,
                PresencaAntes = presencaAntes,
                Latitude = latitude,
                Longitude = longitude,
            };
            return true;
        }
    }

    private static bool TentarAbrir(
        ReadOnlyMemory<byte> payload,
        [NotNullWhen(true)] out JsonDocument? documento,
        out string motivo)
    {
        try
        {
            documento = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            documento = null;
            motivo = "payload não é um JSON válido";
            return false;
        }

        if (documento.RootElement.ValueKind != JsonValueKind.Object)
        {
            documento.Dispose();
            documento = null;
            motivo = "payload não é um objeto JSON";
            return false;
        }

        motivo = string.Empty;
        return true;
    }

    // Campos comuns às duas mensagens: dispositivo (igual ao do tópico) e ts (UTC).
    private static bool TentarLerCabecalho(
        JsonElement raiz,
        string codigoDoTopico,
        out string dispositivo,
        out DateTime ts,
        out string motivo)
    {
        ts = default;

        if (!TentarLerTexto(raiz, "dispositivo", out dispositivo, out motivo))
        {
            return false;
        }

        if (!string.Equals(dispositivo, codigoDoTopico, StringComparison.Ordinal))
        {
            motivo = "campo dispositivo diferente do código do tópico";
            return false;
        }

        if (!TentarLerTexto(raiz, "ts", out string texto, out motivo))
        {
            return false;
        }

        // Sem fuso explícito, o ts é tratado como UTC (o contrato só usa UTC). Com fuso, é convertido.
        if (!DateTimeOffset.TryParse(
                texto,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset instante))
        {
            motivo = "campo ts não é uma data ISO 8601";
            return false;
        }

        ts = instante.UtcDateTime;
        return true;
    }

    private static bool TentarLerTexto(JsonElement raiz, string campo, out string valor, out string motivo)
    {
        valor = string.Empty;

        if (!raiz.TryGetProperty(campo, out JsonElement elemento) || elemento.ValueKind == JsonValueKind.Null)
        {
            motivo = $"campo {campo} ausente";
            return false;
        }

        if (elemento.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(elemento.GetString()))
        {
            motivo = $"campo {campo} deve ser um texto";
            return false;
        }

        valor = elemento.GetString()!;
        motivo = string.Empty;
        return true;
    }

    private static bool TentarLerEnum<TEnum>(JsonElement raiz, string campo, out TEnum valor, out string motivo)
        where TEnum : struct, Enum
    {
        valor = default;

        if (!TentarLerTexto(raiz, campo, out string texto, out motivo))
        {
            return false;
        }

        if (!NomeContrato.TentarLer(texto, out valor))
        {
            motivo = $"campo {campo} com valor fora do contrato";
            return false;
        }

        return true;
    }

    private static bool TentarLerBooleano(JsonElement raiz, string campo, out bool valor, out string motivo)
    {
        valor = false;

        if (!raiz.TryGetProperty(campo, out JsonElement elemento) || elemento.ValueKind == JsonValueKind.Null)
        {
            motivo = $"campo {campo} ausente";
            return false;
        }

        if (elemento.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            motivo = $"campo {campo} deve ser true ou false";
            return false;
        }

        valor = elemento.GetBoolean();
        motivo = string.Empty;
        return true;
    }

    private static bool TentarLerNumero(JsonElement raiz, string campo, out double valor, out string motivo)
    {
        valor = 0;

        if (!TentarLerNumeroOpcional(raiz, campo, out double? lido, out motivo))
        {
            return false;
        }

        if (lido is null)
        {
            motivo = $"campo {campo} ausente";
            return false;
        }

        valor = lido.Value;
        return true;
    }

    // Ausente ou null: valor nulo, sem erro. Presente: precisa ser número.
    private static bool TentarLerNumeroOpcional(JsonElement raiz, string campo, out double? valor, out string motivo)
    {
        valor = null;
        motivo = string.Empty;

        if (!raiz.TryGetProperty(campo, out JsonElement elemento) || elemento.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (elemento.ValueKind != JsonValueKind.Number || !elemento.TryGetDouble(out double numero))
        {
            motivo = $"campo {campo} deve ser um número";
            return false;
        }

        valor = numero;
        return true;
    }
}
