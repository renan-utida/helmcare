using System;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Período de trabalho do entregador com o capacete armado. Guarda o Índice de Risco do
/// Turno calculado pelo Serviço de Risco e seus componentes (campos internos da v1.3).
/// </summary>
public sealed class Turno : Entidade
{
    /// <summary>
    /// Tamanho máximo da explicação do risco.
    /// </summary>
    public const int TamanhoMaximoExplicacao = 500;

    /// <summary>
    /// Abre um turno com a proteção ativa.
    /// </summary>
    /// <param name="entregadorId">Entregador do turno.</param>
    /// <param name="inicio">Início do turno, em UTC.</param>
    /// <param name="fimPrevisto">Fim previsto, em UTC, depois do início.</param>
    /// <exception cref="ValidacaoDominioException">Quando algum dado é inválido.</exception>
    public Turno(Guid entregadorId, DateTime inicio, DateTime fimPrevisto)
    {
        EntregadorId = Garantir.IdInformado(entregadorId, "entregadorId");
        Inicio = Garantir.Utc(inicio, "inicio");
        FimPrevisto = Garantir.Utc(fimPrevisto, "fimPrevisto");

        if (FimPrevisto <= Inicio)
        {
            throw new ValidacaoDominioException("O campo fimPrevisto deve ser depois do inicio.");
        }

        ProtecaoAtiva = true;
    }

    // Construtor usado só pelo EF Core ao materializar o registro do banco.
    private Turno()
    {
    }

    /// <summary>
    /// Entregador do turno.
    /// </summary>
    public Guid EntregadorId { get; private set; }

    /// <summary>
    /// Início do turno, em UTC.
    /// </summary>
    public DateTime Inicio { get; private set; }

    /// <summary>
    /// Fim previsto informado na abertura, em UTC. É o período enviado ao Serviço de Risco.
    /// </summary>
    public DateTime FimPrevisto { get; private set; }

    /// <summary>
    /// Fim real do turno, em UTC, ou nulo enquanto ele está ativo.
    /// </summary>
    public DateTime? Fim { get; private set; }

    /// <summary>
    /// Índice de Risco do Turno (0 a 100), ou nulo se o Serviço de Risco ainda não respondeu.
    /// </summary>
    public int? IndiceRisco { get; private set; }

    /// <summary>
    /// Faixa do índice de risco, sempre coerente com <see cref="IndiceRisco"/>.
    /// </summary>
    public FaixaRisco? FaixaRisco { get; private set; }

    /// <summary>
    /// Componente de sinistros (0 a 100), vindo do modelo de ML.
    /// </summary>
    public int? ComponenteSinistro { get; private set; }

    /// <summary>
    /// Componente de chuva (0 a 100), a exposição direta à chuva.
    /// </summary>
    public int? ComponenteChuva { get; private set; }

    /// <summary>
    /// Componente de calor (0 a 100), calculado pelo índice de calor.
    /// </summary>
    public int? ComponenteCalor { get; private set; }

    /// <summary>
    /// Explicação do risco em linguagem simples, mostrada ao entregador.
    /// </summary>
    public string? Explicacao { get; private set; }

    /// <summary>
    /// Indica se o capacete está protegendo o entregador neste turno.
    /// </summary>
    public bool ProtecaoAtiva { get; private set; }

    /// <summary>
    /// Verdadeiro enquanto o turno não foi encerrado.
    /// </summary>
    public bool EstaAtivo => Fim is null;

    /// <summary>
    /// Registra o risco calculado pelo Serviço de Risco. Valida tudo antes de alterar qualquer campo.
    /// </summary>
    /// <param name="indice">Índice de risco, de 0 a 100.</param>
    /// <param name="faixa">Faixa devolvida, que precisa bater com o índice.</param>
    /// <param name="sinistro">Componente de sinistros, de 0 a 100.</param>
    /// <param name="chuva">Componente de chuva, de 0 a 100.</param>
    /// <param name="calor">Componente de calor, de 0 a 100.</param>
    /// <param name="explicacao">Explicação obrigatória, até 500 caracteres.</param>
    /// <exception cref="ValidacaoDominioException">Quando algum valor está fora da faixa ou a faixa não bate com o índice.</exception>
    public void AplicarRisco(int indice, FaixaRisco faixa, int sinistro, int chuva, int calor, string explicacao)
    {
        FaixaRisco faixaEsperada = ClassificacaoRisco.FaixaDe(indice);
        Garantir.Intervalo(sinistro, "componentes.sinistro", 0, 100);
        Garantir.Intervalo(chuva, "componentes.chuva", 0, 100);
        Garantir.Intervalo(calor, "componentes.calor", 0, 100);
        string explicacaoValidada = Garantir.TextoObrigatorio(explicacao, "explicacao", TamanhoMaximoExplicacao);

        if (faixa != faixaEsperada)
        {
            throw new ValidacaoDominioException(
                $"O campo faixaRisco ({NomeContrato.Para(faixa)}) não corresponde ao indiceRisco {indice} ({NomeContrato.Para(faixaEsperada)}).");
        }

        IndiceRisco = indice;
        FaixaRisco = faixa;
        ComponenteSinistro = sinistro;
        ComponenteChuva = chuva;
        ComponenteCalor = calor;
        Explicacao = explicacaoValidada;
    }

    /// <summary>
    /// Encerra o turno e desliga a proteção. As regras que dependem do capacete e das
    /// ocorrências (409 da v1.3) ficam na camada de serviço.
    /// </summary>
    /// <param name="agora">Instante do encerramento, em UTC, não anterior ao início.</param>
    /// <exception cref="TransicaoInvalidaException">Quando o turno já foi encerrado.</exception>
    /// <exception cref="ValidacaoDominioException">Quando o instante não está em UTC ou é anterior ao início.</exception>
    public void Encerrar(DateTime agora)
    {
        Garantir.Utc(agora, "fim");

        if (!EstaAtivo)
        {
            throw new TransicaoInvalidaException("O turno já foi encerrado.");
        }

        if (agora < Inicio)
        {
            throw new ValidacaoDominioException("O campo fim não pode ser anterior ao inicio.");
        }

        Fim = agora;
        ProtecaoAtiva = false;
    }
}
