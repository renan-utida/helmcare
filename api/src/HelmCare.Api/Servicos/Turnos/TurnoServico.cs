using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.Turnos;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Risco;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Servicos.Turnos;

/// <summary>
/// Ciclo do turno (seção 7 do contrato, "Regras do turno"): início com o Índice de Risco e o
/// <c>ARMAR</c>, encerramento com o <c>DESARMAR</c>, alerta de queda e cancelamento do alerta.
/// </summary>
/// <remarks>
/// Quando há comando para o capacete, ele é publicado <b>antes</b> de gravar. Com o broker fora
/// (503), nada é gravado e o entregador pode simplesmente tentar de novo. Na ordem inversa,
/// ficaria um turno ativo com o capacete desarmado, e o segundo pedido daria 409 (turno ativo).
/// </remarks>
public sealed class TurnoServico
{
    private readonly ITurnoRepositorio _turnos;
    private readonly IEntregadorRepositorio _entregadores;
    private readonly IDispositivoRepositorio _dispositivos;
    private readonly IOcorrenciaRepositorio _ocorrencias;
    private readonly IPrevisaoTempo _previsao;
    private readonly IServicoRisco _risco;
    private readonly IPublicadorComandos _publicador;
    private readonly IUnidadeDeTrabalho _unidadeDeTrabalho;
    private readonly TimeProvider _relogio;

    /// <summary>
    /// Cria o serviço.
    /// </summary>
    /// <param name="turnos">Repositório de turnos.</param>
    /// <param name="entregadores">Repositório de entregadores.</param>
    /// <param name="dispositivos">Repositório de capacetes.</param>
    /// <param name="ocorrencias">Repositório de ocorrências.</param>
    /// <param name="previsao">Previsão do tempo do período do turno.</param>
    /// <param name="risco">Cálculo do Índice de Risco do Turno.</param>
    /// <param name="publicador">Comandos MQTT para o capacete.</param>
    /// <param name="unidadeDeTrabalho">Confirma a gravação.</param>
    /// <param name="relogio">Relógio do servidor.</param>
    public TurnoServico(
        ITurnoRepositorio turnos,
        IEntregadorRepositorio entregadores,
        IDispositivoRepositorio dispositivos,
        IOcorrenciaRepositorio ocorrencias,
        IPrevisaoTempo previsao,
        IServicoRisco risco,
        IPublicadorComandos publicador,
        IUnidadeDeTrabalho unidadeDeTrabalho,
        TimeProvider relogio)
    {
        _turnos = turnos;
        _entregadores = entregadores;
        _dispositivos = dispositivos;
        _ocorrencias = ocorrencias;
        _previsao = previsao;
        _risco = risco;
        _publicador = publicador;
        _unidadeDeTrabalho = unidadeDeTrabalho;
        _relogio = relogio;
    }

    /// <summary>
    /// Inicia o turno, calcula o Índice de Risco e, com <c>protecaoAtiva</c>, publica <c>ARMAR</c>.
    /// </summary>
    /// <param name="requisicao">Entregador, período e proteção.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O Índice de Risco do turno criado.</returns>
    /// <exception cref="ValidacaoDominioException">Campo ausente, <c>fimPrevisto</c> não posterior ao <c>inicio</c> ou turno de mais de 24 h (400).</exception>
    /// <exception cref="RecursoNaoEncontradoException">Entregador inexistente (404).</exception>
    /// <exception cref="RegraNegocioException">Turno ativo ou, com proteção, entregador sem capacete (409).</exception>
    /// <exception cref="MensageriaIndisponivelException">O <c>ARMAR</c> não pôde ser publicado; nada foi gravado (503).</exception>
    public async Task<RiscoTurnoResposta> IniciarAsync(IniciarTurnoRequisicao requisicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        // 400 antes de qualquer consulta: o domínio valida o período (fim depois do início, até 24 h).
        var turno = new Turno(
            requisicao.EntregadorId ?? Guid.Empty,
            ParaUtc(requisicao.Inicio ?? throw CampoObrigatorio("inicio")),
            ParaUtc(requisicao.FimPrevisto ?? throw CampoObrigatorio("fimPrevisto")),
            requisicao.ProtecaoAtiva);

        if (await _entregadores.ObterPorIdAsync(turno.EntregadorId, cancellationToken) is null)
        {
            throw new RecursoNaoEncontradoException("Entregador não encontrado.");
        }

        if (await _turnos.ExisteAtivoDoEntregadorAsync(turno.EntregadorId, cancellationToken))
        {
            throw new RegraNegocioException("O entregador já tem um turno ativo.");
        }

        Dispositivo? capacete = null;

        if (turno.ProtecaoAtiva)
        {
            capacete = await _dispositivos.ObterDoEntregadorAsync(turno.EntregadorId, cancellationToken)
                ?? throw new RegraNegocioException(
                    "Com protecaoAtiva, o entregador precisa ter um capacete vinculado. Vincule o capacete ou envie protecaoAtiva falso.");
        }

        PrevisaoTurno previsao = await _previsao.ObterAsync(turno.Inicio, turno.FimPrevisto, cancellationToken);
        ResultadoRisco risco = await _risco.CalcularAsync(turno.Inicio, turno.FimPrevisto, previsao, cancellationToken);
        turno.AplicarRisco(risco.IndiceRisco, risco.FaixaRisco, risco.Sinistro, risco.Chuva, risco.Calor, risco.Explicacao);

        if (capacete is not null)
        {
            await _publicador.PublicarAsync(capacete.Codigo, TipoComandoMqtt.Armar, cancellationToken);
        }

        await _turnos.AdicionarAsync(turno, cancellationToken);
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        return RiscoTurnoResposta.De(turno);
    }

