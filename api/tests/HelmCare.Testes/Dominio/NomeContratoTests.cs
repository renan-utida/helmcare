using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Confere os nomes dos enums contra a seção 4 do contrato de integração (versão 1.2).
/// Os textos esperados estão escritos à mão de propósito: se alguém mudar um atributo,
/// o teste quebra em vez de acompanhar a mudança.
/// </summary>
[TestClass]
public sealed class NomeContratoTests
{
    private const string NamespaceEnums = "HelmCare.Dominio.Enums";

    // Total de membros na seção 4 do contrato: 8 + 5 + 4 + 4 + 2 + 2 + 5 + 5.
    private const int TotalMembrosNoContrato = 35;

    /// <summary>
    /// Garante que <see cref="NomeContrato.Para{TEnum}(TEnum)"/> devolve exatamente o nome da seção 4 do contrato para cada membro.
    /// </summary>
    [TestMethod]
    [DataRow(EstadoDispositivo.Desarmado, "DESARMADO")]
    [DataRow(EstadoDispositivo.Armado, "ARMADO")]
    [DataRow(EstadoDispositivo.ImpactoDetectado, "IMPACTO_DETECTADO")]
    [DataRow(EstadoDispositivo.Observacao, "OBSERVACAO")]
    [DataRow(EstadoDispositivo.Contagem, "CONTAGEM")]
    [DataRow(EstadoDispositivo.SosEnviado, "SOS_ENVIADO")]
    [DataRow(EstadoDispositivo.Cancelado, "CANCELADO")]
    [DataRow(EstadoDispositivo.FalsoPositivo, "FALSO_POSITIVO")]
    [DataRow(EstadoOcorrencia.SosCamada1, "SOS_CAMADA_1")]
    [DataRow(EstadoOcorrencia.SosCamada2, "SOS_CAMADA_2")]
    [DataRow(EstadoOcorrencia.SocorroACaminho, "SOCORRO_A_CAMINHO")]
    [DataRow(EstadoOcorrencia.Encerrada, "ENCERRADA")]
    [DataRow(EstadoOcorrencia.Cancelada, "CANCELADA")]
    [DataRow(ResultadoImpacto.Ignorado, "IGNORADO")]
    [DataRow(ResultadoImpacto.FalsoPositivo, "FALSO_POSITIVO")]
    [DataRow(ResultadoImpacto.CanceladoPeloEntregador, "CANCELADO_PELO_ENTREGADOR")]
    [DataRow(ResultadoImpacto.SosEnviado, "SOS_ENVIADO")]
    [DataRow(FaixaRisco.Baixo, "BAIXO")]
    [DataRow(FaixaRisco.Moderado, "MODERADO")]
    [DataRow(FaixaRisco.Alto, "ALTO")]
    [DataRow(FaixaRisco.MuitoAlto, "MUITO_ALTO")]
    [DataRow(TipoVoluntario.Entregador, "ENTREGADOR")]
    [DataRow(TipoVoluntario.VigiaMoto, "VIGIA_MOTO")]
    [DataRow(OrigemAcessoCofre.SosAtivo, "SOS_ATIVO")]
    [DataRow(OrigemAcessoCofre.EmergenciaManual, "EMERGENCIA_MANUAL")]
    [DataRow(TipoEventoMqtt.ImpactoDetectado, "IMPACTO_DETECTADO")]
    [DataRow(TipoEventoMqtt.ContagemIniciada, "CONTAGEM_INICIADA")]
    [DataRow(TipoEventoMqtt.Cancelado, "CANCELADO")]
    [DataRow(TipoEventoMqtt.FalsoPositivo, "FALSO_POSITIVO")]
    [DataRow(TipoEventoMqtt.Sos, "SOS")]
    [DataRow(TipoComandoMqtt.Armar, "ARMAR")]
    [DataRow(TipoComandoMqtt.Desarmar, "DESARMAR")]
    [DataRow(TipoComandoMqtt.Cancelar, "CANCELAR")]
    [DataRow(TipoComandoMqtt.SocorroACaminho, "SOCORRO_A_CAMINHO")]
    [DataRow(TipoComandoMqtt.Encerrar, "ENCERRAR")]
    public void Para_ValorDoEnum_DevolveNomeDoContrato(Enum valor, string esperado)
    {
        // Arrange: o DataRow chega como Enum; o método genérico precisa do tipo concreto.
        Func<Enum, string> para = CriarPara(valor.GetType());

        // Act
        string nome = para(valor);

        // Assert
        Assert.AreEqual(esperado, nome);
    }

