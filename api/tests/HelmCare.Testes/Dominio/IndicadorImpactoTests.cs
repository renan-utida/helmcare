using System;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes da regra do indicador de impacto da seção 7 do contrato 1.7 (dois trechos, cada um no
/// próprio relógio), pelo <see cref="Ocorrencia.Aceitar"/> e pelo <see cref="Ocorrencia.SegundosDesdeAQueda"/>.
/// </summary>
[TestClass]
public sealed class IndicadorImpactoTests
{
    private static readonly TimeSpan Trecho = CenarioSos.TrechoCapacetePadrao;
    private static readonly TimeSpan Contagem = CenarioSos.Contagem;

    /// <summary>
    /// Garante o caso do teste de 12/11: 26 s no relógio do capacete (atrasado) mais 30 s no do servidor dão 56.
    /// </summary>
    [TestMethod]
    public void Aceitar_CasoDe12DeNovembro_SomaOsDoisTrechos()
    {
        // Arrange: capacete com a queda às 23:37:52 e a contagem até 23:38:18; servidor abre às 23:40:13.
        EventoImpacto impacto = ImpactoComContagem(Utc(23, 37, 52), inicioContagem: Utc(23, 38, 3));
        DateTime criadaEm = Utc(23, 40, 13);
        Ocorrencia ocorrencia = Abrir(impacto, criadaEm);

        // Act
        Aceitar(ocorrencia, criadaEm.AddSeconds(30));

        // Assert
        Assert.AreEqual(Utc(23, 38, 18), impacto.ContagemTerminaEm);
        Assert.AreEqual(56, ocorrencia.TempoAteAceiteSegundos);
    }

    /// <summary>
    /// Garante que, com os relógios sincronizados, o resultado é o mesmo de contar direto da queda até o aceite.
    /// </summary>
    [TestMethod]
    public void Aceitar_RelogiosSincronizados_IgualAoAceiteMenosAQueda()
    {
        // Arrange: queda às 21:15:37, contagem até 21:16:02, e o servidor abre no mesmo instante.
        DateTime queda = Utc(21, 15, 37);
        EventoImpacto impacto = ImpactoComContagem(queda, inicioContagem: Utc(21, 15, 47));
        Ocorrencia ocorrencia = Abrir(impacto, impacto.ContagemTerminaEm!.Value);
        DateTime aceite = Utc(21, 17, 15);

        // Act
        Aceitar(ocorrencia, aceite);

        // Assert
        Assert.AreEqual(98, ocorrencia.TempoAteAceiteSegundos);
        Assert.AreEqual((int)(aceite - queda).TotalSeconds, ocorrencia.TempoAteAceiteSegundos);
    }

    /// <summary>
    /// Garante que o impacto estimado a partir do SOS usa o trecho padrão (25 s) mais o trecho do servidor.
    /// </summary>
    [TestMethod]
    public void Aceitar_ImpactoEstimado_UsaOTrechoPadrao()
    {
        // Arrange
        EventoImpacto impacto = EventoImpacto.RegistrarEstimadoPorSos(
            Guid.NewGuid(), Utc(21, 16, 2), Trecho, 4.8, true, true, true);
        DateTime criadaEm = Utc(21, 16, 5);
        Ocorrencia ocorrencia = Abrir(impacto, criadaEm);

        // Act
        Aceitar(ocorrencia, criadaEm.AddSeconds(40));

        // Assert
        Assert.IsTrue(impacto.OcorridoEmEstimado);
        Assert.AreEqual(65, ocorrencia.TempoAteAceiteSegundos);
    }

    /// <summary>
    /// Garante que, com o CONTAGEM_INICIADA perdido (contagemTerminaEm nulo), vale o trecho padrão mais o do servidor.
    /// </summary>
    [TestMethod]
    public void Aceitar_ContagemIniciadaPerdida_UsaOTrechoPadrao()
    {
        // Arrange
        var impacto = EventoImpacto.Registrar(Guid.NewGuid(), Utc(21, 15, 37), 4.8, true, true, true);
        DateTime criadaEm = Utc(21, 16, 2);
        Ocorrencia ocorrencia = Abrir(impacto, criadaEm);

        // Act
        Aceitar(ocorrencia, criadaEm.AddSeconds(40));

        // Assert
        Assert.IsNull(impacto.ContagemTerminaEm);
        Assert.AreEqual(65, ocorrencia.TempoAteAceiteSegundos);
    }

    /// <summary>
    /// Garante que um trecho do capacete negativo conta zero. O domínio não cria esse dado (a contagem
    /// não começa antes do impacto), então o teste simula uma linha já gravada, pela reflexão.
    /// </summary>
    [TestMethod]
    public void SegundosDesdeAQueda_TrechoDoCapaceteNegativo_ContaZero()
    {
        // Arrange
        var impacto = EventoImpacto.Registrar(Guid.NewGuid(), Utc(21, 15, 37), 4.8, true, true, true);
        typeof(EventoImpacto).GetProperty(nameof(EventoImpacto.ContagemTerminaEm))!
            .SetValue(impacto, (DateTime?)Utc(21, 15, 27));
        DateTime criadaEm = Utc(21, 16, 2);
        Ocorrencia ocorrencia = Abrir(impacto, criadaEm);

        // Act
        int segundos = ocorrencia.SegundosDesdeAQueda(criadaEm.AddSeconds(30), Trecho);

        // Assert
        Assert.AreEqual(30, segundos);
    }

    /// <summary>
    /// Garante o arredondamento único, na soma: trechos de 25,6 s e 30,6 s dão 56, e não 55.
    /// </summary>
    [TestMethod]
    public void Aceitar_TrechosComFracao_ArredondaSoUmaVezNaSoma()
    {
        // Arrange: queda às 21:15:36.400 e contagem até 21:16:02 (25,6 s); aceite 30,6 s depois da criação.
        EventoImpacto impacto = ImpactoComContagem(Utc(21, 15, 36).AddMilliseconds(400), inicioContagem: Utc(21, 15, 47));
        DateTime criadaEm = Utc(21, 16, 2);
        Ocorrencia ocorrencia = Abrir(impacto, criadaEm);

        // Act
        Aceitar(ocorrencia, criadaEm.AddMilliseconds(30_600));

        // Assert
        Assert.AreEqual(56, ocorrencia.TempoAteAceiteSegundos);
    }

    /// <summary>
    /// Garante que o cronômetro antes do aceite usa a mesma regra, com o agora no relógio do servidor.
    /// </summary>
    [TestMethod]
    public void SegundosDesdeAQueda_AntesDoAceite_UsaOsDoisTrechos()
    {
        // Arrange: o mesmo caso de 12/11, ainda sem aceite.
        EventoImpacto impacto = ImpactoComContagem(Utc(23, 37, 52), inicioContagem: Utc(23, 38, 3));
        DateTime criadaEm = Utc(23, 40, 13);
        Ocorrencia ocorrencia = Abrir(impacto, criadaEm);

        // Act
        int segundos = ocorrencia.SegundosDesdeAQueda(criadaEm.AddSeconds(10), Trecho);

        // Assert
        Assert.AreEqual(36, segundos);
    }

    /// <summary>
    /// Garante que um trecho padrão que não é positivo é recusado sem alterar a ocorrência.
    /// </summary>
    [TestMethod]
    public void Aceitar_TrechoPadraoZero_LancaValidacaoSemAlterar()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima);

        // Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(
            () => ocorrencia.Aceitar(voluntario, CenarioSos.Aceite, CenarioSos.RaioCamada1Km, TimeSpan.Zero));
        Assert.AreEqual(EstadoOcorrencia.SosCamada1, ocorrencia.Estado);
        Assert.IsNull(ocorrencia.AceitaEm);
        Assert.IsEmpty(ocorrencia.Aceites);
    }

    private static DateTime Utc(int hora, int minuto, int segundo)
    {
        return new DateTime(2026, 11, 12, hora, minuto, segundo, DateTimeKind.Utc);
    }

    private static EventoImpacto ImpactoComContagem(DateTime queda, DateTime inicioContagem)
    {
        var impacto = EventoImpacto.Registrar(Guid.NewGuid(), queda, 2.54, true, false, true);
        impacto.IniciarContagem(inicioContagem, Contagem);
        return impacto;
    }

    private static Ocorrencia Abrir(EventoImpacto impacto, DateTime criadaEm)
    {
        return Ocorrencia.Abrir(Guid.NewGuid(), impacto, CenarioSos.Local, criadaEm);
    }

    private static void Aceitar(Ocorrencia ocorrencia, DateTime aceitaEm)
    {
        ocorrencia.Aceitar(
            CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima),
            aceitaEm,
            CenarioSos.RaioCamada1Km,
            Trecho);
    }
}
