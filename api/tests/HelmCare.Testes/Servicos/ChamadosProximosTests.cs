using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Ocorrencias;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Ocorrencias;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Dominio.ObjetosDeValor;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="OcorrenciaServico.ListarProximasAsync"/>: quem vê cada chamado (raio da
/// camada 1, camada 2, próprio SOS, estados), localização aproximada, distância e ordem.
/// </summary>
[TestClass]
public sealed class ChamadosProximosTests
{
    private readonly FakeOcorrenciaRepositorio _ocorrencias = new();
    private readonly FakeVoluntarioRepositorio _voluntarios = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(CenarioSos.Sos.AddSeconds(20)));

    /// <summary>
    /// Garante que um chamado da camada 1 dentro do raio aparece, com a localização aproximada
    /// (2 casas), a distância até ela (0,63 km dá 0,6) e o cronômetro do GET /ocorrencias/{id}.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ListarProximasAsync_Camada1DentroDoRaio_ApareceComLocalizacaoAproximada()
    {
        // Arrange
        Ocorrencia ocorrencia = Semear(CenarioSos.AbrirOcorrencia());
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoProxima);

        // Act
        IReadOnlyList<OcorrenciaProximaResposta> chamados = await Servico().ListarProximasAsync(voluntario.Id, CancellationToken.None);

        // Assert
        OcorrenciaProximaResposta chamado = chamados.Single();
        Assert.AreEqual(ocorrencia.Id, chamado.OcorrenciaId);
        Assert.AreEqual(EstadoOcorrencia.SosCamada1, chamado.Estado);
        Assert.AreEqual(1, chamado.Camada);
        Assert.AreEqual(CenarioSos.Sos, chamado.CriadaEm);
        Assert.AreEqual(-23.56, chamado.Latitude);
        Assert.AreEqual(-46.65, chamado.Longitude);
        Assert.AreEqual(0.6, chamado.DistanciaKm);
        Assert.AreEqual(45, chamado.Cronometro.SegundosDesdeQueda);
        Assert.AreEqual(120, chamado.Cronometro.MetaSegundos);
        Assert.IsFalse(chamado.Cronometro.Parado);
    }

    /// <summary>
    /// Garante o raio inclusivo: com o raio igual à distância exata até a queda, o chamado aparece;
    /// um pouco menor, não aparece. O filtro usa a posição exata, e não a aproximada.
    /// </summary>
    /// <param name="folgaKm">Quanto somar à distância exata para formar o raio.</param>
    /// <param name="aparece">Se o chamado deve aparecer.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(0.0, true)]
    [DataRow(-0.000001, false)]
    public async Task ListarProximasAsync_NoLimiteDoRaio_InclusivoPelaPosicaoExata(double folgaKm, bool aparece)
    {
        // Arrange
        Semear(CenarioSos.AbrirOcorrencia());
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoDistante);
        double raio = CenarioSos.PosicaoDistante.DistanciaKmAte(CenarioSos.Local) + folgaKm;

        // Act
        IReadOnlyList<OcorrenciaProximaResposta> chamados = await Servico(raio).ListarProximasAsync(voluntario.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(aparece ? 1 : 0, chamados.Count);
    }

    /// <summary>
    /// Garante que, com o raio padrão de 2 km, o voluntário a 2,47 km não vê a camada 1, mas vê a camada 2.
    /// </summary>
    /// <param name="estado">Estado da ocorrência.</param>
    /// <param name="aparece">Se o chamado deve aparecer.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoOcorrencia.SosCamada1, false)]
    [DataRow(EstadoOcorrencia.SosCamada2, true)]
    public async Task ListarProximasAsync_ForaDoRaio_SoACamada2Aparece(EstadoOcorrencia estado, bool aparece)
    {
        // Arrange
        Semear(CenarioSos.OcorrenciaEm(estado));
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoDistante);

        // Act
        IReadOnlyList<OcorrenciaProximaResposta> chamados = await Servico().ListarProximasAsync(voluntario.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(aparece ? 1 : 0, chamados.Count);
    }

    /// <summary>
    /// Garante que o voluntário indisponível recebe a lista vazia, mesmo com chamados na camada 2.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ListarProximasAsync_VoluntarioIndisponivel_ListaVazia()
    {
        // Arrange
        Semear(CenarioSos.OcorrenciaEm(EstadoOcorrencia.SosCamada2));
        var voluntario = new Voluntario(Guid.NewGuid(), TipoVoluntario.Entregador);
        _voluntarios.Semear(voluntario);

        // Act
        IReadOnlyList<OcorrenciaProximaResposta> chamados = await Servico().ListarProximasAsync(voluntario.Id, CancellationToken.None);

        // Assert
        Assert.IsEmpty(chamados);
    }

    /// <summary>
    /// Garante que o entregador nunca vê o próprio SOS, nem na camada 1 dentro do raio nem na camada 2.
    /// </summary>
    /// <param name="estado">Estado da ocorrência do próprio entregador.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoOcorrencia.SosCamada1)]
    [DataRow(EstadoOcorrencia.SosCamada2)]
    public async Task ListarProximasAsync_ProprioSos_NuncaAparece(EstadoOcorrencia estado)
    {
        // Arrange
        Guid entregadorId = Guid.NewGuid();
        Ocorrencia propria = CenarioSos.AbrirOcorrencia(entregadorId);
        if (estado == EstadoOcorrencia.SosCamada2)
        {
            propria.EscalarParaCamada2(CenarioSos.Sos + CenarioSos.TempoAceiteCamada1, CenarioSos.TempoAceiteCamada1);
        }

        Semear(propria);
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoProxima, entregadorId);

        // Act
        IReadOnlyList<OcorrenciaProximaResposta> chamados = await Servico().ListarProximasAsync(voluntario.Id, CancellationToken.None);

        // Assert
        Assert.IsEmpty(chamados);
    }

    /// <summary>
    /// Garante que ocorrências fora da espera (aceita, encerrada ou cancelada) não aparecem.
    /// </summary>
    /// <param name="estado">Estado da ocorrência.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(EstadoOcorrencia.SocorroACaminho)]
    [DataRow(EstadoOcorrencia.Encerrada)]
    [DataRow(EstadoOcorrencia.Cancelada)]
    public async Task ListarProximasAsync_ForaDaEspera_NaoAparece(EstadoOcorrencia estado)
    {
        // Arrange
        Semear(CenarioSos.OcorrenciaEm(estado));
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoProxima);

        // Act
        IReadOnlyList<OcorrenciaProximaResposta> chamados = await Servico().ListarProximasAsync(voluntario.Id, CancellationToken.None);

        // Assert
        Assert.IsEmpty(chamados);
    }

    /// <summary>
    /// Garante a ordem pela distância mostrada e, no empate (mesma localização aproximada), pelo criadaEm.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ListarProximasAsync_VariosChamados_OrdemPorDistanciaEDepoisCriadaEm()
    {
        // Arrange: A e C caem na mesma localização aproximada (-23.56, -46.65); B fica na Sé (-23.55, -46.63).
        Ocorrencia a = Semear(Abrir(new Coordenada(-23.5641, -46.6525), CenarioSos.Sos));
        Ocorrencia b = Semear(Abrir(new Coordenada(-23.5505, -46.6333), CenarioSos.Sos.AddSeconds(-10)));
        Ocorrencia c = Semear(Abrir(new Coordenada(-23.5649, -46.6510), CenarioSos.Sos.AddSeconds(-5)));
        Voluntario voluntario = SemearVoluntario(CenarioSos.PosicaoProxima);

        // Act
        IReadOnlyList<OcorrenciaProximaResposta> chamados = await Servico(raioKm: 50).ListarProximasAsync(voluntario.Id, CancellationToken.None);

        // Assert
        Assert.AreEqual(
            string.Join(' ', new[] { c.Id, a.Id, b.Id }),
            string.Join(' ', chamados.Select(x => x.OcorrenciaId)));
        Assert.AreEqual("0.6 0.6 2.9", string.Join(' ', chamados.Select(x => x.DistanciaKm.ToString(CultureInfo.InvariantCulture))));
    }

    /// <summary>
    /// Garante o 404 para voluntário inexistente.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ListarProximasAsync_VoluntarioInexistente_Lanca404()
    {
        // Act
        RecursoNaoEncontradoException excecao = await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => Servico().ListarProximasAsync(Guid.NewGuid(), CancellationToken.None));

        // Assert
        Assert.AreEqual("Voluntário não encontrado.", excecao.Message);
    }

    private static Ocorrencia Abrir(Coordenada local, DateTime criadaEm)
    {
        return Ocorrencia.Abrir(Guid.NewGuid(), CenarioSos.NovoEventoEmAberto(), local, criadaEm);
    }

    private OcorrenciaServico Servico(double raioKm = CenarioSos.RaioCamada1Km)
    {
        var opcoes = new OpcoesHelmCare { RaioCamada1Km = raioKm };
        return new OcorrenciaServico(_ocorrencias, _voluntarios, new FakeAutorizacaoLivre(), _relogio, Options.Create(opcoes));
    }

    private Ocorrencia Semear(Ocorrencia ocorrencia)
    {
        _ocorrencias.Semear(ocorrencia);
        return ocorrencia;
    }

    private Voluntario SemearVoluntario(Coordenada posicao, Guid? entregadorId = null)
    {
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(posicao, entregadorId);
        _voluntarios.Semear(voluntario);
        return voluntario;
    }
}
