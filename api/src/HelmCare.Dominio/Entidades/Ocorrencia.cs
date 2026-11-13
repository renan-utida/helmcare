using System;
using System.Collections.Generic;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;
using HelmCare.Dominio.ObjetosDeValor;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// O SOS de um entregador. Segue a máquina de estados do CLAUDE.md:
/// <c>SOS_CAMADA_1</c> para <c>SOS_CAMADA_2</c> (escalonamento), para <c>SOCORRO_A_CAMINHO</c>
/// (aceite), e daí para os finais <c>ENCERRADA</c> ou <c>CANCELADA</c>. Qualquer outra
/// transição lança <see cref="TransicaoInvalidaException"/> sem alterar nenhum campo.
/// Os comandos MQTT de cada transição são enviados pela camada de serviço.
/// </summary>
public sealed class Ocorrencia : Entidade
{
    /// <summary>
    /// Camada inicial: voluntários a até <c>RAIO_CAMADA_1_KM</c>.
    /// </summary>
    public const int CamadaProxima = 1;

    /// <summary>
    /// Camada depois do escalonamento: toda a rede de voluntários disponíveis.
    /// </summary>
    public const int CamadaRedeToda = 2;

    private readonly List<AceiteSocorro> _aceites = [];

    // Construtor usado pela fábrica Abrir e pelo EF Core.
    private Ocorrencia()
    {
    }

    /// <summary>
    /// Entregador que caiu.
    /// </summary>
    public Guid EntregadorId { get; private set; }

    /// <summary>
    /// Impacto que originou o SOS.
    /// </summary>
    public Guid EventoImpactoId { get; private set; }

    /// <summary>
    /// Impacto que originou o SOS. Precisa estar carregado para aceitar e para o cronômetro.
    /// </summary>
    public EventoImpacto? EventoImpacto { get; private set; }

    /// <summary>
    /// Estado atual da ocorrência.
    /// </summary>
    public EstadoOcorrencia Estado { get; private set; }

    /// <summary>
    /// Camada de voluntários acionada: <see cref="CamadaProxima"/> ou <see cref="CamadaRedeToda"/>.
    /// </summary>
    public int Camada { get; private set; }

    /// <summary>
    /// Latitude exata da queda (localização pessoal, seção 9 do contrato).
    /// </summary>
    public double Latitude { get; private set; }

    /// <summary>
    /// Longitude exata da queda (localização pessoal, seção 9 do contrato).
    /// </summary>
    public double Longitude { get; private set; }

    /// <summary>
    /// Instante em que a API abriu a ocorrência, em UTC.
    /// </summary>
    public DateTime CriadaEm { get; private set; }

    /// <summary>
    /// Instante do aceite, em UTC, ou nulo se ninguém aceitou.
    /// </summary>
    public DateTime? AceitaEm { get; private set; }

    /// <summary>
    /// Instante do encerramento ou do cancelamento, em UTC.
    /// </summary>
    public DateTime? EncerradaEm { get; private set; }

    /// <summary>
    /// Indicador de impacto: segundos completos entre a queda e o aceite. Nulo quando não houve aceite.
    /// </summary>
    public int? TempoAteAceiteSegundos { get; private set; }

    /// <summary>
    /// Aceites registrados (no máximo um, porque o aceite tira a ocorrência da espera).
    /// </summary>
    public IReadOnlyCollection<AceiteSocorro> Aceites => _aceites.AsReadOnly();

    /// <summary>
    /// Verdadeiro em <c>SOS_CAMADA_1</c> ou <c>SOS_CAMADA_2</c>.
    /// </summary>
    public bool EstaAguardandoAceite => Estado is EstadoOcorrencia.SosCamada1 or EstadoOcorrencia.SosCamada2;

    /// <summary>
    /// Verdadeiro enquanto aguarda aceite ou está em <c>SOCORRO_A_CAMINHO</c>.
    /// </summary>
    public bool EstaAtiva => EstaAguardandoAceite || Estado == EstadoOcorrencia.SocorroACaminho;

