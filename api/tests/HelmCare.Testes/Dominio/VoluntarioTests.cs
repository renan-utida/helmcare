using System;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Dominio.ObjetosDeValor;
using HelmCare.Testes.Construtores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes da disponibilidade e da posição do <see cref="Voluntario"/>.
/// </summary>
[TestClass]
public sealed class VoluntarioTests
{
    /// <summary>
    /// Garante que o voluntário nasce indisponível e sem posição.
    /// </summary>
    [TestMethod]
    public void Construtor_DadosValidos_ComecaIndisponivelSemPosicao()
    {
        // Arrange e Act
        var voluntario = new Voluntario(Guid.NewGuid(), TipoVoluntario.VigiaMoto);

        // Assert
        Assert.IsFalse(voluntario.Disponivel);
        Assert.IsNull(voluntario.Latitude);
        Assert.IsNull(voluntario.Longitude);
    }

    /// <summary>
    /// Garante que ficar disponível sem posição é rejeitado.
    /// </summary>
    [TestMethod]
    public void AtualizarDisponibilidade_DisponivelSemPosicao_LancaValidacao()
    {
        // Arrange
        var voluntario = new Voluntario(Guid.NewGuid(), TipoVoluntario.Entregador);

        // Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(() => voluntario.AtualizarDisponibilidade(true, null));
        Assert.IsFalse(voluntario.Disponivel);
    }

    /// <summary>
    /// Garante que ficar disponível guarda a posição exata.
    /// </summary>
    [TestMethod]
    public void AtualizarDisponibilidade_DisponivelComPosicao_GuardaPosicao()
    {
        // Arrange
        var voluntario = new Voluntario(Guid.NewGuid(), TipoVoluntario.Entregador);

        // Act
        voluntario.AtualizarDisponibilidade(true, new Coordenada(-23.5505, -46.6333));

        // Assert
        Assert.IsTrue(voluntario.Disponivel);
        Assert.AreEqual(-23.5505, voluntario.Latitude);
        Assert.AreEqual(-46.6333, voluntario.Longitude);
    }

    /// <summary>
    /// Garante que ficar indisponível apaga a posição, mesmo que uma seja enviada.
    /// </summary>
    [TestMethod]
    public void AtualizarDisponibilidade_Indisponivel_ApagaPosicao()
    {
        // Arrange
        var voluntario = new Voluntario(Guid.NewGuid(), TipoVoluntario.Entregador);
        var posicao = new Coordenada(-23.5505, -46.6333);
        voluntario.AtualizarDisponibilidade(true, posicao);

        // Act
        voluntario.AtualizarDisponibilidade(false, posicao);

        // Assert
        Assert.IsFalse(voluntario.Disponivel);
        Assert.IsNull(voluntario.Latitude);
        Assert.IsNull(voluntario.Longitude);
    }

    /// <summary>
    /// Garante que um voluntário que ficou indisponível não pode atender, mesmo perto da queda.
    /// </summary>
    [TestMethod]
    public void PodeAtender_Indisponivel_DevolveFalse()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima);
        voluntario.AtualizarDisponibilidade(false, null);

        // Act
        bool pode = voluntario.PodeAtender(ocorrencia, CenarioSos.RaioCamada1Km);

        // Assert
        Assert.IsFalse(pode);
    }

    /// <summary>
    /// Garante que um voluntário recém-cadastrado, sem posição, não pode atender.
    /// </summary>
    [TestMethod]
    public void PodeAtender_SemPosicao_DevolveFalse()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(EstadoOcorrencia.SosCamada2);
        var voluntario = new Voluntario(Guid.NewGuid(), TipoVoluntario.Entregador);

        // Act
        bool pode = voluntario.PodeAtender(ocorrencia, CenarioSos.RaioCamada1Km);

        // Assert
        Assert.IsNull(voluntario.Latitude);
        Assert.IsFalse(pode);
    }

    /// <summary>
    /// Garante que o voluntário não pode atender a ocorrência do próprio entregador.
    /// </summary>
    [TestMethod]
    public void PodeAtender_PropriaOcorrencia_DevolveFalse()
    {
        // Arrange
        Guid entregadorId = Guid.NewGuid();
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia(entregadorId);
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima, entregadorId);

        // Act
        bool pode = voluntario.PodeAtender(ocorrencia, CenarioSos.RaioCamada1Km);

        // Assert
        Assert.IsFalse(pode);
    }

    /// <summary>
    /// Garante que ocorrências que não estão aguardando aceite não podem ser atendidas.
    /// </summary>
    /// <param name="estado">Estado fora da espera.</param>
    [TestMethod]
    [DataRow(EstadoOcorrencia.SocorroACaminho)]
    [DataRow(EstadoOcorrencia.Encerrada)]
    [DataRow(EstadoOcorrencia.Cancelada)]
    public void PodeAtender_OcorrenciaNaoAguardando_DevolveFalse(EstadoOcorrencia estado)
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(estado);
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima);

        // Act
        bool pode = voluntario.PodeAtender(ocorrencia, CenarioSos.RaioCamada1Km);

        // Assert
        Assert.IsFalse(pode);
    }

    /// <summary>
    /// Garante que, na camada 1, o voluntário a 2,47 km fica fora do raio de 2 km.
    /// </summary>
    [TestMethod]
    public void PodeAtender_ForaDoRaioNaCamada1_DevolveFalse()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoDistante);

        // Act
        bool pode = voluntario.PodeAtender(ocorrencia, CenarioSos.RaioCamada1Km);

        // Assert
        Assert.IsFalse(pode);
    }

    /// <summary>
    /// Garante que, na camada 1, o voluntário a 0,47 km pode atender.
    /// </summary>
    [TestMethod]
    public void PodeAtender_DentroDoRaioNaCamada1_DevolveTrue()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima);

        // Act
        bool pode = voluntario.PodeAtender(ocorrencia, CenarioSos.RaioCamada1Km);

        // Assert
        Assert.IsTrue(pode);
    }

    /// <summary>
    /// Garante que, na camada 2, o raio não vale e o voluntário distante pode atender.
    /// </summary>
    [TestMethod]
    public void PodeAtender_Camada2_IgnoraORaio()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(EstadoOcorrencia.SosCamada2);
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoDistante);

        // Act
        bool pode = voluntario.PodeAtender(ocorrencia, CenarioSos.RaioCamada1Km);

        // Assert
        Assert.IsTrue(pode);
    }

    /// <summary>
    /// Garante que a distância exatamente igual ao raio é aceita (limite inclusivo).
    /// </summary>
    [TestMethod]
    public void PodeAtender_DistanciaIgualAoRaio_DevolveTrue()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoDistante);
        double raioExato = CenarioSos.PosicaoDistante.DistanciaKmAte(CenarioSos.Local);

        // Act
        bool pode = voluntario.PodeAtender(ocorrencia, raioExato);

        // Assert
        Assert.IsTrue(pode);
    }

    /// <summary>
    /// Garante que raio negativo ou NaN é rejeitado.
    /// </summary>
    /// <param name="raio">Raio inválido.</param>
    [TestMethod]
    [DataRow(-0.1)]
    [DataRow(double.NaN)]
    public void PodeAtender_RaioInvalido_LancaValidacao(double raio)
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima);

        // Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(() => voluntario.PodeAtender(ocorrencia, raio));
    }
}
