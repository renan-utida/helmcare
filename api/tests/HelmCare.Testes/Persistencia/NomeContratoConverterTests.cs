using System;
using HelmCare.Api.Persistencia.Conversores;
using HelmCare.Dominio.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Persistencia;

/// <summary>
/// Testes do conversor que grava os enums no banco com o nome do contrato.
/// </summary>
[TestClass]
public sealed class NomeContratoConverterTests
{
    /// <summary>
    /// Garante a ida (enum para texto do contrato) e a volta (texto para o mesmo enum) no conversor.
    /// </summary>
    /// <param name="valor">Valor do enum.</param>
    /// <param name="texto">Texto do contrato.</param>
    [TestMethod]
    [DataRow(EstadoDispositivo.SosEnviado, "SOS_ENVIADO")]
    [DataRow(EstadoDispositivo.ImpactoDetectado, "IMPACTO_DETECTADO")]
    [DataRow(EstadoOcorrencia.SosCamada1, "SOS_CAMADA_1")]
    [DataRow(EstadoOcorrencia.SocorroACaminho, "SOCORRO_A_CAMINHO")]
    [DataRow(ResultadoImpacto.CanceladoPeloEntregador, "CANCELADO_PELO_ENTREGADOR")]
    [DataRow(ResultadoImpacto.Ignorado, "IGNORADO")]
    [DataRow(FaixaRisco.MuitoAlto, "MUITO_ALTO")]
    [DataRow(FaixaRisco.Baixo, "BAIXO")]
    [DataRow(TipoVoluntario.VigiaMoto, "VIGIA_MOTO")]
    [DataRow(OrigemAcessoCofre.EmergenciaManual, "EMERGENCIA_MANUAL")]
    [DataRow(TipoEventoMqtt.ContagemIniciada, "CONTAGEM_INICIADA")]
    [DataRow(TipoEventoMqtt.Sos, "SOS")]
    [DataRow(TipoComandoMqtt.SocorroACaminho, "SOCORRO_A_CAMINHO")]
    [DataRow(TipoComandoMqtt.Desarmar, "DESARMAR")]
    public void Converter_IdaEVolta_UsaNomeDoContrato(Enum valor, string texto)
    {
        // Arrange
        ValueConverter conversor = CriarConversor(valor.GetType());

        // Act
        object? gravado = conversor.ConvertToProvider(valor);
        object? lido = conversor.ConvertFromProvider(texto);

        // Assert
        Assert.AreEqual(texto, gravado);
        Assert.AreEqual(valor, lido);
    }

    /// <summary>
    /// Garante que um texto fora do contrato vindo do banco falha com mensagem clara.
    /// </summary>
    /// <param name="texto">Texto inválido.</param>
    [TestMethod]
    [DataRow("SosCamada1")]
    [DataRow("sos_camada_1")]
    [DataRow("SOS_CAMADA_9")]
    [DataRow("")]
    public void ConvertFromProvider_TextoDesconhecido_LancaInvalidOperation(string texto)
    {
        // Arrange
        var conversor = new NomeContratoConverter<EstadoOcorrencia>();

        // Act
        InvalidOperationException excecao = Assert.ThrowsExactly<InvalidOperationException>(
            () => conversor.ConvertFromProvider(texto));

        // Assert
        Assert.Contains(nameof(EstadoOcorrencia), excecao.Message);
    }

    private static ValueConverter CriarConversor(Type tipoEnum)
    {
        Type tipo = typeof(NomeContratoConverter<>).MakeGenericType(tipoEnum);
        return (ValueConverter)Activator.CreateInstance(tipo)!;
    }
}
