using System;
using HelmCare.Api.Mensageria.Mensagens;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Mensageria;

/// <summary>
/// Aplica a telemetria (seção 6.1) no capacete: o último estado e o último índice de calor, que
/// alimentam o <c>GET /turnos/{id}/alerta</c>. Sem idempotência: a telemetria não é guardada,
/// e uma repetida ou atrasada é ignorada pela regra de ordem do <see cref="Dispositivo"/>.
/// </summary>
public sealed class ProcessadorTelemetria
{
    /// <summary>
    /// Registra o estado e, se veio, o índice de calor.
    /// </summary>
    /// <param name="dispositivo">Capacete que publicou a telemetria.</param>
    /// <param name="telemetria">Telemetria lida.</param>
    /// <returns><see langword="true"/> se algum dado foi aplicado; <see langword="false"/> se a telemetria era mais antiga que os dados atuais.</returns>
    public bool Processar(Dispositivo dispositivo, TelemetriaMensagem telemetria)
    {
        ArgumentNullException.ThrowIfNull(dispositivo);
        ArgumentNullException.ThrowIfNull(telemetria);

        bool estadoAplicado = dispositivo.RegistrarEstado(telemetria.Estado, telemetria.Ts);
        bool calorAplicado = telemetria.IndiceCalorC is double indice
            && dispositivo.RegistrarIndiceCalor(indice, telemetria.Ts);

        return estadoAplicado || calorAplicado;
    }
}