    /// <summary>
    /// Garante que o <see cref="JsonSerializer"/> grava cada membro com o nome do contrato entre aspas.
    /// </summary>
    [TestMethod]
    [DataRow(EstadoDispositivo.Desarmado, "DESARMADO")]
    [DataRow(EstadoDispositivo.Armado, "ARMADO")]
    [DataRow(EstadoDispositivo.ImpactoDetectado, "IMPACTO_DETECTADO")]
    [DataRow(EstadoDispositivo.Observacao, "OBSERVACAO")]
    [DataRow(EstadoDispositivo.Contagem, "CONTAGEM")]
    [DataRow(EstadoDispositivo.SosEnviado, "SOS_ENVIADO")]
    [DataRow(EstadoDispositivo.Cancelado, "CANCELADO")]
    [DataRow(EstadoDispositivo.FalsoPositivo, "FALSO_POSITIVO")]
    [DataRow(EstadoOcorrencia.SosCamada1, "SOS_CAMADA_1")]
    [DataRow(EstadoOcorrencia.SosCamada2, "SOS_CAMADA_2")]
    [DataRow(EstadoOcorrencia.SocorroACaminho, "SOCORRO_A_CAMINHO")]
    [DataRow(EstadoOcorrencia.Encerrada, "ENCERRADA")]
    [DataRow(EstadoOcorrencia.Cancelada, "CANCELADA")]
    [DataRow(ResultadoImpacto.Ignorado, "IGNORADO")]
    [DataRow(ResultadoImpacto.FalsoPositivo, "FALSO_POSITIVO")]
    [DataRow(ResultadoImpacto.CanceladoPeloEntregador, "CANCELADO_PELO_ENTREGADOR")]
    [DataRow(ResultadoImpacto.SosEnviado, "SOS_ENVIADO")]
    [DataRow(FaixaRisco.Baixo, "BAIXO")]
    [DataRow(FaixaRisco.Moderado, "MODERADO")]
    [DataRow(FaixaRisco.Alto, "ALTO")]
    [DataRow(FaixaRisco.MuitoAlto, "MUITO_ALTO")]
    [DataRow(TipoVoluntario.Entregador, "ENTREGADOR")]
    [DataRow(TipoVoluntario.VigiaMoto, "VIGIA_MOTO")]
    [DataRow(OrigemAcessoCofre.SosAtivo, "SOS_ATIVO")]
    [DataRow(OrigemAcessoCofre.EmergenciaManual, "EMERGENCIA_MANUAL")]
    [DataRow(TipoEventoMqtt.ImpactoDetectado, "IMPACTO_DETECTADO")]
    [DataRow(TipoEventoMqtt.ContagemIniciada, "CONTAGEM_INICIADA")]
    [DataRow(TipoEventoMqtt.Cancelado, "CANCELADO")]
    [DataRow(TipoEventoMqtt.FalsoPositivo, "FALSO_POSITIVO")]
    [DataRow(TipoEventoMqtt.Sos, "SOS")]
    [DataRow(TipoComandoMqtt.Armar, "ARMAR")]
    [DataRow(TipoComandoMqtt.Desarmar, "DESARMAR")]
    [DataRow(TipoComandoMqtt.Cancelar, "CANCELAR")]
    [DataRow(TipoComandoMqtt.SocorroACaminho, "SOCORRO_A_CAMINHO")]
    [DataRow(TipoComandoMqtt.Encerrar, "ENCERRAR")]
    public void Serialize_ValorDoEnum_GeraNomeDoContratoEntreAspas(Enum valor, string esperado)
    {
        // Arrange
        Type tipo = valor.GetType();

        // Act
        string json = JsonSerializer.Serialize(valor, tipo);

        // Assert
        Assert.AreEqual($"\"{esperado}\"", json);
    }

