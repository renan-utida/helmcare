using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Índice de Risco do Turno calculado dentro da API, sem chamar o Serviço de Risco. É a reserva
/// do <c>ServicoRiscoComReserva</c> e segue a mesma regra do serviço, para o mesmo turno ter
/// o mesmo índice com o serviço no ar ou não (seções 8 e 8.1 do contrato).
/// </summary>
/// <remarks>
/// <para>
/// <b>Componente sinistro:</b> as horas do turno são as horas cheias UTC que se sobrepõem a ele
/// (como no <c>horas_turno.py</c>), e cada uma pesa os segundos que fica dentro do turno. O
/// componente é a média ponderada dos valores da tabela, sem arredondar; o único arredondamento é
/// o da <see cref="CalculadoraIndiceRisco"/>. É a agregação da seção 8.1 (fim da pendência).
/// </para>
/// <para>
/// <b>Explicação:</b> o mesmo texto do <c>explicacao.py</c>, com as mesmas frases e os limites
/// aplicados sobre os componentes já arredondados.
/// </para>
/// </remarks>
public sealed class ServicoRiscoSimulado : IServicoRisco
{
    // A partir deste valor (inclusive), chuva e calor entram na explicação.
    private const int ComponenteMinimoParaCitar = 1;

    // Limites superiores (inclusive) das faixas BAIXO e MODERADO, usados também na frase do histórico.
    private const int LimiteBaixo = 25;
    private const int LimiteModerado = 50;

    /// <summary>
    /// Fuso de São Paulo, fixo em UTC-3: o Brasil não tem horário de verão desde 2019.
    /// </summary>
    public static readonly TimeSpan FusoSaoPaulo = TimeSpan.FromHours(-3);

    private static readonly TimeSpan UmaHora = TimeSpan.FromHours(1);

    // Componente sinistro por dia da semana (linha 0 = segunda, como o weekday() do Python) e hora
    // local (coluna 0 a 23), já normalizado: 100 × previsão do modelo ÷ P95 (0,322927), limitado a
    // 100 e sem arredondar a inteiro. Gerada com o modelo real (ml/modelo, versão 1.0.0) para o
    // mês 11 e sem feriado, com 2 casas.
    //
    // Limitação: o mês e os feriados não entram; vale a tabela de novembro sem feriado para
    // qualquer data. Em 15/11 e 20/11, feriados nacionais, o serviço usa feriado = 1 e a simulada
    // pode dar um valor diferente. Se a versão do modelo mudar, a tabela precisa ser gerada de novo.
    private static readonly double[][] SinistroPorDiaEHora =
    [
        [62.26, 45.87, 43.66, 43.66, 43.94, 42.59, 59.73, 74.41, 74.14, 57.71, 57.71, 57.83, 66.34, 66.34, 66.34, 68.92, 76.33, 78.68, 79.75, 82.14, 72.72, 62.67, 61.85, 36.08],
        [30.67, 23.52, 18.58, 18.58, 18.58, 33.53, 62.38, 74.75, 74.48, 57.97, 57.97, 58.09, 66.64, 66.64, 66.64, 69.24, 76.98, 79.35, 80.99, 83.42, 74.57, 64.26, 61.97, 48.51],
        [33.33, 25.57, 20.19, 20.19, 20.19, 41.37, 71.65, 76.11, 75.67, 61.20, 61.20, 61.50, 71.31, 71.31, 71.31, 73.66, 80.89, 83.38, 85.09, 87.64, 78.53, 66.55, 63.92, 54.43],
        [39.11, 28.64, 21.05, 21.05, 21.05, 43.71, 73.68, 76.11, 75.67, 61.20, 61.20, 61.50, 71.31, 71.31, 71.31, 73.66, 80.89, 83.38, 85.18, 87.50, 78.41, 66.45, 63.82, 53.64],
        [42.10, 35.16, 27.49, 26.99, 27.65, 47.93, 72.49, 75.60, 74.80, 61.92, 61.92, 62.23, 72.15, 72.65, 72.65, 76.47, 90.35, 90.35, 94.50, 97.07, 97.62, 97.73, 94.82, 89.84],
        [77.65, 66.50, 66.62, 49.50, 58.72, 78.26, 73.75, 68.26, 67.22, 57.61, 57.61, 59.58, 69.08, 69.94, 69.94, 73.79, 87.19, 87.19, 91.46, 95.11, 95.65, 95.75, 91.95, 87.13],
        [80.78, 74.04, 74.58, 70.84, 71.13, 74.74, 76.52, 64.27, 63.29, 46.35, 46.35, 47.80, 61.50, 63.77, 63.77, 68.75, 64.00, 64.00, 89.78, 93.36, 93.58, 93.68, 88.83, 84.17],
    ];