    /// <summary>
    /// Devolve o Índice de Risco guardado no início do turno, sem calcular de novo.
    /// </summary>
    /// <param name="id">Id do turno.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O Índice de Risco do turno.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Turno inexistente (404).</exception>
    public async Task<RiscoTurnoResposta> ObterRiscoAsync(Guid id, CancellationToken cancellationToken)
    {
        Turno turno = await _turnos.ObterAsync(id, cancellationToken) ?? throw TurnoNaoEncontrado();
        return RiscoTurnoResposta.De(turno);
    }

    /// <summary>
    /// Encerra o turno. Sem alerta em andamento e com a proteção ativa, publica <c>DESARMAR</c> antes de gravar.
    /// </summary>
    /// <param name="id">Id do turno.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O turno encerrado.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Turno inexistente (404).</exception>
    /// <exception cref="TransicaoInvalidaException">Turno já encerrado (409).</exception>
    /// <exception cref="RegraNegocioException">Alerta de queda em andamento ou ocorrência ativa (409).</exception>
    /// <exception cref="MensageriaIndisponivelException">O <c>DESARMAR</c> não pôde ser publicado; nada foi gravado (503).</exception>
    public async Task<TurnoResposta> EncerrarAsync(Guid id, CancellationToken cancellationToken)
    {
        // Rastreado: é o único caso de uso que altera o turno.
        Turno turno = await _turnos.ObterPorIdAsync(id, cancellationToken) ?? throw TurnoNaoEncontrado();

        if (!turno.EstaAtivo)
        {
            throw new TransicaoInvalidaException("O turno já foi encerrado.");
        }

        Dispositivo? capacete = await _dispositivos.ObterDoEntregadorAsync(turno.EntregadorId, cancellationToken);

        if (capacete is not null && AlertaEmAndamento(capacete.EstadoAtual))
        {
            throw new RegraNegocioException(
                $"Há um alerta de queda em andamento (capacete em {NomeContrato.Para(capacete.EstadoAtual)}). "
                + "O turno só pode ser encerrado depois que o alerta terminar.");
        }

        if (await _ocorrencias.ExisteAtivaDoEntregadorAsync(turno.EntregadorId, cancellationToken))
        {
            throw new RegraNegocioException(
                "O entregador tem uma ocorrência ativa. Encerre ou cancele a ocorrência antes de encerrar o turno.");
        }

        // Decidido antes, porque o Encerrar desliga a proteção.
        Dispositivo? capaceteParaDesarmar = turno.ProtecaoAtiva ? capacete : null;

        // O domínio valida o instante antes da publicação; se o DESARMAR falhar, a alteração em
        // memória é descartada com o escopo da requisição, sem chegar ao banco.
        turno.Encerrar(_relogio.GetUtcNow().UtcDateTime);

        // Também vai quando o último estado conhecido é CANCELADO ou FALSO_POSITIVO, que no capacete
        // já voltaram para ARMADO. Fora de ARMADO o capacete ignora o DESARMAR (seção 6.3).
        if (capaceteParaDesarmar is not null)
        {
            await _publicador.PublicarAsync(capaceteParaDesarmar.Codigo, TipoComandoMqtt.Desarmar, cancellationToken);
        }

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);

