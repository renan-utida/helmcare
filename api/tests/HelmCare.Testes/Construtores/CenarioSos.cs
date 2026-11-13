using System;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Dominio.ObjetosDeValor;

namespace HelmCare.Testes.Construtores;

/// <summary>
/// Monta o cenário de referência do SOS para os testes (dados simulados).
/// Linha do tempo: queda (<c>IMPACTO_DETECTADO</c>) às 21:15:37Z, SOS às 21:16:02Z (25 s depois)
/// e aceite às 21:16:40Z, com a queda na região da Av. Paulista.
/// </summary>
internal static class CenarioSos
{
    /// <summary>
    /// Instante da queda: 2026-11-12T21:15:37Z.
    /// </summary>
    public static readonly DateTime Queda = new(2026, 11, 12, 21, 15, 37, DateTimeKind.Utc);

    /// <summary>
    /// Instante do SOS e da abertura da ocorrência: 2026-11-12T21:16:02Z.
    /// </summary>
    public static readonly DateTime Sos = new(2026, 11, 12, 21, 16, 2, DateTimeKind.Utc);

    /// <summary>
    /// Instante do aceite de referência: 2026-11-12T21:16:40Z (63 s depois da queda).
    /// </summary>
    public static readonly DateTime Aceite = new(2026, 11, 12, 21, 16, 40, DateTimeKind.Utc);

    /// <summary>
    /// Local da queda: coordenadas simuladas do capacete (seção 5 do contrato).
    /// </summary>
    public static readonly Coordenada Local = new(-23.5641, -46.6525);

    /// <summary>
    /// Posição a cerca de 0,47 km do local: dentro do raio da camada 1.
    /// </summary>
    public static readonly Coordenada PosicaoProxima = new(-23.5613, -46.6560);

    /// <summary>
    /// Posição a cerca de 2,47 km do local (região da Sé): fora do raio da camada 1.
    /// </summary>
    public static readonly Coordenada PosicaoDistante = new(-23.5505, -46.6333);

    /// <summary>
    /// <c>JANELA_OBSERVACAO_S + CONTAGEM_CANCELAMENTO_S</c>: 25 s entre a queda e o SOS.
    /// </summary>
    public static readonly TimeSpan AntecedenciaSos = TimeSpan.FromSeconds(25);

    /// <summary>
    /// Trecho do capacete no indicador quando ele não pode ser medido (<c>JANELA_OBSERVACAO_S + CONTAGEM_CANCELAMENTO_S</c>): 25 s.
    /// </summary>
    public static readonly TimeSpan TrechoCapacetePadrao = TimeSpan.FromSeconds(25);

    /// <summary>
    /// <c>CONTAGEM_CANCELAMENTO_S</c>: 15 s.
    /// </summary>
    public static readonly TimeSpan Contagem = TimeSpan.FromSeconds(15);

    /// <summary>
    /// <c>TEMPO_ACEITE_CAMADA_1_S</c>: 60 s.
    /// </summary>
    public static readonly TimeSpan TempoAceiteCamada1 = TimeSpan.FromSeconds(60);

    /// <summary>
    /// <c>RAIO_CAMADA_1_KM</c>: 2 km.
    /// </summary>
    public const double RaioCamada1Km = 2;

    /// <summary>
    /// Cria um entregador com dados simulados.
    /// </summary>
    /// <returns>O entregador.</returns>
    public static Entregador NovoEntregador()
    {
        return new Entregador("Entregador Teste", "12345678912", "01234567890", "11987654321", Queda);
    }

    /// <summary>
    /// Registra o impacto da queda de referência, em aberto (pilotando e com o capacete na cabeça).
    /// </summary>
    /// <returns>O evento em aberto.</returns>
    public static EventoImpacto NovoEventoEmAberto()
    {
        return EventoImpacto.Registrar(Guid.NewGuid(), Queda, 4.8, true, true, true);
    }

    /// <summary>
    /// Abre a ocorrência de referência às 21:16:02Z, a partir de um evento em aberto.
    /// </summary>
    /// <param name="entregadorId">Entregador que caiu; um novo id quando omitido.</param>
    /// <param name="evento">Evento a usar; o de referência quando omitido.</param>
    /// <returns>A ocorrência em <c>SOS_CAMADA_1</c>.</returns>
    public static Ocorrencia AbrirOcorrencia(Guid? entregadorId = null, EventoImpacto? evento = null)
    {
        return Ocorrencia.Abrir(entregadorId ?? Guid.NewGuid(), evento ?? NovoEventoEmAberto(), Local, Sos);
    }

    /// <summary>
    /// Cria um voluntário disponível na posição informada.
    /// </summary>
    /// <param name="posicao">Posição atual do voluntário.</param>
    /// <param name="entregadorId">Entregador que é o voluntário; um novo id quando omitido.</param>
    /// <returns>O voluntário disponível.</returns>
    public static Voluntario NovoVoluntarioDisponivel(Coordenada posicao, Guid? entregadorId = null)
    {
        var voluntario = new Voluntario(entregadorId ?? Guid.NewGuid(), TipoVoluntario.Entregador);
        voluntario.AtualizarDisponibilidade(true, posicao);
        return voluntario;
    }

    /// <summary>
    /// Leva uma ocorrência nova até o estado pedido pelo caminho normal da máquina de estados.
    /// </summary>
    /// <param name="estado">Estado desejado.</param>
    /// <returns>A ocorrência no estado pedido.</returns>
    public static Ocorrencia OcorrenciaEm(EstadoOcorrencia estado)
    {
        Ocorrencia ocorrencia = AbrirOcorrencia();
        DateTime depoisDoEscalonamento = Sos + TempoAceiteCamada1;

        switch (estado)
        {
            case EstadoOcorrencia.SosCamada1:
                break;
            case EstadoOcorrencia.SosCamada2:
                ocorrencia.EscalarParaCamada2(depoisDoEscalonamento, TempoAceiteCamada1);
                break;
            case EstadoOcorrencia.SocorroACaminho:
                ocorrencia.Aceitar(NovoVoluntarioDisponivel(PosicaoProxima), Aceite, RaioCamada1Km, TrechoCapacetePadrao);
                break;
            case EstadoOcorrencia.Encerrada:
                ocorrencia.Encerrar(Aceite);
                break;
            case EstadoOcorrencia.Cancelada:
                ocorrencia.Cancelar(Aceite);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado sem cenário.");
        }

        return ocorrencia;
    }
}
