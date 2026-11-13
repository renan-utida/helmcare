namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Previsão do tempo para o período do turno, no formato do campo <c>previsao</c> do
/// <c>POST /risco/v1/indice-turno</c> (seção 8 do contrato).
/// </summary>
/// <param name="ChuvaMmHora">Chuva prevista, em mm/h.</param>
/// <param name="TemperaturaC">Temperatura prevista, em °C.</param>
/// <param name="UmidadePct">Umidade relativa prevista, em %.</param>
public sealed record PrevisaoTurno(double ChuvaMmHora, double TemperaturaC, double UmidadePct);
