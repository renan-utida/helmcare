using System.Collections.Generic;

namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Lista de trechos críticos devolvida pelo Serviço de Risco, na ordem recebida (decrescente de
/// sinistros graves, seção 7).
/// </summary>
/// <param name="Periodo">Período dos dados (ex.: <c>2019 a 2025</c>).</param>
/// <param name="Trechos">Os trechos, na ordem recebida.</param>
public sealed record ListaTrechosCriticos(string Periodo, IReadOnlyList<TrechoCritico> Trechos);