    /// <summary>
    /// Garante a ida e volta: <see cref="NomeContrato.TentarLer{TEnum}(string, out TEnum)"/> reconhece cada nome do contrato e devolve o membro certo.
    /// </summary>
    [TestMethod]
    [DataRow(EstadoDispositivo.Desarmado, "DESARMADO")]
    [DataRow(EstadoDispositivo.Armado, "ARMADO")]
    [DataRow(EstadoDispositivo.ImpactoDetectado, "IMPACTO_DETECTADO")]
    [DataRow(EstadoDispositivo.Observacao, "OBSERVACAO")]
    [DataRow(EstadoDispositivo.Contagem, "CONTAGEM")]
    [DataRow(EstadoDispositivo.SosEnviado, "SOS_ENVIADO")]
    [DataRow(EstadoDispositivo.Cancelado, "CANCELADO")]
    [DataRow(EstadoDispositivo.FalsoPositivo, "FALSO_POSITIVO")]
    [DataRow(EstadoOcorrencia.SosCamada1, "SOS_CAMADA_1")]
    [DataRow(EstadoOcorrencia.SosCamada2, "SOS_CAMADA_2")]
    [DataRow(EstadoOcorrencia.SocorroACaminho, "SOCORRO_A_CAMINHO")]
    [DataRow(EstadoOcorrencia.Encerrada, "ENCERRADA")]
    [DataRow(EstadoOcorrencia.Cancelada, "CANCELADA")]
    [DataRow(ResultadoImpacto.Ignorado, "IGNORADO")]
    [DataRow(ResultadoImpacto.FalsoPositivo, "FALSO_POSITIVO")]
    [DataRow(ResultadoImpacto.CanceladoPeloEntregador, "CANCELADO_PELO_ENTREGADOR")]
    [DataRow(ResultadoImpacto.SosEnviado, "SOS_ENVIADO")]
    [DataRow(FaixaRisco.Baixo, "BAIXO")]
    [DataRow(FaixaRisco.Moderado, "MODERADO")]
    [DataRow(FaixaRisco.Alto, "ALTO")]
    [DataRow(FaixaRisco.MuitoAlto, "MUITO_ALTO")]
    [DataRow(TipoVoluntario.Entregador, "ENTREGADOR")]
    [DataRow(TipoVoluntario.VigiaMoto, "VIGIA_MOTO")]
    [DataRow(OrigemAcessoCofre.SosAtivo, "SOS_ATIVO")]
    [DataRow(OrigemAcessoCofre.EmergenciaManual, "EMERGENCIA_MANUAL")]
    [DataRow(TipoEventoMqtt.ImpactoDetectado, "IMPACTO_DETECTADO")]
    [DataRow(TipoEventoMqtt.ContagemIniciada, "CONTAGEM_INICIADA")]
    [DataRow(TipoEventoMqtt.Cancelado, "CANCELADO")]
    [DataRow(TipoEventoMqtt.FalsoPositivo, "FALSO_POSITIVO")]
    [DataRow(TipoEventoMqtt.Sos, "SOS")]
    [DataRow(TipoComandoMqtt.Armar, "ARMAR")]
    [DataRow(TipoComandoMqtt.Desarmar, "DESARMAR")]
    [DataRow(TipoComandoMqtt.Cancelar, "CANCELAR")]
    [DataRow(TipoComandoMqtt.SocorroACaminho, "SOCORRO_A_CAMINHO")]
    [DataRow(TipoComandoMqtt.Encerrar, "ENCERRAR")]
    public void TentarLer_NomeDoContrato_DevolveTrueEMembroCerto(Enum esperado, string texto)
    {
        // Arrange
        Func<string?, (bool Sucesso, Enum Valor)> tentarLer = CriarTentarLer(esperado.GetType());

        // Act
        (bool sucesso, Enum valor) = tentarLer(texto);

        // Assert
        Assert.IsTrue(sucesso);
        Assert.AreEqual(esperado, valor);
    }