        return TurnoResposta.De(turno);
    }

    /// <summary>
    /// Estado do alerta de queda para a tela de alerta do app (exemplo do <c>GET /turnos/{id}/alerta</c>).
    /// </summary>
    /// <param name="id">Id do turno.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>O estado do alerta.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Turno inexistente (404).</exception>
    /// <exception cref="RegraNegocioException">Entregador sem capacete (409).</exception>
    public async Task<AlertaTurnoResposta> ObterAlertaAsync(Guid id, CancellationToken cancellationToken)
    {
        Turno turno = await _turnos.ObterAsync(id, cancellationToken) ?? throw TurnoNaoEncontrado();

        DadosAlertaTurno dados = await _turnos.ObterDadosAlertaAsync(turno.EntregadorId, cancellationToken)
            ?? throw SemCapacete();

        // O contagemTerminaEm vem do relógio do capacete (no Wokwi, atrasado): ele é repassado como
        // está, sem comparar com a hora do servidor. Quem decide se a contagem acabou é o estado.
        return new AlertaTurnoResposta
        {
            EstadoCapacete = dados.EstadoCapacete,
            ContagemTerminaEm = dados.EstadoCapacete == EstadoDispositivo.Contagem ? dados.ContagemTerminaEm : null,
            OcorrenciaId = dados.OcorrenciaAtivaId,
            IndiceCalorC = dados.IndiceCalorC,
        };
    }

    /// <summary>
    /// Cancela o alerta durante a contagem, publicando <c>CANCELAR</c>. Não espera a resposta do capacete (202).
    /// </summary>
    /// <param name="id">Id do turno.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <returns>A tarefa da operação.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Turno inexistente (404).</exception>
    /// <exception cref="TransicaoInvalidaException">Turno encerrado ou capacete fora de <c>CONTAGEM</c> (409).</exception>
    /// <exception cref="RegraNegocioException">Entregador sem capacete (409).</exception>
    /// <exception cref="MensageriaIndisponivelException">O <c>CANCELAR</c> não pôde ser publicado (503).</exception>
    public async Task CancelarAlertaAsync(Guid id, CancellationToken cancellationToken)
    {
        Turno turno = await _turnos.ObterAsync(id, cancellationToken) ?? throw TurnoNaoEncontrado();

        if (!turno.EstaAtivo)
        {
            throw new TransicaoInvalidaException("O turno já foi encerrado.");
        }

        Dispositivo capacete = await _dispositivos.ObterDoEntregadorAsync(turno.EntregadorId, cancellationToken)
            ?? throw SemCapacete();

        // A decisão é só pelo estado, nunca pelo contagemTerminaEm (relógio do capacete). Se o
        // CANCELAR chegar depois do fim da contagem, quem o ignora é o capacete (seção 6.3).
        if (capacete.EstadoAtual != EstadoDispositivo.Contagem)
        {
            throw new TransicaoInvalidaException(
                $"O alerta só pode ser cancelado durante a contagem; o capacete está em {NomeContrato.Para(capacete.EstadoAtual)}.");
        }

        await _publicador.PublicarAsync(capacete.Codigo, TipoComandoMqtt.Cancelar, cancellationToken);
    }

    // IMPACTO_DETECTADO entra porque é o estado que a API registra no começo da observação: o
    // capacete publica o evento IMPACTO_DETECTADO e só a telemetria seguinte traz OBSERVACAO.
    private static bool AlertaEmAndamento(EstadoDispositivo estado)
    {
        return estado is EstadoDispositivo.ImpactoDetectado
            or EstadoDispositivo.Observacao
            or EstadoDispositivo.Contagem
            or EstadoDispositivo.SosEnviado;
    }

    // O System.Text.Json entrega DateTime Utc para "Z", Local para "-03:00" (já convertido para o
    // fuso da máquina) e Unspecified sem fuso. Sem fuso vale como UTC, como no leitor do MQTT.
    private static DateTime ParaUtc(DateTime instante)
    {
        return instante.Kind switch
        {
            DateTimeKind.Utc => instante,
            DateTimeKind.Local => instante.ToUniversalTime(),
            _ => DateTime.SpecifyKind(instante, DateTimeKind.Utc),
        };
    }

    private static ValidacaoDominioException CampoObrigatorio(string campo)
    {
        return new ValidacaoDominioException($"O campo {campo} é obrigatório.");
    }

    private static RecursoNaoEncontradoException TurnoNaoEncontrado()
    {
        return new RecursoNaoEncontradoException("Turno não encontrado.");
    }

    private static RegraNegocioException SemCapacete()
    {
        return new RegraNegocioException("O entregador do turno não tem capacete vinculado.");
    }
}
