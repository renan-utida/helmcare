using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Índice de Risco do Turno devolvido pelo Serviço de Risco (resposta do
/// <c>POST /risco/v1/indice-turno</c>, seção 8 do contrato).
/// </summary>
/// <param name="IndiceRisco">Índice, de 0 a 100.</param>
/// <param name="FaixaRisco">Faixa do índice.</param>
/// <param name="Sinistro">Componente de sinistros, de 0 a 100.</param>
/// <param name="Chuva">Componente de chuva, de 0 a 100.</param>
/// <param name="Calor">Componente de calor, de 0 a 100.</param>
/// <param name="Explicacao">Explicação em linguagem simples, para o entregador.</param>
public sealed record ResultadoRisco(
    int IndiceRisco,
    FaixaRisco FaixaRisco,
    int Sinistro,
    int Chuva,
    int Calor,
    string Explicacao);
