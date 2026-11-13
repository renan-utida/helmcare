using System;
using System.Linq;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes da máquina de estados da <see cref="Ocorrencia"/> (tabela do CLAUDE.md) e do indicador de impacto.
/// </summary>
[TestClass]
public sealed class OcorrenciaTests
{
    private static readonly TimeSpan _tempoCamada1 = CenarioSos.TempoAceiteCamada1;
    private const double Raio = CenarioSos.RaioCamada1Km;

    private static readonly TimeSpan Trecho = CenarioSos.TrechoCapacetePadrao;

    /// <summary>
    /// Garante que abrir cria a ocorrência em SOS_CAMADA_1 e marca o impacto como SOS_ENVIADO.
    /// </summary>
    [TestMethod]
    public void Abrir_EventoEmAberto_CriaEmCamada1EMarcaSosEnviado()
    {
        // Arrange
        Guid entregadorId = Guid.NewGuid();
        EventoImpacto evento = CenarioSos.NovoEventoEmAberto();

        // Act
        var ocorrencia = Ocorrencia.Abrir(entregadorId, evento, CenarioSos.Local, CenarioSos.Sos);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.SosCamada1, ocorrencia.Estado);
        Assert.AreEqual(1, ocorrencia.Camada);
        Assert.AreEqual(entregadorId, ocorrencia.EntregadorId);
        Assert.AreEqual(evento.Id, ocorrencia.EventoImpactoId);
        Assert.AreSame(evento, ocorrencia.EventoImpacto);
        Assert.AreEqual(CenarioSos.Sos, ocorrencia.CriadaEm);
        Assert.AreEqual(CenarioSos.Local.Latitude, ocorrencia.Latitude);
        Assert.AreEqual(CenarioSos.Local.Longitude, ocorrencia.Longitude);
        Assert.AreEqual(ResultadoImpacto.SosEnviado, evento.Resultado);
        Assert.IsNull(ocorrencia.AceitaEm);
        Assert.IsNull(ocorrencia.EncerradaEm);
        Assert.IsNull(ocorrencia.TempoAteAceiteSegundos);
        Assert.IsEmpty(ocorrencia.Aceites);
    }

    /// <summary>
    /// Garante que não se abre SOS a partir de um impacto já IGNORADO.
    /// </summary>
    [TestMethod]
    public void Abrir_EventoIgnorado_LancaTransicaoInvalida()
    {
        // Arrange
        var evento = EventoImpacto.Registrar(Guid.NewGuid(), CenarioSos.Queda, 3.1, false, false, false);

        // Act e Assert
        Assert.ThrowsExactly<TransicaoInvalidaException>(
            () => Ocorrencia.Abrir(Guid.NewGuid(), evento, CenarioSos.Local, CenarioSos.Sos));
        Assert.AreEqual(ResultadoImpacto.Ignorado, evento.Resultado);
    }

    /// <summary>
    /// Garante que o escalonamento só é devido a partir de 60 s depois da criação.
    /// </summary>
    /// <param name="segundos">Segundos depois da criação.</param>
    /// <param name="esperado">Se deve escalar.</param>
    [TestMethod]
    [DataRow(59, false)]
    [DataRow(60, true)]
    public void DeveEscalar_TempoDesdeACriacao_RespeitaTempoDaCamada1(int segundos, bool esperado)
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();

        // Act
        bool deve = ocorrencia.DeveEscalar(CenarioSos.Sos.AddSeconds(segundos), _tempoCamada1);

        // Assert
        Assert.AreEqual(esperado, deve);
    }

    /// <summary>
    /// Garante que DeveEscalar é falso fora de SOS_CAMADA_1, mesmo depois do tempo.
    /// </summary>
    [TestMethod]
    public void DeveEscalar_JaNaCamada2_DevolveFalse()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(EstadoOcorrencia.SosCamada2);

        // Act e Assert
        Assert.IsFalse(ocorrencia.DeveEscalar(CenarioSos.Sos.AddMinutes(5), _tempoCamada1));
    }

    /// <summary>
    /// Garante que escalar aos 59 s é regra de negócio violada e nada muda.
    /// </summary>
    [TestMethod]
    public void EscalarParaCamada2_Aos59Segundos_LancaRegraNegocioSemAlterar()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();

        // Act
        Assert.ThrowsExactly<RegraNegocioException>(
            () => ocorrencia.EscalarParaCamada2(CenarioSos.Sos.AddSeconds(59), _tempoCamada1));

        // Assert
        Assert.AreEqual(EstadoOcorrencia.SosCamada1, ocorrencia.Estado);
        Assert.AreEqual(1, ocorrencia.Camada);
    }

    /// <summary>
    /// Garante que aos 60 s a ocorrência vai para SOS_CAMADA_2 e escalar de novo é transição inválida.
    /// </summary>
    [TestMethod]
    public void EscalarParaCamada2_Aos60Segundos_VaiParaCamada2EDepoisLanca()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();

        // Act
        ocorrencia.EscalarParaCamada2(CenarioSos.Sos.AddSeconds(60), _tempoCamada1);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.SosCamada2, ocorrencia.Estado);
        Assert.AreEqual(2, ocorrencia.Camada);
        Assert.ThrowsExactly<TransicaoInvalidaException>(
            () => ocorrencia.EscalarParaCamada2(CenarioSos.Sos.AddSeconds(61), _tempoCamada1));
        Assert.AreEqual(2, ocorrencia.Camada);
    }

    /// <summary>
    /// Garante que o indicador conta da queda (63 s) e não da criação da ocorrência (38 s).
    /// </summary>
    [TestMethod]
    public void Aceitar_QuedaCriacaoEAceite_TempoAteAceiteContaDaQueda()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima);

        // Act
        AceiteSocorro aceite = ocorrencia.Aceitar(voluntario, CenarioSos.Aceite, Raio, Trecho);

        // Assert
        Assert.AreEqual(63, ocorrencia.TempoAteAceiteSegundos);
        Assert.AreEqual(EstadoOcorrencia.SocorroACaminho, ocorrencia.Estado);
        Assert.AreEqual(CenarioSos.Aceite, ocorrencia.AceitaEm);
        Assert.AreSame(aceite, ocorrencia.Aceites.Single());
        Assert.AreEqual(ocorrencia.Id, aceite.OcorrenciaId);
        Assert.AreEqual(voluntario.Id, aceite.VoluntarioId);
        Assert.AreEqual(CenarioSos.Aceite, aceite.AceitoEm);
    }

    /// <summary>
    /// Garante o mesmo indicador de 63 s quando a queda foi estimada a partir do SOS.
    /// </summary>
    [TestMethod]
    public void Aceitar_EventoEstimadoPorSos_TempoAteAceiteContaDaQuedaEstimada()
    {
        // Arrange
        var evento = EventoImpacto.RegistrarEstimadoPorSos(
            Guid.NewGuid(), CenarioSos.Sos, CenarioSos.AntecedenciaSos, 4.8, true, true, true);
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia(evento: evento);

        // Act
        ocorrencia.Aceitar(CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima), CenarioSos.Aceite, Raio, Trecho);

        // Assert
        Assert.AreEqual(63, ocorrencia.TempoAteAceiteSegundos);
    }

    /// <summary>
    /// Garante que um relógio do capacete adiantado em relação ao servidor não entra no indicador:
    /// sem contagem, vale o trecho padrão (25 s) mais o trecho do servidor (38 s).
    /// </summary>
    [TestMethod]
    public void Aceitar_RelogioDoCapaceteAdiantado_NaoEntraNoIndicador()
    {
        // Arrange
        var evento = EventoImpacto.Registrar(Guid.NewGuid(), CenarioSos.Aceite.AddMinutes(2), 4.8, true, true, true);
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia(evento: evento);

        // Act
        ocorrencia.Aceitar(CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima), CenarioSos.Aceite, Raio, Trecho);

        // Assert
        Assert.AreEqual(63, ocorrencia.TempoAteAceiteSegundos);
    }

    /// <summary>
    /// Garante que o aceite em SOS_CAMADA_2 funciona e que um segundo aceite é transição inválida.
    /// </summary>
    [TestMethod]
    public void Aceitar_NaCamada2ESegundoAceite_AceitaUmaVezSo()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(EstadoOcorrencia.SosCamada2);
        DateTime momento = CenarioSos.Sos.AddSeconds(70);

        // Act
        ocorrencia.Aceitar(CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima), momento, Raio, Trecho);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.SocorroACaminho, ocorrencia.Estado);
        Assert.ThrowsExactly<TransicaoInvalidaException>(
            () => ocorrencia.Aceitar(CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima), momento.AddSeconds(1), Raio, Trecho));
        Assert.HasCount(1, ocorrencia.Aceites);
        Assert.AreEqual(momento, ocorrencia.AceitaEm);
    }

    /// <summary>
    /// Garante que o próprio entregador não pode aceitar o seu SOS.
    /// </summary>
    [TestMethod]
    public void Aceitar_PeloProprioEntregador_LancaRegraNegocio()
    {
        // Arrange
        Guid entregadorId = Guid.NewGuid();
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia(entregadorId);
        Voluntario proprio = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima, entregadorId);

        // Act e Assert
        Assert.ThrowsExactly<RegraNegocioException>(() => ocorrencia.Aceitar(proprio, CenarioSos.Aceite, Raio, Trecho));
        AssertAindaAguardandoNaCamada1(ocorrencia);
    }

    /// <summary>
    /// Garante que um voluntário indisponível não pode aceitar.
    /// </summary>
    [TestMethod]
    public void Aceitar_VoluntarioIndisponivel_LancaRegraNegocio()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        var indisponivel = new Voluntario(Guid.NewGuid(), TipoVoluntario.VigiaMoto);

        // Act e Assert
        Assert.ThrowsExactly<RegraNegocioException>(() => ocorrencia.Aceitar(indisponivel, CenarioSos.Aceite, Raio, Trecho));
        AssertAindaAguardandoNaCamada1(ocorrencia);
    }

    /// <summary>
    /// Garante que o voluntário a 2,47 km é recusado na camada 1 (raio de 2 km) e aceito na camada 2.
    /// </summary>
    [TestMethod]
    public void Aceitar_VoluntarioForaDoRaio_RecusadoNaCamada1EAceitoNaCamada2()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        Voluntario distante = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoDistante);

        // Act e Assert: camada 1
        Assert.ThrowsExactly<RegraNegocioException>(() => ocorrencia.Aceitar(distante, CenarioSos.Aceite, Raio, Trecho));
        AssertAindaAguardandoNaCamada1(ocorrencia);

        // Act e Assert: camada 2
        ocorrencia.EscalarParaCamada2(CenarioSos.Sos + _tempoCamada1, _tempoCamada1);
        ocorrencia.Aceitar(distante, CenarioSos.Sos.AddSeconds(65), Raio, Trecho);
        Assert.AreEqual(EstadoOcorrencia.SocorroACaminho, ocorrencia.Estado);
    }

    /// <summary>
    /// Garante que o voluntário a 0,47 km é aceito na camada 1.
    /// </summary>
    [TestMethod]
    public void Aceitar_VoluntarioDentroDoRaio_AceitoNaCamada1()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        Voluntario proximo = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima);

        // Act
        ocorrencia.Aceitar(proximo, CenarioSos.Aceite, Raio, Trecho);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.SocorroACaminho, ocorrencia.Estado);
        Assert.AreEqual(1, ocorrencia.Camada);
    }

    /// <summary>
    /// Garante que cancelar em SOS_CAMADA_1 ou SOS_CAMADA_2 vai para CANCELADA com EncerradaEm.
    /// </summary>
    /// <param name="estado">Estado de espera.</param>
    [TestMethod]
    [DataRow(EstadoOcorrencia.SosCamada1)]
    [DataRow(EstadoOcorrencia.SosCamada2)]
    public void Cancelar_AguardandoAceite_VaiParaCancelada(EstadoOcorrencia estado)
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(estado);
        DateTime momento = CenarioSos.Sos.AddMinutes(2);

        // Act
        ocorrencia.Cancelar(momento);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.Cancelada, ocorrencia.Estado);
        Assert.AreEqual(momento, ocorrencia.EncerradaEm);
        Assert.IsNull(ocorrencia.TempoAteAceiteSegundos);
    }

    /// <summary>
    /// Garante que, depois do aceite, cancelar é transição inválida.
    /// </summary>
    [TestMethod]
    public void Cancelar_DepoisDoAceite_LancaTransicaoInvalida()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(EstadoOcorrencia.SocorroACaminho);

        // Act e Assert
        Assert.ThrowsExactly<TransicaoInvalidaException>(() => ocorrencia.Cancelar(CenarioSos.Aceite.AddMinutes(1)));
        Assert.AreEqual(EstadoOcorrencia.SocorroACaminho, ocorrencia.Estado);
        Assert.IsNull(ocorrencia.EncerradaEm);
    }

    /// <summary>
    /// Garante que encerrar sem aceite (v1.3) vai para ENCERRADA com o tempo de aceite nulo.
    /// </summary>
    /// <param name="estado">Estado de espera.</param>
    [TestMethod]
    [DataRow(EstadoOcorrencia.SosCamada1)]
    [DataRow(EstadoOcorrencia.SosCamada2)]
    public void Encerrar_SemAceite_VaiParaEncerradaComTempoNulo(EstadoOcorrencia estado)
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(estado);
        DateTime momento = CenarioSos.Sos.AddMinutes(10);

        // Act
        ocorrencia.Encerrar(momento);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.Encerrada, ocorrencia.Estado);
        Assert.AreEqual(momento, ocorrencia.EncerradaEm);
        Assert.IsNull(ocorrencia.AceitaEm);
        Assert.IsNull(ocorrencia.TempoAteAceiteSegundos);
    }

    /// <summary>
    /// Garante que encerrar depois do aceite mantém o tempo até o aceite.
    /// </summary>
    [TestMethod]
    public void Encerrar_DepoisDoAceite_MantemTempoAteAceite()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(EstadoOcorrencia.SocorroACaminho);
        DateTime momento = CenarioSos.Aceite.AddMinutes(15);

        // Act
        ocorrencia.Encerrar(momento);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.Encerrada, ocorrencia.Estado);
        Assert.AreEqual(momento, ocorrencia.EncerradaEm);
        Assert.AreEqual(63, ocorrencia.TempoAteAceiteSegundos);
        Assert.AreEqual(CenarioSos.Aceite, ocorrencia.AceitaEm);
    }

    /// <summary>
    /// Garante que, em ENCERRADA e CANCELADA, toda ação é transição inválida e nenhum campo muda.
    /// </summary>
    /// <param name="estadoFinal">Estado final.</param>
    /// <param name="acao">Ação tentada.</param>
    [TestMethod]
    [DataRow(EstadoOcorrencia.Encerrada, "Escalar")]
    [DataRow(EstadoOcorrencia.Encerrada, "Aceitar")]
    [DataRow(EstadoOcorrencia.Encerrada, "Cancelar")]
    [DataRow(EstadoOcorrencia.Encerrada, "Encerrar")]
    [DataRow(EstadoOcorrencia.Cancelada, "Escalar")]
    [DataRow(EstadoOcorrencia.Cancelada, "Aceitar")]
    [DataRow(EstadoOcorrencia.Cancelada, "Cancelar")]
    [DataRow(EstadoOcorrencia.Cancelada, "Encerrar")]
    public void Acao_EmEstadoFinal_LancaTransicaoInvalidaSemAlterar(EstadoOcorrencia estadoFinal, string acao)
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(estadoFinal);
        string antes = Retrato(ocorrencia);
        DateTime depois = CenarioSos.Sos.AddMinutes(30);
        Voluntario voluntario = CenarioSos.NovoVoluntarioDisponivel(CenarioSos.PosicaoProxima);

        Action executar = acao switch
        {
            "Escalar" => () => ocorrencia.EscalarParaCamada2(depois, _tempoCamada1),
            "Aceitar" => () => ocorrencia.Aceitar(voluntario, depois, Raio, Trecho),
            "Cancelar" => () => ocorrencia.Cancelar(depois),
            "Encerrar" => () => ocorrencia.Encerrar(depois),
            _ => throw new ArgumentOutOfRangeException(nameof(acao), acao, "Ação desconhecida."),
        };

        // Act
        Assert.ThrowsExactly<TransicaoInvalidaException>(executar);

        // Assert
        Assert.AreEqual(antes, Retrato(ocorrencia));
    }

    /// <summary>
    /// Garante que uma ação com instante anterior à criação é rejeitada sem alterar nada.
    /// </summary>
    [TestMethod]
    public void Encerrar_AntesDaCriacao_LancaValidacaoSemAlterar()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();

        // Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(() => ocorrencia.Encerrar(CenarioSos.Sos.AddSeconds(-1)));
        AssertAindaAguardandoNaCamada1(ocorrencia);
    }

    /// <summary>
    /// Garante que a contagem vai da queda até o instante informado, que pode ser o agora
    /// (sem contagem no impacto: 25 s do trecho padrão mais o trecho do servidor).
    /// </summary>
    [TestMethod]
    public void SegundosDesdeAQueda_InstanteDepoisDaQueda_ContaDesdeAQueda()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();

        // Act
        int aos10 = ocorrencia.SegundosDesdeAQueda(CenarioSos.Sos.AddSeconds(10), Trecho);
        int aos20 = ocorrencia.SegundosDesdeAQueda(CenarioSos.Sos.AddSeconds(20), Trecho);

        // Assert
        Assert.AreEqual(35, aos10);
        Assert.AreEqual(45, aos20);
    }

    /// <summary>
    /// Garante que a fração de segundo é descartada (arredonda para baixo).
    /// </summary>
    [TestMethod]
    public void SegundosDesdeAQueda_FracaoDeSegundo_ArredondaParaBaixo()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();

        // Act
        int segundos = ocorrencia.SegundosDesdeAQueda(CenarioSos.Queda.AddMilliseconds(62_999), Trecho);

        // Assert
        Assert.AreEqual(62, segundos);
    }

    /// <summary>
    /// Garante que um instante anterior à criação faz o trecho do servidor contar zero, e sobra só o do capacete.
    /// </summary>
    [TestMethod]
    public void SegundosDesdeAQueda_InstanteAntesDaCriacao_TrechoDoServidorContaZero()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();

        // Act
        int segundos = ocorrencia.SegundosDesdeAQueda(CenarioSos.Sos.AddMinutes(-10), Trecho);

        // Assert
        Assert.AreEqual(25, segundos);
    }

    /// <summary>
    /// Garante que, no instante do aceite, o valor é o mesmo do indicador calculado pelo Aceitar.
    /// </summary>
    [TestMethod]
    public void SegundosDesdeAQueda_NoInstanteDoAceite_IgualAoTempoAteAceite()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(EstadoOcorrencia.SocorroACaminho);

        // Act
        int segundos = ocorrencia.SegundosDesdeAQueda(ocorrencia.AceitaEm!.Value, Trecho);

        // Assert
        Assert.AreEqual(63, segundos);
        Assert.AreEqual(ocorrencia.TempoAteAceiteSegundos, segundos);
    }

    /// <summary>
    /// Garante que um instante fora de UTC é recusado.
    /// </summary>
    [TestMethod]
    public void SegundosDesdeAQueda_InstanteNaoUtc_LancaValidacao()
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.AbrirOcorrencia();
        DateTime local = DateTime.SpecifyKind(CenarioSos.Aceite, DateTimeKind.Local);

        // Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(() => ocorrencia.SegundosDesdeAQueda(local, Trecho));
    }

    /// <summary>
    /// Garante EstaAtiva e EstaAguardandoAceite em cada estado.
    /// </summary>
    /// <param name="estado">Estado testado.</param>
    /// <param name="ativa">Valor esperado de EstaAtiva.</param>
    /// <param name="aguardando">Valor esperado de EstaAguardandoAceite.</param>
    [TestMethod]
    [DataRow(EstadoOcorrencia.SosCamada1, true, true)]
    [DataRow(EstadoOcorrencia.SosCamada2, true, true)]
    [DataRow(EstadoOcorrencia.SocorroACaminho, true, false)]
    [DataRow(EstadoOcorrencia.Encerrada, false, false)]
    [DataRow(EstadoOcorrencia.Cancelada, false, false)]
    public void Propriedades_EmCadaEstado_RefletemOEstado(EstadoOcorrencia estado, bool ativa, bool aguardando)
    {
        // Arrange e Act
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(estado);

        // Assert
        Assert.AreEqual(estado, ocorrencia.Estado);
        Assert.AreEqual(ativa, ocorrencia.EstaAtiva);
        Assert.AreEqual(aguardando, ocorrencia.EstaAguardandoAceite);
    }

    private static void AssertAindaAguardandoNaCamada1(Ocorrencia ocorrencia)
    {
        Assert.AreEqual(EstadoOcorrencia.SosCamada1, ocorrencia.Estado);
        Assert.IsNull(ocorrencia.AceitaEm);
        Assert.IsNull(ocorrencia.EncerradaEm);
        Assert.IsNull(ocorrencia.TempoAteAceiteSegundos);
        Assert.IsEmpty(ocorrencia.Aceites);
    }

    // Junta todos os campos mutáveis num texto, para comparar o antes e o depois.
    private static string Retrato(Ocorrencia ocorrencia)
    {
        return string.Join(
            '|',
            ocorrencia.Estado,
            ocorrencia.Camada,
            ocorrencia.AceitaEm?.Ticks,
            ocorrencia.EncerradaEm?.Ticks,
            ocorrencia.TempoAteAceiteSegundos,
            ocorrencia.Aceites.Count);
    }
}
