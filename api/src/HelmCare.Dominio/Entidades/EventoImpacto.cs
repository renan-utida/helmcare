using System;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Registro de cada impacto detectado pelo capacete, inclusive os ignorados e os falsos
/// positivos (seção 3 do contrato). O <see cref="Resultado"/> fica nulo enquanto o desfecho
/// não chega, e cada evento MQTT da seção 6.2 o leva a um resultado final.
/// </summary>
public sealed class EventoImpacto : Entidade
{
    // Construtor usado pelas fábricas e pelo EF Core ao materializar o registro do banco.
    private EventoImpacto()
    {
    }

    /// <summary>
    /// Capacete que detectou o impacto.
    /// </summary>
    public Guid DispositivoId { get; private set; }

    /// <summary>
    /// Instante da queda, em UTC. É o início do indicador de impacto (<c>tempoAteAceiteSegundos</c>).
    /// </summary>
    public DateTime OcorridoEm { get; private set; }

    /// <summary>
    /// Verdadeiro quando <see cref="OcorridoEm"/> foi estimado a partir do SOS, porque o
    /// <c>IMPACTO_DETECTADO</c> não chegou (campo interno da v1.3).
    /// </summary>
    public bool OcorridoEmEstimado { get; private set; }

    /// <summary>
    /// Pico de aceleração total do impacto, em g.
    /// </summary>
    public double PicoG { get; private set; }

    /// <summary>
    /// Inclinação maior que 60° depois do impacto. Só informativo: não bloqueia a detecção.
    /// </summary>
    public bool OrientacaoAnormal { get; private set; }

    /// <summary>
    /// Se a moto estava em velocidade de pilotagem antes do impacto.
    /// </summary>
    public bool PilotandoAntes { get; private set; }

    /// <summary>
    /// Se o sensor de presença indicava o capacete na cabeça antes do impacto.
    /// </summary>
    public bool PresencaAntes { get; private set; }

    /// <summary>
    /// Desfecho do impacto, ou nulo enquanto está em observação ou contagem.
    /// </summary>
    public ResultadoImpacto? Resultado { get; private set; }

    /// <summary>
    /// Fim da contagem de cancelamento (<c>ts</c> do <c>CONTAGEM_INICIADA</c> mais
    /// <c>CONTAGEM_CANCELAMENTO_S</c>), exibido em <c>GET /turnos/{id}/alerta</c>. Campo interno da v1.3.
    /// </summary>
    public DateTime? ContagemTerminaEm { get; private set; }

    /// <summary>
    /// Verdadeiro enquanto o impacto ainda não tem desfecho.
    /// </summary>
    public bool EstaEmAberto => Resultado is null;

    /// <summary>
    /// Registra o evento <c>IMPACTO_DETECTADO</c>. Pela tabela "Como a API registra cada evento"
    /// (seção 6.2), com <paramref name="pilotandoAntes"/> e <paramref name="presencaAntes"/> falsos
    /// o capacete descarta o impacto e o registro já nasce <see cref="ResultadoImpacto.Ignorado"/>;
    /// com pelo menos um verdadeiro, começa a observação e o registro nasce em aberto.
    /// </summary>
    /// <param name="dispositivoId">Capacete que detectou o impacto.</param>
    /// <param name="ocorridoEm">Instante do impacto (<c>ts</c> do evento), em UTC.</param>
    /// <param name="picoG">Pico de aceleração, finito e maior que zero.</param>
    /// <param name="orientacaoAnormal">Inclinação anormal depois do impacto.</param>
    /// <param name="pilotandoAntes">Se estava pilotando antes.</param>
    /// <param name="presencaAntes">Se o capacete estava na cabeça antes.</param>
    /// <returns>O evento registrado.</returns>
    /// <exception cref="ValidacaoDominioException">Quando algum dado é inválido.</exception>
    public static EventoImpacto Registrar(
        Guid dispositivoId,
        DateTime ocorridoEm,
        double picoG,
        bool orientacaoAnormal,
        bool pilotandoAntes,
        bool presencaAntes)
    {
        EventoImpacto evento = Criar(dispositivoId, ocorridoEm, picoG, orientacaoAnormal, pilotandoAntes, presencaAntes);

        if (!pilotandoAntes && !presencaAntes)
        {
            evento.Resultado = ResultadoImpacto.Ignorado;
        }

        return evento;
    }