    /// <summary>
    /// Componente sinistro da tabela para um dia da semana e uma hora local.
    /// </summary>
    /// <param name="dia">Dia da semana local.</param>
    /// <param name="horaLocal">Hora local de São Paulo (0 a 23).</param>
    /// <returns>O valor normalizado, sem arredondar.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Quando a hora está fora de 0 a 23.</exception>
    public static double SinistroDaHora(DayOfWeek dia, int horaLocal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(horaLocal);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(horaLocal, 23);

        // O DayOfWeek do .NET começa no domingo (0); a tabela começa na segunda, como o Python.
        int linha = ((int)dia + 6) % 7;
        return SinistroPorDiaEHora[linha][horaLocal];
    }

    /// <summary>
    /// Divide o turno nas horas cheias UTC que se sobrepõem a ele, como o <c>gerar_horas_turno</c>
    /// do serviço: parte da hora cheia em que o início cai e avança de hora em hora enquanto a hora
    /// começar antes do fim. Um fim exatamente na hora cheia não inclui a hora seguinte.
    /// </summary>
    /// <param name="inicio">Início do turno, em UTC.</param>
    /// <param name="fim">Fim previsto, em UTC, depois do início.</param>
    /// <returns>As horas na ordem, com o peso e o valor da tabela de cada uma.</returns>
    /// <exception cref="ArgumentException">Quando o fim não é depois do início.</exception>
    public static IReadOnlyList<HoraTurnoSimulada> HorasDoTurno(DateTime inicio, DateTime fim)
    {
        if (fim <= inicio)
        {
            throw new ArgumentException("O fim do turno deve ser depois do início.", nameof(fim));
        }

        var horas = new List<HoraTurnoSimulada>();
        var hora = new DateTime(inicio.Ticks - (inicio.Ticks % TimeSpan.TicksPerHour), DateTimeKind.Utc);

        for (; hora < fim; hora += UmaHora)
        {
            // Parte desta hora que fica dentro do turno: min(fim, hora + 1h) - max(início, hora).
            DateTime fimDaParte = fim < hora + UmaHora ? fim : hora + UmaHora;
            DateTime inicioDaParte = inicio > hora ? inicio : hora;
            DateTime local = hora + FusoSaoPaulo;

            horas.Add(new HoraTurnoSimulada(
                hora,
                local.DayOfWeek,
                local.Hour,
                (fimDaParte - inicioDaParte).TotalSeconds,
                SinistroDaHora(local.DayOfWeek, local.Hour)));
        }

        return horas;
    }

