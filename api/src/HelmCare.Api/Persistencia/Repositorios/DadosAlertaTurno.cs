using System;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Persistencia.Repositorios;

/// <summary>
/// Dados crus do <c>GET /turnos/{id}/alerta</c>, lidos numa única consulta com projeção. As
/// regras do que aparece na resposta (por exemplo, <c>contagemTerminaEm</c> só em
/// <c>CONTAGEM</c>) ficam no serviço, não aqui.
/// </summary>
/// <param name="EstadoCapacete">Último estado recebido do capacete (<c>Dispositivo.EstadoAtual</c>).</param>
/// <param name="IndiceCalorC">Último índice de calor da telemetria, ou nulo.</param>
/// <param name="ContagemTerminaEm">Fim da contagem do impacto em aberto mais recente, ou nulo.</param>
/// <param name="OcorrenciaAtivaId">Id da ocorrência ativa do entregador, ou nulo.</param>
public sealed record DadosAlertaTurno(
    EstadoDispositivo EstadoCapacete,
    double? IndiceCalorC,
    DateTime? ContagemTerminaEm,
    Guid? OcorrenciaAtivaId);