    /// <summary>
    /// Registra o impacto a partir de um <c>SOS</c> que chegou sem impacto em aberto (decisão da v1.3):
    /// o <c>IMPACTO_DETECTADO</c> se perdeu, então o instante da queda é estimado como
    /// <c>tsSos - antecedencia</c> (<c>JANELA_OBSERVACAO_S + CONTAGEM_CANCELAMENTO_S</c>).
    /// Nasce em aberto mesmo com <paramref name="pilotandoAntes"/> e <paramref name="presencaAntes"/>
    /// falsos, porque o SOS já prova que o capacete confirmou a queda.
    /// </summary>
    /// <param name="dispositivoId">Capacete que enviou o SOS.</param>
    /// <param name="tsSos">Instante do SOS, em UTC.</param>
    /// <param name="antecedencia">Tempo entre a queda e o SOS, maior que zero.</param>
    /// <param name="picoG">Pico de aceleração, finito e maior que zero.</param>
    /// <param name="orientacaoAnormal">Inclinação anormal depois do impacto.</param>
    /// <param name="pilotandoAntes">Se estava pilotando antes.</param>
    /// <param name="presencaAntes">Se o capacete estava na cabeça antes.</param>
    /// <returns>O evento registrado, com <see cref="OcorridoEmEstimado"/> verdadeiro.</returns>
    /// <exception cref="ValidacaoDominioException">Quando algum dado é inválido.</exception>
    public static EventoImpacto RegistrarEstimadoPorSos(
        Guid dispositivoId,
        DateTime tsSos,
        TimeSpan antecedencia,
        double picoG,
        bool orientacaoAnormal,
        bool pilotandoAntes,
        bool presencaAntes)
    {
        Garantir.Utc(tsSos, "ts");

        if (antecedencia <= TimeSpan.Zero)
        {
            throw new ValidacaoDominioException("A antecedência da queda estimada deve ser maior que zero.");
        }

        EventoImpacto evento = Criar(dispositivoId, tsSos - antecedencia, picoG, orientacaoAnormal, pilotandoAntes, presencaAntes);
        evento.OcorridoEmEstimado = true;
        return evento;
    }

    /// <summary>
    /// Registra o evento <c>CONTAGEM_INICIADA</c>: guarda quando a contagem de cancelamento termina.
    /// </summary>
    /// <param name="ts">Instante do evento, em UTC, não anterior ao impacto.</param>
    /// <param name="duracao">Duração da contagem (<c>CONTAGEM_CANCELAMENTO_S</c>), maior que zero.</param>
    /// <exception cref="TransicaoInvalidaException">Quando o impacto já tem resultado ou a contagem já foi iniciada.</exception>
    /// <exception cref="ValidacaoDominioException">Quando o instante ou a duração são inválidos.</exception>
    public void IniciarContagem(DateTime ts, TimeSpan duracao)
    {
        Garantir.Utc(ts, "ts");
        ExigirEmAberto("iniciar a contagem de");

        if (ContagemTerminaEm is not null)
        {
            throw new TransicaoInvalidaException("A contagem deste impacto já foi iniciada.");
        }

        if (ts < OcorridoEm)
        {
            throw new ValidacaoDominioException("O campo ts não pode ser anterior ao impacto.");
        }

        if (duracao <= TimeSpan.Zero)
        {
            throw new ValidacaoDominioException("A duração da contagem deve ser maior que zero.");
        }

        ContagemTerminaEm = ts + duracao;
    }

    /// <summary>
    /// Registra o evento <c>FALSO_POSITIVO</c>: a velocidade de moto voltou durante a observação.
    /// </summary>
    /// <exception cref="TransicaoInvalidaException">Quando o impacto já tem resultado.</exception>
    public void MarcarFalsoPositivo()
    {
        DefinirResultado(ResultadoImpacto.FalsoPositivo, "marcar como falso positivo");
    }

    /// <summary>
    /// Registra o evento <c>CANCELADO</c>: o entregador cancelou durante a contagem.
    /// </summary>
    /// <exception cref="TransicaoInvalidaException">Quando o impacto já tem resultado.</exception>
    public void MarcarCancelado()
    {
        DefinirResultado(ResultadoImpacto.CanceladoPeloEntregador, "cancelar");
    }

    /// <summary>
    /// Marca o impacto como <see cref="ResultadoImpacto.SosEnviado"/>. Interno: só
    /// <c>Ocorrencia.Abrir</c> chama, para todo <c>SOS_ENVIADO</c> ter uma ocorrência.
    /// </summary>
    /// <exception cref="TransicaoInvalidaException">Quando o impacto já tem resultado.</exception>
    internal void MarcarSosEnviado()
    {
        DefinirResultado(ResultadoImpacto.SosEnviado, "enviar o SOS de");
    }

    private static EventoImpacto Criar(
        Guid dispositivoId,
        DateTime ocorridoEm,
        double picoG,
        bool orientacaoAnormal,
        bool pilotandoAntes,
        bool presencaAntes)
    {
        if (!double.IsFinite(picoG) || picoG <= 0)
        {
            throw new ValidacaoDominioException("O campo picoG deve ser um número maior que zero.");
        }

        return new EventoImpacto
        {
            DispositivoId = Garantir.IdInformado(dispositivoId, "dispositivoId"),
            OcorridoEm = Garantir.Utc(ocorridoEm, "ocorridoEm"),
            PicoG = picoG,
            OrientacaoAnormal = orientacaoAnormal,
            PilotandoAntes = pilotandoAntes,
            PresencaAntes = presencaAntes,
        };
    }

    // Os resultados não exigem a contagem iniciada: o capacete publica em QoS 0 e um evento
    // intermediário (como CONTAGEM_INICIADA) pode se perder. A API confia na máquina de estados
    // do capacete e só impede trocar um resultado que já é final.
    private void DefinirResultado(ResultadoImpacto resultado, string acao)
    {
        ExigirEmAberto(acao);
        Resultado = resultado;
    }

    private void ExigirEmAberto(string acao)
    {
        if (Resultado is { } atual)
        {
            throw new TransicaoInvalidaException(
                $"Não é possível {acao} um impacto com resultado {NomeContrato.Para(atual)}.");
        }
    }
}
