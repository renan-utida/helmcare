using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Mensageria;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace HelmCare.Testes.Mensageria;

/// <summary>
/// Testes do SOS repetido no <see cref="RoteadorMensagemMqtt"/> (seção 6.2): a duplicata não grava
/// nada, mas refaz o comando conforme o estado da ocorrência mais recente do entregador.
/// </summary>
[TestClass]
public sealed class SosRepetidoTests
{
    private static readonly DateTime _ts = CenarioMqtt.TsImpacto;

    private readonly CenarioMqtt _cenario = new();

    /// <summary>
    /// Garante o comando refeito por estado: SOCORRO_A_CAMINHO republica SOCORRO_A_CAMINHO; ENCERRADA
    /// e CANCELADA republicam ENCERRAR. Nada é gravado e o log leva só o código, o comando e o id.
    /// </summary>
    /// <param name="estado">Estado da ocorrência quando o SOS se repete.</param>
    /// <param name="comando">Comando esperado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoOcorrencia.SocorroACaminho, TipoComandoMqtt.SocorroACaminho)]
    [DataRow(EstadoOcorrencia.Encerrada, TipoComandoMqtt.Encerrar)]
    [DataRow(EstadoOcorrencia.Cancelada, TipoComandoMqtt.Encerrar)]
    public async Task ProcessarAsync_SosRepetido_RefazOComandoSemGravar(EstadoOcorrencia estado, TipoComandoMqtt comando)
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);
        Ocorrencia ocorrencia = _cenario.Ocorrencias.Ocorrencias.Single();
        Levar(ocorrencia, estado);

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Duplicada, resultado);
        Assert.AreEqual(new ComandoPublicadoFake(CenarioMqtt.Codigo, comando), _cenario.Publicador.Comandos.Single());
        Assert.AreEqual(1, _cenario.Unidade.VezesSalvo);
        Assert.HasCount(1, _cenario.Ocorrencias.Ocorrencias);
        Assert.HasCount(1, _cenario.Processados.Registros);
        RegistroLogFake refeito = _cenario.LogRoteador.Registros.Single(r => r.Nivel == LogLevel.Information);
        Assert.AreEqual(
            $"SOS repetido do capacete HC-0001 (duplicata, nada gravado): comando {NomeDe(comando)} refeito para a ocorrência {ocorrencia.Id}.",
            refeito.Mensagem);
    }

    /// <summary>
    /// Garante que, aguardando aceite (camada 1 ou 2), o SOS repetido não publica nada: o capacete
    /// deve continuar reenviando.
    /// </summary>
    /// <param name="estado">Estado da ocorrência.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoOcorrencia.SosCamada1)]
    [DataRow(EstadoOcorrencia.SosCamada2)]
    public async Task ProcessarAsync_SosRepetidoAguardandoAceite_NaoPublica(EstadoOcorrencia estado)
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);
        Levar(_cenario.Ocorrencias.Ocorrencias.Single(), estado);

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Duplicada, resultado);
        Assert.IsEmpty(_cenario.Publicador.Comandos);
        Assert.AreEqual(1, _cenario.Unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que a falha do publicador no comando refeito não lança: a duplicata segue e o aviso vai para o log.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_SosRepetidoComBrokerFora_NaoLanca()
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);
        Levar(_cenario.Ocorrencias.Ocorrencias.Single(), EstadoOcorrencia.Encerrada);
        _cenario.Publicador.Indisponivel = true;

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Duplicada, resultado);
        Assert.IsEmpty(_cenario.Publicador.Comandos);
        Assert.AreEqual(LogLevel.Warning, _cenario.LogComandos.Registros.Single().Nivel);
        Assert.AreEqual(1, _cenario.Unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que a duplicata pela corrida (índice único na gravação) também refaz o comando.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_SosDuplicadoNaCorrida_RefazOComando()
    {
        // Arrange: a primeira cópia já foi gravada e aceita; a segunda esbarra no índice único.
        Ocorrencia daVitima = Ocorrencia.Abrir(_cenario.Entregador.Id, CenarioSos.NovoEventoEmAberto(), CenarioSos.Local, CenarioSos.Sos);
        Levar(daVitima, EstadoOcorrencia.SocorroACaminho);
        _cenario.Ocorrencias.Semear(daVitima);
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        _cenario.Unidade.FalhaAoSalvar = () => new DbUpdateException(
            "duplicado",
            new PostgresException("duplicado", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Duplicada, resultado);
        Assert.AreEqual(TipoComandoMqtt.SocorroACaminho, _cenario.Publicador.Comandos.Single().Tipo);
    }

    /// <summary>
    /// Garante que a duplicata de outro tipo de evento não publica nada, mesmo com a ocorrência encerrada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_OutroEventoDuplicado_NaoPublica()
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);
        Levar(_cenario.Ocorrencias.Ocorrencias.Single(), EstadoOcorrencia.Encerrada);
        await roteador.ProcessarAsync(CenarioMqtt.Evento("IMPACTO_DETECTADO", _ts.AddMinutes(5)), CancellationToken.None);

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("IMPACTO_DETECTADO", _ts.AddMinutes(5)), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Duplicada, resultado);
        Assert.IsEmpty(_cenario.Publicador.Comandos);
    }

    private static string NomeDe(TipoComandoMqtt comando)
    {
        return comando == TipoComandoMqtt.Encerrar ? "ENCERRAR" : "SOCORRO_A_CAMINHO";
    }

    // Leva a ocorrência aberta pelo SOS até o estado pedido, pelo caminho normal da máquina de estados.
    private static void Levar(Ocorrencia ocorrencia, EstadoOcorrencia estado)
    {
        DateTime agora = ocorrencia.CriadaEm.AddSeconds(70);
        var tempo = TimeSpan.FromSeconds(60);

        switch (estado)
        {
            case EstadoOcorrencia.SosCamada1:
                break;
            case EstadoOcorrencia.SosCamada2:
                ocorrencia.EscalarParaCamada2(agora, tempo);
                break;
            case EstadoOcorrencia.SocorroACaminho:
                ocorrencia.Aceitar(CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima), agora, 2, tempo);
                break;
            case EstadoOcorrencia.Encerrada:
                ocorrencia.Encerrar(agora);
                break;
            case EstadoOcorrencia.Cancelada:
                ocorrencia.Cancelar(agora);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado sem cenário.");
        }

    }
}