    /// <summary>
    /// Abre o SOS em <c>SOS_CAMADA_1</c> e marca o impacto como <c>SOS_ENVIADO</c>.
    /// </summary>
    /// <param name="entregadorId">Entregador que caiu.</param>
    /// <param name="evento">Impacto em aberto que originou o SOS.</param>
    /// <param name="local">Local da queda informado no evento.</param>
    /// <param name="agora">Instante da abertura, em UTC.</param>
    /// <returns>A ocorrência aberta.</returns>
    /// <exception cref="ValidacaoDominioException">Quando algum dado é inválido.</exception>
    /// <exception cref="TransicaoInvalidaException">Quando o impacto já tem resultado.</exception>
    public static Ocorrencia Abrir(Guid entregadorId, EventoImpacto evento, Coordenada local, DateTime agora)
    {
        ArgumentNullException.ThrowIfNull(evento);
        Garantir.IdInformado(entregadorId, "entregadorId");
        Garantir.Utc(agora, "criadaEm");

        // Última verificação: lança se o impacto já tiver resultado. Depois dela nada mais falha.
        evento.MarcarSosEnviado();

        return new Ocorrencia
        {
            EntregadorId = entregadorId,
            EventoImpactoId = evento.Id,
            EventoImpacto = evento,
            Estado = EstadoOcorrencia.SosCamada1,
            Camada = CamadaProxima,
            Latitude = local.Latitude,
            Longitude = local.Longitude,
            CriadaEm = agora,
        };
    }

    /// <summary>
    /// Indica se já passou o tempo de aceite da camada 1 sem aceite.
    /// </summary>
    /// <param name="agora">Instante atual, em UTC.</param>
    /// <param name="tempoAceiteCamada1">Tempo de espera da camada 1 (<c>TEMPO_ACEITE_CAMADA_1_S</c>).</param>
    /// <returns><see langword="true"/> só em <c>SOS_CAMADA_1</c> com o tempo esgotado.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o instante não está em UTC ou o tempo não é positivo.</exception>
    public bool DeveEscalar(DateTime agora, TimeSpan tempoAceiteCamada1)
    {
        Garantir.Utc(agora, "agora");
        ExigirPositivo(tempoAceiteCamada1, "O tempo de aceite da camada 1");
        return Estado == EstadoOcorrencia.SosCamada1 && agora - CriadaEm >= tempoAceiteCamada1;
    }

    /// <summary>
    /// Escala o SOS para toda a rede de voluntários (<c>SOS_CAMADA_2</c>).
    /// </summary>
    /// <param name="agora">Instante atual, em UTC.</param>
    /// <param name="tempoAceiteCamada1">Tempo de espera da camada 1 (<c>TEMPO_ACEITE_CAMADA_1_S</c>).</param>
    /// <exception cref="TransicaoInvalidaException">Fora de <c>SOS_CAMADA_1</c>.</exception>
    /// <exception cref="RegraNegocioException">Antes de esgotar o tempo da camada 1.</exception>
    /// <exception cref="ValidacaoDominioException">Quando o instante ou o tempo são inválidos.</exception>
    public void EscalarParaCamada2(DateTime agora, TimeSpan tempoAceiteCamada1)
    {
        ExigirTransicao(agora, "escalar", EstadoOcorrencia.SosCamada1);
        ExigirPositivo(tempoAceiteCamada1, "O tempo de aceite da camada 1");

        if (agora - CriadaEm < tempoAceiteCamada1)
        {
            throw new RegraNegocioException("A camada 1 ainda está dentro do tempo de aceite.");
        }

        Estado = EstadoOcorrencia.SosCamada2;
        Camada = CamadaRedeToda;
    }

