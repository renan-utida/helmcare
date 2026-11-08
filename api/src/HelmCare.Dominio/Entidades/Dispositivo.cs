using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Capacete inteligente (ESP32) de um entregador. Além dos campos do contrato, guarda o
/// último estado e o último índice de calor recebidos por MQTT (campos internos da v1.3),
/// para a API responder sem esperar a próxima mensagem.
/// </summary>
public sealed partial class Dispositivo : Entidade
{
    /// <summary>
    /// Tamanho do código do capacete (ex.: <c>HC-0001</c>).
    /// </summary>
    public const int TamanhoCodigo = 7;

    /// <summary>
    /// Quantidade de bytes aleatórios do QR token (256 bits).
    /// </summary>
    public const int BytesQrToken = 32;

    /// <summary>
    /// Cria um dispositivo com um QR token novo e o estado inicial <see cref="EstadoDispositivo.Desarmado"/>.
    /// </summary>
    /// <param name="codigo">Código no formato <c>HC-0000</c>.</param>
    /// <param name="entregadorId">Entregador dono do capacete.</param>
    /// <exception cref="ValidacaoDominioException">Quando o código ou o entregador são inválidos.</exception>
    public Dispositivo(string codigo, Guid entregadorId)
    {
        string codigoInformado = Garantir.TextoObrigatorio(codigo, "codigo", TamanhoCodigo);

        if (!FormatoCodigo().IsMatch(codigoInformado))
        {
            throw new ValidacaoDominioException("O campo codigo deve estar no formato HC-0000.");
        }

        Codigo = codigoInformado;
        EntregadorId = Garantir.IdInformado(entregadorId, "entregadorId");

        // O token vai impresso no QR Code do capacete e abre a página do Cofre, então
        // precisa ser imprevisível: 32 bytes de um gerador criptográfico (256 bits).
        // Base64Url (sem "+", "/" nem "=") porque o token entra na URL /cofre/{qrToken}.
        QrToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(BytesQrToken));
        EstadoAtual = EstadoDispositivo.Desarmado;
    }

    // Construtor usado só pelo EF Core ao materializar o registro do banco.
    private Dispositivo()
    {
        Codigo = string.Empty;
        QrToken = string.Empty;
    }

    /// <summary>
    /// Código do capacete, no formato <c>HC-0000</c>. Também identifica o capacete nos tópicos MQTT.
    /// </summary>
    public string Codigo { get; private set; }

    /// <summary>
    /// Entregador dono do capacete.
    /// </summary>
    public Guid EntregadorId { get; private set; }

    /// <summary>
    /// Token impresso no QR Code do capacete, que identifica o entregador na página do Cofre.
    /// É um segredo: nunca vai para logs.
    /// </summary>
    public string QrToken { get; private set; }

    /// <summary>
    /// Último estado do capacete informado por MQTT.
    /// </summary>
    public EstadoDispositivo EstadoAtual { get; private set; }

    /// <summary>
    /// Instante (<c>ts</c> da mensagem) do último estado aplicado, ou nulo se nenhum chegou.
    /// </summary>
    public DateTime? EstadoAtualizadoEm { get; private set; }

    /// <summary>
    /// Último índice de calor, em °C, recebido na telemetria.
    /// </summary>
    public double? UltimoIndiceCalorC { get; private set; }

    /// <summary>
    /// Instante (<c>ts</c> da telemetria) do último índice de calor aplicado.
    /// </summary>
    public DateTime? UltimoIndiceCalorEm { get; private set; }

    /// <summary>
    /// Aplica um estado recebido por MQTT, ignorando mensagens atrasadas: o MQTT não garante
    /// a ordem de entrega, e uma mensagem velha não pode sobrescrever um estado mais novo.
    /// </summary>
    /// <param name="estado">Estado informado pelo capacete.</param>
    /// <param name="ts">Instante da mensagem, em UTC.</param>
    /// <returns><see langword="true"/> se aplicou; <see langword="false"/> se a mensagem era mais antiga que o estado atual.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o estado é inválido ou o instante não está em UTC.</exception>
    public bool RegistrarEstado(EstadoDispositivo estado, DateTime ts)
    {
        Garantir.Definido(estado, "estado");
        Garantir.Utc(ts, "ts");

        if (ts < EstadoAtualizadoEm)
        {
            return false;
        }

        EstadoAtual = estado;
        EstadoAtualizadoEm = ts;
        return true;
    }

    /// <summary>
    /// Aplica o índice de calor recebido na telemetria, com a mesma regra de ordem de <see cref="RegistrarEstado"/>.
    /// </summary>
    /// <param name="valor">Índice de calor em °C.</param>
    /// <param name="ts">Instante da telemetria, em UTC.</param>
    /// <returns><see langword="true"/> se aplicou; <see langword="false"/> se a telemetria era mais antiga que a atual.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o valor não é um número finito ou o instante não está em UTC.</exception>
    public bool RegistrarIndiceCalor(double valor, DateTime ts)
    {
        if (!double.IsFinite(valor))
        {
            throw new ValidacaoDominioException("O campo indiceCalorC deve ser um número.");
        }

        Garantir.Utc(ts, "ts");

        if (ts < UltimoIndiceCalorEm)
        {
            return false;
        }

        UltimoIndiceCalorC = valor;
        UltimoIndiceCalorEm = ts;
        return true;
    }

    // Regex gerada em tempo de compilação. [0-9] em vez de \d, que aceitaria dígitos de outros alfabetos.
    [GeneratedRegex("^HC-[0-9]{4}$")]
    private static partial Regex FormatoCodigo();
}
