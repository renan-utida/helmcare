using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Ocorrencias;
using HelmCare.Api.Seguranca;
using HelmCare.Api.Servicos.Ocorrencias;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.ObjetosDeValor;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes da <c>distanciaVoluntarioKm</c> e do <c>encerradaAutomaticamente</c> no corpo da ocorrência
/// (seção 7 do contrato, 1.15): a distância só vai para a vítima, depois do aceite, e nunca a posição.
/// </summary>
[TestClass]
public sealed class DistanciaVoluntarioTests
{
    private readonly FakeOcorrenciaRepositorio _ocorrencias = new();
    private readonly FakeVoluntarioRepositorio _voluntarios = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(CenarioSos.Aceite.AddSeconds(30)));
    private readonly Guid _vitimaId = Guid.NewGuid();
    private readonly Guid _entregadorDoVoluntario = Guid.NewGuid();
    private readonly Voluntario _voluntario;
    private readonly Ocorrencia _ocorrencia;

    /// <summary>
    /// Semeia a ocorrência da vítima aceita por um voluntário na posição próxima de referência.
    /// </summary>
    public DistanciaVoluntarioTests()
    {
        _voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima, _entregadorDoVoluntario);
        _voluntarios.Semear(_voluntario);
        _ocorrencia = CenarioSos.AbrirOcorrencia(_vitimaId);
        _ocorrencias.Semear(_ocorrencia);
    }

    /// <summary>
    /// Garante a distância para a vítima depois do aceite: da posição do voluntário até a localização
    /// exata, com 1 casa (0,46 km arredondado para 0,5).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_VitimaDepoisDoAceite_DistanciaComUmaCasa()
    {
        // Arrange
        Aceitar();
        double esperado = Math.Round(CenarioSos.PosicaoProxima.DistanciaKmAte(CenarioSos.Local), 1, MidpointRounding.AwayFromZero);

        // Act
        OcorrenciaResposta resposta = await Servico(_vitimaId).ObterAsync(_ocorrencia.Id, null, CancellationToken.None);

        // Assert
        Assert.AreEqual(esperado, resposta.DistanciaVoluntarioKm);
        Assert.AreEqual(0.5, resposta.DistanciaVoluntarioKm);
        Assert.IsFalse(resposta.EncerradaAutomaticamente);
    }

    /// <summary>
    /// Garante que a distância é nula antes do aceite, mesmo para a vítima.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_VitimaAntesDoAceite_DistanciaNula()
    {
        // Act
        OcorrenciaResposta resposta = await Servico(_vitimaId).ObterAsync(_ocorrencia.Id, null, CancellationToken.None);

        // Assert
        Assert.IsNull(resposta.DistanciaVoluntarioKm);
    }

    /// <summary>
    /// Garante que outro usuário (o voluntário que aceitou ou outro voluntário) recebe a distância nula.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_OutroUsuario_DistanciaNula()
    {
        // Arrange
        Aceitar();
        Voluntario outro = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoDistante);
        _voluntarios.Semear(outro);

        // Act
        OcorrenciaResposta doVoluntario = await Servico(_entregadorDoVoluntario).ObterAsync(_ocorrencia.Id, _voluntario.Id, CancellationToken.None);
        OcorrenciaResposta deOutro = await Servico(outro.EntregadorId).ObterAsync(_ocorrencia.Id, outro.Id, CancellationToken.None);

        // Assert
        Assert.IsNull(doVoluntario.DistanciaVoluntarioKm);
        Assert.IsTrue(doVoluntario.LocalizacaoExata);
        Assert.IsNull(deOutro.DistanciaVoluntarioKm);
    }

    /// <summary>
    /// Garante a distância nula quando o voluntário que aceitou ficou indisponível (posição apagada).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_VoluntarioSemPosicao_DistanciaNula()
    {
        // Arrange
        Aceitar();
        _voluntario.AtualizarDisponibilidade(false, null);

        // Act
        OcorrenciaResposta resposta = await Servico(_vitimaId).ObterAsync(_ocorrencia.Id, null, CancellationToken.None);

        // Assert
        Assert.IsNull(resposta.DistanciaVoluntarioKm);
    }

    /// <summary>
    /// Garante a distância medida da última posição do voluntário, não da posição no aceite.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_VoluntarioSeMoveu_UsaAUltimaPosicao()
    {
        // Arrange
        Aceitar();
        var maisPerto = new Coordenada(-23.5636, -46.6530);
        _voluntario.AtualizarDisponibilidade(true, maisPerto);
        double esperado = Math.Round(maisPerto.DistanciaKmAte(CenarioSos.Local), 1, MidpointRounding.AwayFromZero);

        // Act
        OcorrenciaResposta resposta = await Servico(_vitimaId).ObterAsync(_ocorrencia.Id, null, CancellationToken.None);

        // Assert
        Assert.AreEqual(esperado, resposta.DistanciaVoluntarioKm);
    }

    private void Aceitar()
    {
        _ocorrencia.Aceitar(_voluntario, CenarioSos.Aceite, CenarioSos.RaioCamada1Km, CenarioSos.TrechoCapacetePadrao);
    }

    private OcorrenciaServico Servico(Guid entregadorDoToken)
    {
        var autorizacao = new AutorizacaoRecursos(FakeUsuarioAtual.Entregador(entregadorDoToken), _voluntarios);
        return new OcorrenciaServico(_ocorrencias, _voluntarios, autorizacao, _relogio, Options.Create(new OpcoesHelmCare()));
    }
}