    /// <summary>
    /// Registra o aceite de um voluntário e para o cronômetro do indicador de impacto.
    /// </summary>
    /// <param name="voluntario">Voluntário que aceitou.</param>
    /// <param name="agora">Instante do aceite, em UTC.</param>
    /// <param name="raioCamada1Km">Raio da camada 1 (<c>RAIO_CAMADA_1_KM</c>).</param>
    /// <param name="trechoCapacetePadrao">
    /// Trecho do capacete quando ele não pode ser medido (<c>JANELA_OBSERVACAO_S + CONTAGEM_CANCELAMENTO_S</c>); veja <see cref="SegundosDesdeAQueda"/>.
    /// </param>
    /// <returns>O aceite criado.</returns>
    /// <exception cref="TransicaoInvalidaException">Fora de <c>SOS_CAMADA_1</c> ou <c>SOS_CAMADA_2</c>.</exception>
    /// <exception cref="RegraNegocioException">Quando o voluntário não pode atender esta ocorrência.</exception>
    /// <exception cref="ValidacaoDominioException">Quando o instante ou o raio são inválidos.</exception>
    /// <exception cref="InvalidOperationException">Quando a navegação <see cref="EventoImpacto"/> não foi carregada.</exception>
    public AceiteSocorro Aceitar(Voluntario voluntario, DateTime agora, double raioCamada1Km, TimeSpan trechoCapacetePadrao)
    {
        ArgumentNullException.ThrowIfNull(voluntario);
        ExigirTransicao(agora, "aceitar", EstadoOcorrencia.SosCamada1, EstadoOcorrencia.SosCamada2);

        // O indicador precisa do impacto e do trecho padrão; confere aqui, antes de alterar qualquer campo.
        ExigirEventoCarregado();
        ExigirPositivo(trechoCapacetePadrao, "O trecho padrão do capacete");

        if (!voluntario.PodeAtender(this, raioCamada1Km))
        {
            throw new RegraNegocioException(
                "O voluntário não pode atender esta ocorrência: precisa estar disponível, com posição, "
                + "não ser o próprio entregador e estar dentro do raio da camada 1.");
        }

        var aceite = new AceiteSocorro(Id, voluntario.Id, agora);
        _aceites.Add(aceite);
        Estado = EstadoOcorrencia.SocorroACaminho;
        AceitaEm = agora;

        // O indicador de impacto (README: tempo entre a queda e o primeiro aceite humano) conta
        // desde a queda, não desde CriadaEm: a observação e a contagem no capacete também são
        // tempo sem socorro.
        TempoAteAceiteSegundos = SegundosDesdeAQueda(agora, trechoCapacetePadrao);
        return aceite;
    }

    /// <summary>
    /// Cancela o SOS: o entregador informou que está bem.
    /// </summary>
    /// <param name="agora">Instante do cancelamento, em UTC.</param>
    /// <exception cref="TransicaoInvalidaException">Fora de <c>SOS_CAMADA_1</c> ou <c>SOS_CAMADA_2</c>.</exception>
    /// <exception cref="ValidacaoDominioException">Quando o instante é inválido.</exception>
    public void Cancelar(DateTime agora)
    {
        ExigirTransicao(agora, "cancelar", EstadoOcorrencia.SosCamada1, EstadoOcorrencia.SosCamada2);
        Estado = EstadoOcorrencia.Cancelada;
        EncerradaEm = agora;
    }

    /// <summary>
    /// Encerra o atendimento. Também vale sem aceite (decisão da v1.3: o SAMU pode chegar antes);
    /// nesse caso <see cref="TempoAteAceiteSegundos"/> continua nulo e a ocorrência conta como
    /// "sem aceite" nos indicadores, em vez de distorcer a média com um tempo que não existiu.
    /// </summary>
    /// <param name="agora">Instante do encerramento, em UTC.</param>
    /// <exception cref="TransicaoInvalidaException">Em <c>ENCERRADA</c> ou <c>CANCELADA</c>.</exception>
    /// <exception cref="ValidacaoDominioException">Quando o instante é inválido.</exception>
    public void Encerrar(DateTime agora)
    {
        ExigirTransicao(
            agora,
            "encerrar",
            EstadoOcorrencia.SosCamada1,
            EstadoOcorrencia.SosCamada2,
            EstadoOcorrencia.SocorroACaminho);

        Estado = EstadoOcorrencia.Encerrada;
        EncerradaEm = agora;
    }