    /// <summary>
    /// Garante que o <see cref="JsonSerializer"/> lê cada nome do contrato e devolve o membro certo.
    /// </summary>
    [TestMethod]
    [DataRow(EstadoDispositivo.Desarmado, "DESARMADO")]
    [DataRow(EstadoDispositivo.Armado, "ARMADO")]
    [DataRow(EstadoDispositivo.ImpactoDetectado, "IMPACTO_DETECTADO")]
    [DataRow(EstadoDispositivo.Observacao, "OBSERVACAO")]
    [DataRow(EstadoDispositivo.Contagem, "CONTAGEM")]
    [DataRow(EstadoDispositivo.SosEnviado, "SOS_ENVIADO")]
    [DataRow(EstadoDispositivo.Cancelado, "CANCELADO")]
    [DataRow(EstadoDispositivo.FalsoPositivo, "FALSO_POSITIVO")]
    [DataRow(EstadoOcorrencia.SosCamada1, "SOS_CAMADA_1")]
    [DataRow(EstadoOcorrencia.SosCamada2, "SOS_CAMADA_2")]
    [DataRow(EstadoOcorrencia.SocorroACaminho, "SOCORRO_A_CAMINHO")]
    [DataRow(EstadoOcorrencia.Encerrada, "ENCERRADA")]
    [DataRow(EstadoOcorrencia.Cancelada, "CANCELADA")]
    [DataRow(ResultadoImpacto.Ignorado, "IGNORADO")]
    [DataRow(ResultadoImpacto.FalsoPositivo, "FALSO_POSITIVO")]
    [DataRow(ResultadoImpacto.CanceladoPeloEntregador, "CANCELADO_PELO_ENTREGADOR")]
    [DataRow(ResultadoImpacto.SosEnviado, "SOS_ENVIADO")]
    [DataRow(FaixaRisco.Baixo, "BAIXO")]
    [DataRow(FaixaRisco.Moderado, "MODERADO")]
    [DataRow(FaixaRisco.Alto, "ALTO")]
    [DataRow(FaixaRisco.MuitoAlto, "MUITO_ALTO")]
    [DataRow(TipoVoluntario.Entregador, "ENTREGADOR")]
    [DataRow(TipoVoluntario.VigiaMoto, "VIGIA_MOTO")]
    [DataRow(OrigemAcessoCofre.SosAtivo, "SOS_ATIVO")]
    [DataRow(OrigemAcessoCofre.EmergenciaManual, "EMERGENCIA_MANUAL")]
    [DataRow(TipoEventoMqtt.ImpactoDetectado, "IMPACTO_DETECTADO")]
    [DataRow(TipoEventoMqtt.ContagemIniciada, "CONTAGEM_INICIADA")]
    [DataRow(TipoEventoMqtt.Cancelado, "CANCELADO")]
    [DataRow(TipoEventoMqtt.FalsoPositivo, "FALSO_POSITIVO")]
    [DataRow(TipoEventoMqtt.Sos, "SOS")]
    [DataRow(TipoComandoMqtt.Armar, "ARMAR")]
    [DataRow(TipoComandoMqtt.Desarmar, "DESARMAR")]
    [DataRow(TipoComandoMqtt.Cancelar, "CANCELAR")]
    [DataRow(TipoComandoMqtt.SocorroACaminho, "SOCORRO_A_CAMINHO")]
    [DataRow(TipoComandoMqtt.Encerrar, "ENCERRAR")]
    public void Deserialize_NomeDoContratoEntreAspas_DevolveMembroCerto(Enum esperado, string texto)
    {
        // Arrange
        string json = $"\"{texto}\"";

        // Act
        object? valor = JsonSerializer.Deserialize(json, esperado.GetType());

        // Assert
        Assert.AreEqual(esperado, valor);
    }