    /// <summary>
    /// Componente sinistro do turno: média ponderada dos valores da tabela pelos segundos de cada
    /// hora dentro do turno, sem arredondar, e a hora de pico.
    /// </summary>
    /// <param name="inicio">Início do turno, em UTC.</param>
    /// <param name="fim">Fim previsto, em UTC, depois do início.</param>
    /// <returns>A média e a hora local de maior valor (a primeira delas, em caso de empate).</returns>
    /// <exception cref="ArgumentException">Quando o fim não é depois do início.</exception>
    public static (double Media, int HoraPico) AgregarSinistro(DateTime inicio, DateTime fim)
    {
        IReadOnlyList<HoraTurnoSimulada> horas = HorasDoTurno(inicio, fim);

        // Mesma ordem de contas do serviço: soma de valor × peso dividida pela soma dos pesos.
        double somaPonderada = 0;
        double somaPesos = 0;
        HoraTurnoSimulada pico = horas[0];

        foreach (HoraTurnoSimulada hora in horas)
        {
            somaPonderada += hora.Sinistro * hora.SegundosNoTurno;
            somaPesos += hora.SegundosNoTurno;

            if (hora.Sinistro > pico.Sinistro)
            {
                pico = hora;
            }
        }

        return (somaPonderada / somaPesos, pico.HoraLocal);
    }

    /// <summary>
    /// Monta a explicação em uma frase, igual ao <c>gerar_explicacao</c> do serviço:
    /// <c>Risco {faixa}: </c>, os trechos (chuva, histórico do horário e calor) e ponto final.
    /// </summary>
    /// <remarks>
    /// A explicação nunca diz que a chuva aumenta os sinistros graves (seção 8): ela só avisa que
    /// há chuva prevista, num trecho separado do histórico.
    /// </remarks>
    /// <param name="calculo">Índice, faixa e componentes já arredondados.</param>
    /// <param name="horaPico">Hora local de maior valor da tabela no turno.</param>
    /// <param name="indiceCalorC">Índice de calor previsto, em °C.</param>
    /// <returns>A explicação.</returns>
    public static string Explicar(CalculoIndiceRisco calculo, int horaPico, double indiceCalorC)
    {
        ArgumentNullException.ThrowIfNull(calculo);

        var trechos = new List<string>();

        if (calculo.Chuva >= ComponenteMinimoParaCitar)
        {
            trechos.Add("chuva prevista no turno");
        }

        trechos.Add(calculo.Sinistro switch
        {
            > LimiteModerado => $"o seu horário tem histórico de sinistros graves (pico às {horaPico.ToString(CultureInfo.InvariantCulture)}h)",
            > LimiteBaixo => "o seu horário tem histórico moderado de sinistros graves",
            _ => "o seu horário tem poucos sinistros graves no histórico",
        });

        if (calculo.Calor >= ComponenteMinimoParaCitar)
        {
            trechos.Add($"calor previsto no turno (índice de calor de {UmaCasa(indiceCalorC)} °C)");
        }

        return $"Risco {NomeDaFaixa(calculo.FaixaRisco)}: {Juntar(trechos)}.";
    }

    /// <inheritdoc/>
    public Task<ResultadoRisco> CalcularAsync(
        DateTime inicio,
        DateTime fim,
        PrevisaoTurno previsao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previsao);

        (double sinistro, int horaPico) = AgregarSinistro(inicio, fim);
        double indiceCalorC = IndiceCalor.CalcularC(previsao.TemperaturaC, previsao.UmidadePct);
        CalculoIndiceRisco calculo = CalculadoraIndiceRisco.Calcular(sinistro, previsao.ChuvaMmHora, indiceCalorC);

        return Task.FromResult(new ResultadoRisco(
            calculo.IndiceRisco,
            calculo.FaixaRisco,
            calculo.Sinistro,
            calculo.Chuva,
            calculo.Calor,
            Explicar(calculo, horaPico, indiceCalorC)));
    }

    // "a", "a e b", "a, b e c".
    private static string Juntar(List<string> trechos)
    {
        return trechos.Count == 1
            ? trechos[0]
            : string.Join(", ", trechos.GetRange(0, trechos.Count - 1)) + " e " + trechos[^1];
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

    // Uma casa decimal com vírgula, arredondando 0,5 para cima sobre o valor × 10, como o
    // formatar_uma_casa do serviço, e sem depender da cultura instalada no servidor.
    private static string UmaCasa(double valor)
    {
        int decimos = CalculadoraIndiceRisco.ArredondarMeioParaCima(valor * 10);
        return (decimos / 10.0).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