    /// <summary>
    /// Segundos completos desde a queda até <paramref name="instante"/>. É o único lugar da regra do
    /// indicador de impacto (seção 7, "Indicador de impacto"): o <see cref="Aceitar"/> calcula o
    /// <see cref="TempoAteAceiteSegundos"/> por aqui, com o instante do aceite, e o cronômetro do
    /// <c>GET /ocorrencias/{id}</c> também.
    /// </summary>
    /// <remarks>
    /// <para>O tempo é medido em dois trechos, cada um no próprio relógio:</para>
    /// <list type="number">
    /// <item><description>Trecho do capacete: <c>EventoImpacto.ContagemTerminaEm - OcorridoEm</c>, os dois no
    /// relógio do capacete. Vale <paramref name="trechoCapacetePadrao"/> quando o impacto foi estimado a
    /// partir do SOS ou quando o <c>CONTAGEM_INICIADA</c> se perdeu (sem <c>ContagemTerminaEm</c>).</description></item>
    /// <item><description>Trecho do servidor: <paramref name="instante"/> - <see cref="CriadaEm"/>, os dois no
    /// relógio do servidor.</description></item>
    /// </list>
    /// <para>
    /// O motivo: no Wokwi, o relógio do capacete roda atrasado em relação à hora real (seção 6.2).
    /// Subtrair um instante do capacete de um instante do servidor colocaria esse atraso no indicador;
    /// com cada trecho no próprio relógio, o atraso some. Com relógios certos, o resultado é o mesmo
    /// de contar direto da queda até o instante.
    /// </para>
    /// </remarks>
    /// <param name="instante">Fim da contagem, em UTC, no relógio do servidor (o aceite, o encerramento ou o agora; quem escolhe é o chamador).</param>
    /// <param name="trechoCapacetePadrao">Trecho do capacete quando ele não pode ser medido (<c>JANELA_OBSERVACAO_S + CONTAGEM_CANCELAMENTO_S</c>, vindo da configuração).</param>
    /// <returns>A soma dos dois trechos em segundos completos, nunca negativa.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o instante não está em UTC ou o trecho padrão não é positivo.</exception>
    /// <exception cref="InvalidOperationException">Quando a navegação <see cref="EventoImpacto"/> não foi carregada.</exception>
    public int SegundosDesdeAQueda(DateTime instante, TimeSpan trechoCapacetePadrao)
    {
        Garantir.Utc(instante, "instante");
        ExigirPositivo(trechoCapacetePadrao, "O trecho padrão do capacete");
        EventoImpacto evento = ExigirEventoCarregado();

        TimeSpan trechoCapacete = evento.OcorridoEmEstimado || evento.ContagemTerminaEm is not { } fimContagem
            ? trechoCapacetePadrao
            : fimContagem - evento.OcorridoEm;
        TimeSpan trechoServidor = instante - CriadaEm;

        // Cada trecho negativo conta zero. A soma mantém as frações e só ela é arredondada para
        // baixo, uma vez: 25,6 s + 30,6 s dão 56, e não 25 + 30 = 55.
        TimeSpan total = NuncaNegativo(trechoCapacete) + NuncaNegativo(trechoServidor);
        return (int)Math.Min(total.Ticks / TimeSpan.TicksPerSecond, int.MaxValue);
    }

    private static TimeSpan NuncaNegativo(TimeSpan trecho)
    {
        return trecho < TimeSpan.Zero ? TimeSpan.Zero : trecho;
    }

    private static void ExigirPositivo(TimeSpan duracao, string descricao)
    {
        if (duracao <= TimeSpan.Zero)
        {
            throw new ValidacaoDominioException($"{descricao} deve ser maior que zero.");
        }
    }

    // Verificações comuns a toda transição, feitas antes de alterar qualquer campo.
    private void ExigirTransicao(DateTime agora, string acao, params EstadoOcorrencia[] permitidos)
    {
        Garantir.Utc(agora, "agora");

        if (Array.IndexOf(permitidos, Estado) < 0)
        {
            throw new TransicaoInvalidaException(
                $"Não é possível {acao} uma ocorrência em {NomeContrato.Para(Estado)}.");
        }

        if (agora < CriadaEm)
        {
            throw new ValidacaoDominioException("O instante da ação não pode ser anterior à criação da ocorrência.");
        }
    }

    private EventoImpacto ExigirEventoCarregado()
    {
        return EventoImpacto
            ?? throw new InvalidOperationException(
                "A navegação EventoImpacto da ocorrência não foi carregada. O serviço precisa incluí-la na consulta.");
    }
}