    /// <summary>
    /// Garante que textos fora do contrato (outra caixa, PascalCase, sem o último sublinhado, vazio ou nulo) são rejeitados.
    /// </summary>
    [TestMethod]
    [DataRow("sos_camada_1")]
    [DataRow("SosCamada1")]
    [DataRow("SOS_CAMADA1")]
    [DataRow("")]
    [DataRow(null)]
    public void TentarLer_TextoForaDoContrato_DevolveFalse(string? texto)
    {
        // Arrange: a entrada inválida vem do DataRow.

        // Act
        bool sucesso = NomeContrato.TentarLer(texto, out EstadoOcorrencia valor);

        // Assert
        Assert.IsFalse(sucesso);
        Assert.AreEqual(default, valor);
    }

    /// <summary>
    /// Proteção para o futuro: todo membro de todo enum de <c>HelmCare.Dominio.Enums</c> tem o atributo com o nome do contrato.
    /// </summary>
    [TestMethod]
    public void EnumsDoDominio_TodoMembro_TemJsonStringEnumMemberName()
    {
        // Arrange
        List<FieldInfo> membros = ObterEnumsDoDominio()
            .SelectMany(tipo => tipo.GetFields(BindingFlags.Public | BindingFlags.Static))
            .ToList();

        // Act
        List<string> semAtributo = membros
            .Where(campo => campo.GetCustomAttribute<JsonStringEnumMemberNameAttribute>() is null)
            .Select(campo => $"{campo.DeclaringType!.Name}.{campo.Name}")
            .ToList();

        // Assert
        Assert.HasCount(TotalMembrosNoContrato, membros);
        Assert.IsEmpty(semAtributo, $"Membros sem o atributo: {string.Join(", ", semAtributo)}");
    }

    /// <summary>
    /// Proteção para o futuro: todo enum de <c>HelmCare.Dominio.Enums</c> declara o <see cref="JsonStringEnumConverter{TEnum}"/> do próprio tipo.
    /// </summary>
    [TestMethod]
    public void EnumsDoDominio_TodoEnum_TemJsonConverterDoProprioTipo()
    {
        // Arrange
        List<Type> enums = ObterEnumsDoDominio();

        // Act
        List<string> semConversor = enums
            .Where(tipo => tipo.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType
                != typeof(JsonStringEnumConverter<>).MakeGenericType(tipo))
            .Select(tipo => tipo.Name)
            .ToList();

        // Assert
        Assert.HasCount(8, enums);
        Assert.IsEmpty(semConversor, $"Enums sem o JsonConverter: {string.Join(", ", semConversor)}");
    }

    private static List<Type> ObterEnumsDoDominio()
    {
        return typeof(Entidade).Assembly
            .GetTypes()
            .Where(tipo => tipo.IsEnum && tipo.Namespace == NamespaceEnums)
            .ToList();
    }

    // Os dois helpers abaixo chamam os métodos genéricos de NomeContrato com o tipo
    // concreto do enum, porque o mesmo teste recebe enums diferentes pelo DataRow.
    private static Func<Enum, string> CriarPara(Type tipoEnum)
    {
        MethodInfo metodo = typeof(NomeContrato)
            .GetMethod(nameof(NomeContrato.Para))!
            .MakeGenericMethod(tipoEnum);

        return valor => (string)metodo.Invoke(null, [valor])!;
    }

    private static Func<string?, (bool Sucesso, Enum Valor)> CriarTentarLer(Type tipoEnum)
    {
        MethodInfo metodo = typeof(NomeContrato)
            .GetMethod(nameof(NomeContrato.TentarLer))!
            .MakeGenericMethod(tipoEnum);

        return texto =>
        {
            object?[] argumentos = [texto, null];
            bool sucesso = (bool)metodo.Invoke(null, argumentos)!;
            return (sucesso, (Enum)argumentos[1]!);
        };
    }
}
