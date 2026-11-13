using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Índice de Risco do Turno calculado dentro da API, sem chamar o Serviço de Risco. Aplica a
/// fórmula da seção 8 pela <see cref="CalculadoraIndiceRisco"/>, com o componente <c>calor</c>
/// pelo <see cref="IndiceCalor"/> da previsão.
/// </summary>
/// <remarks>
/// <para>
/// O componente <c>sinistro</c>, que no serviço real vem do modelo de ML, sai de uma tabela
/// simulada por hora local de São Paulo. <b>Os números são ilustrativos, não são dados reais</b>:
/// só imitam o formato do dia (mais sinistros nos horários de pico).
/// </para>
/// <para>
/// O turno pega o maior valor entre as horas que ele cobre. A agregação real das horas do turno
/// (média, maior valor ou outra) é decisão da disciplina de SOA (pendência da seção 8.1).
/// </para>
/// <para>
/// Na sexta entra o cliente HTTP do Serviço de Risco (<c>POST /risco/v1/indice-turno</c>);
/// esta implementação fica como reserva.
/// </para>
/// </remarks>
public sealed class ServicoRiscoSimulado : IServicoRisco
{
    /// <summary>
    /// Fuso de São Paulo, fixo em UTC-3: o Brasil não tem horário de verão desde 2019.
    /// </summary>
    public static readonly TimeSpan FusoSaoPaulo = TimeSpan.FromHours(-3);

    // Componente sinistro simulado (0 a 100) para cada hora local, de 0h a 23h. Ilustrativo.
    private static readonly int[] SinistroPorHora =
    [
        20, 15, 12, 10, 10, 15, 35, 60, 65, 50, 40, 45,
        50, 45, 40, 45, 55, 70, 80, 75, 60, 50, 40, 30,
    ];

    /// <summary>
    /// Componente sinistro simulado de uma hora local (0 a 23).
    /// </summary>
    /// <param name="horaLocal">Hora local de São Paulo.</param>
    /// <returns>O valor da tabela simulada.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Quando a hora está fora de 0 a 23.</exception>
    public static int SinistroDaHora(int horaLocal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(horaLocal);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(horaLocal, 23);
        return SinistroPorHora[horaLocal];
    }

    /// <summary>
    /// Maior componente sinistro entre as horas locais que o turno cobre (inclusive a hora do
    /// início e a hora em que o fim cai, se ele não for exato).
    /// </summary>
    /// <param name="inicio">Início do turno, em UTC.</param>
    /// <param name="fim">Fim previsto, em UTC, depois do início.</param>
    /// <returns>O maior valor e a hora local em que ele aparece (a primeira, em caso de empate).</returns>
    /// <exception cref="ArgumentException">Quando o fim não é depois do início.</exception>
    public static (int Valor, int HoraLocal) MaiorSinistroDoTurno(DateTime inicio, DateTime fim)
    {
        if (fim <= inicio)
        {
            throw new ArgumentException("O fim do turno deve ser depois do início.", nameof(fim));
        }

        DateTime inicioLocal = inicio + FusoSaoPaulo;
        DateTime fimLocal = fim + FusoSaoPaulo;
        var hora = new DateTime(inicioLocal.Year, inicioLocal.Month, inicioLocal.Day, inicioLocal.Hour, 0, 0, DateTimeKind.Unspecified);

        (int Valor, int HoraLocal) maior = (-1, 0);

        for (; hora < fimLocal; hora = hora.AddHours(1))
        {
            int valor = SinistroPorHora[hora.Hour];

            if (valor > maior.Valor)
            {
                maior = (valor, hora.Hour);
            }
        }

        return maior;
    }

    /// <inheritdoc/>
    public Task<ResultadoRisco> CalcularAsync(
        DateTime inicio,
        DateTime fim,
        PrevisaoTurno previsao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previsao);

        (int sinistro, int horaPico) = MaiorSinistroDoTurno(inicio, fim);
        double indiceCalorC = IndiceCalor.CalcularC(previsao.TemperaturaC, previsao.UmidadePct);
        CalculoIndiceRisco calculo = CalculadoraIndiceRisco.Calcular(sinistro, previsao.ChuvaMmHora, indiceCalorC);

        string explicacao = Explicar(calculo, horaPico, previsao.ChuvaMmHora, indiceCalorC);

        return Task.FromResult(new ResultadoRisco(
            calculo.IndiceRisco,
            calculo.FaixaRisco,
            calculo.Sinistro,
            calculo.Chuva,
            calculo.Calor,
            explicacao));
    }

    // A explicação cita o componente que mais pesou no índice (pelo peso de cada um) e sempre
    // separa o histórico de sinistros, que já inclui o efeito da chuva nos acidentes, da
    // exposição direta à chuva (nota da seção 8: "A chuva aparece duas vezes, de propósito").
    private static string Explicar(CalculoIndiceRisco calculo, int horaPico, double chuvaMmHora, double indiceCalorC)
    {
        double pesoSinistro = 0.5 * calculo.Sinistro;
        double pesoChuva = 0.25 * calculo.Chuva;
        double pesoCalor = 0.25 * calculo.Calor;

        string maisPesou;

        if (pesoSinistro >= pesoChuva && pesoSinistro >= pesoCalor)
        {
            maisPesou = $"o histórico de sinistros graves com moto no horário do seu turno (pico às {horaPico}h)";
        }
        else if (pesoChuva >= pesoCalor)
        {
            maisPesou = "a chuva prevista para o turno";
        }
        else
        {
            maisPesou = "o calor previsto para o turno";
        }

        return $"Risco {NomeDaFaixa(calculo.FaixaRisco)} ({calculo.IndiceRisco}): o que mais pesou foi {maisPesou}. "
            + $"O histórico de sinistros (componente {calculo.Sinistro}) já considera o efeito da chuva nos acidentes; "
            + $"a chuva prevista de {UmaCasa(chuvaMmHora)} mm/h (componente {calculo.Chuva}) conta à parte, "
            + $"como a sua exposição direta ao tempo. Índice de calor previsto: {UmaCasa(indiceCalorC)} °C "
            + $"(componente {calculo.Calor}).";
    }

    private static string NomeDaFaixa(FaixaRisco faixa)
    {
        return faixa switch
        {
            FaixaRisco.Baixo => "baixo",
            FaixaRisco.Moderado => "moderado",
            FaixaRisco.Alto => "alto",
            _ => "muito alto",
        };
    }

    // Uma casa decimal com vírgula, sem depender da cultura instalada no servidor.
    private static string UmaCasa(double valor)
    {
        return Math.Round(valor, 1, MidpointRounding.AwayFromZero)
            .ToString("0.0", CultureInfo.InvariantCulture)
            .Replace('.', ',');
    }
}
