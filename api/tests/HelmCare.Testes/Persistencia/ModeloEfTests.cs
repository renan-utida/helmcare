using System;
using System.Collections.Generic;
using System.Linq;
using HelmCare.Api.Persistencia;
using HelmCare.Api.Persistencia.Configuracoes;
using HelmCare.Api.Persistencia.Conversores;
using HelmCare.Api.Persistencia.Modelos;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Persistencia;

/// <summary>
/// Confere o modelo do EF Core sem banco: o contexto é montado com uma connection string
/// fictícia e nenhuma conexão é aberta. Usa o modelo de design (o mesmo das migrations),
/// que guarda os filtros de índice e os conversores.
/// </summary>
[TestClass]
public sealed class ModeloEfTests
{
    private static readonly Lazy<IModel> _modelo = new(MontarModelo);

    // Inclui as anuláveis (Turno.FaixaRisco, EventoImpacto.Resultado), que também precisam do conversor.
    private static readonly string[] _enumsEsperados =
    [
        "Dispositivo.EstadoAtual",
        "Turno.FaixaRisco",
        "EventoImpacto.Resultado",
        "Ocorrencia.Estado",
        "Voluntario.Tipo",
        "AcessoCofre.Origem",
        "EventoMqttProcessado.Tipo",
    ];

    /// <summary>
    /// Garante o nome explícito, no plural, da tabela de cada classe mapeada.
    /// </summary>
    /// <param name="tipo">Classe mapeada.</param>
    /// <param name="tabela">Nome esperado da tabela.</param>
    [TestMethod]
    [DataRow(typeof(Entregador), "Entregadores")]
    [DataRow(typeof(ContatoEmergencia), "ContatosEmergencia")]
    [DataRow(typeof(FichaMedica), "FichasMedicas")]
    [DataRow(typeof(Dispositivo), "Dispositivos")]
    [DataRow(typeof(Turno), "Turnos")]
    [DataRow(typeof(EventoImpacto), "EventosImpacto")]
    [DataRow(typeof(Ocorrencia), "Ocorrencias")]
    [DataRow(typeof(Voluntario), "Voluntarios")]
    [DataRow(typeof(AceiteSocorro), "AceitesSocorro")]
    [DataRow(typeof(Socorrista), "Socorristas")]
    [DataRow(typeof(AcessoCofre), "AcessosCofre")]
    [DataRow(typeof(EventoMqttProcessado), "EventosMqttProcessados")]
    public void Tabela_CadaClasse_TemNomeNoPlural(Type tipo, string tabela)
    {
        // Arrange
        IEntityType entidade = Entidade(tipo);

        // Act
        string? nome = entidade.GetTableName();

        // Assert
        Assert.AreEqual(tabela, nome);
    }

    /// <summary>
    /// Garante que o modelo tem exatamente as 12 classes mapeadas (11 entidades e a idempotência).
    /// </summary>
    [TestMethod]
    public void Modelo_Classes_TemDozeTabelas()
    {
        // Arrange e Act
        int quantidade = _modelo.Value.GetEntityTypes().Count();

        // Assert
        Assert.AreEqual(12, quantidade);
    }

    /// <summary>
    /// Garante cada índice único pedido, pelas colunas.
    /// </summary>
    /// <param name="tipo">Classe mapeada.</param>
    /// <param name="colunas">Propriedades do índice, separadas por vírgula.</param>
    [TestMethod]
    [DataRow(typeof(Entregador), "Cpf")]
    [DataRow(typeof(Dispositivo), "Codigo")]
    [DataRow(typeof(Dispositivo), "QrToken")]
    [DataRow(typeof(Dispositivo), "EntregadorId")]
    [DataRow(typeof(Voluntario), "EntregadorId")]
    [DataRow(typeof(Socorrista), "UsuarioId")]
    [DataRow(typeof(Ocorrencia), "EventoImpactoId")]
    [DataRow(typeof(AceiteSocorro), "OcorrenciaId")]
    [DataRow(typeof(FichaMedica), "EntregadorId")]
    [DataRow(typeof(EventoMqttProcessado), "CodigoDispositivo,Tipo,Ts")]
    public void Indice_Unico_Existe(Type tipo, string colunas)
    {
        // Arrange
        string[] propriedades = colunas.Split(',');

        // Act
        IIndex? indice = Indice(tipo, propriedades);

        // Assert
        Assert.IsNotNull(indice, $"Índice em {tipo.Name}({colunas}) não encontrado.");
        Assert.IsTrue(indice.IsUnique, $"Índice em {tipo.Name}({colunas}) não é único.");
    }

    /// <summary>
    /// Garante o índice único filtrado que permite um só turno ativo por entregador.
    /// </summary>
    [TestMethod]
    public void Indice_TurnoAtivo_EhUnicoComFiltroFimNulo()
    {
        // Arrange
        IEntityType turno = Entidade(typeof(Turno));

        // Act
        IIndex? indice = turno.GetIndexes().SingleOrDefault(i => i.Name == TurnoConfiguracao.IndiceTurnoAtivo);

        // Assert
        Assert.IsNotNull(indice);
        Assert.IsTrue(indice.IsUnique);
        Assert.AreEqual("\"Fim\" IS NULL", indice.GetFilter());
        Assert.AreEqual("EntregadorId", indice.Properties.Single().Name);
    }

    /// <summary>
    /// Garante que o índice único do usuário do socorrista ignora os nulos.
    /// </summary>
    [TestMethod]
    public void Indice_UsuarioDoSocorrista_FiltraNaoNulos()
    {
        // Arrange e Act
        IIndex? indice = Indice(typeof(Socorrista), "UsuarioId");

        // Assert
        Assert.IsNotNull(indice);
        Assert.AreEqual("\"UsuarioId\" IS NOT NULL", indice.GetFilter());
    }

    /// <summary>
    /// Garante os índices de consulta (não únicos).
    /// </summary>
    /// <param name="tipo">Classe mapeada.</param>
    /// <param name="colunas">Propriedades do índice, separadas por vírgula.</param>
    [TestMethod]
    [DataRow(typeof(EventoImpacto), "DispositivoId,OcorridoEm")]
    [DataRow(typeof(Ocorrencia), "Estado")]
    [DataRow(typeof(AcessoCofre), "EntregadorId,AcessadoEm")]
    public void Indice_DeConsulta_Existe(Type tipo, string colunas)
    {
        // Arrange e Act
        IIndex? indice = Indice(tipo, colunas.Split(','));

        // Assert
        Assert.IsNotNull(indice, $"Índice em {tipo.Name}({colunas}) não encontrado.");
        Assert.IsFalse(indice.IsUnique);
    }

    /// <summary>
    /// Garante o token de concorrência da ocorrência ligado ao xmin do PostgreSQL.
    /// </summary>
    [TestMethod]
    public void Ocorrencia_Versao_EhTokenDeConcorrenciaNoXmin()
    {
        // Arrange
        IEntityType ocorrencia = Entidade(typeof(Ocorrencia));

        // Act
        IProperty? versao = ocorrencia.FindProperty(OcorrenciaConfiguracao.PropriedadeVersao);

        // Assert
        Assert.IsNotNull(versao);
        Assert.IsTrue(versao.IsShadowProperty());
        Assert.IsTrue(versao.IsConcurrencyToken);
        Assert.AreEqual(typeof(uint), versao.ClrType);
        Assert.AreEqual(ValueGenerated.OnAddOrUpdate, versao.ValueGenerated);
        Assert.AreEqual("xmin", versao.GetColumnName());
    }

    /// <summary>
    /// Garante que toda coluna de enum, inclusive as anuláveis, usa o conversor do contrato e texto de até 30.
    /// </summary>
    [TestMethod]
    public void Enums_TodasAsColunas_UsamNomeContratoConverterComTamanho30()
    {
        // Arrange
        List<IProperty> propriedades = _modelo.Value.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType).IsEnum)
            .ToList();

        // Act e Assert
        List<string> nomes = propriedades.Select(p => $"{p.DeclaringType.ClrType.Name}.{p.Name}").ToList();
        CollectionAssert.IsSubsetOf(_enumsEsperados, nomes);

        foreach (IProperty propriedade in propriedades)
        {
            Type tipoEnum = Nullable.GetUnderlyingType(propriedade.ClrType) ?? propriedade.ClrType;
            Type esperado = typeof(NomeContratoConverter<>).MakeGenericType(tipoEnum);
            Type? conversor = (propriedade.GetValueConverter() ?? propriedade.GetTypeMapping().Converter)?.GetType();
            string nome = $"{propriedade.DeclaringType.ClrType.Name}.{propriedade.Name}";

            Assert.AreEqual(esperado, conversor, $"Conversor errado em {nome}.");
            Assert.AreEqual(30, propriedade.GetMaxLength(), $"Tamanho errado em {nome}.");
            Assert.AreEqual("character varying(30)", propriedade.GetColumnType(), $"Tipo de coluna errado em {nome}.");
        }
    }

    private static IModel MontarModelo()
    {
        // Connection string fictícia: montar o modelo não abre conexão.
        DbContextOptions<AppDbContext> opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=modelo_ficticio;Username=ficticio")
            .Options;

        using var contexto = new AppDbContext(opcoes);
        return contexto.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entidade(Type tipo)
    {
        IEntityType? entidade = _modelo.Value.FindEntityType(tipo);
        Assert.IsNotNull(entidade, $"{tipo.Name} não está mapeada.");
        return entidade;
    }

    private static IIndex? Indice(Type tipo, params string[] propriedades)
    {
        return Entidade(tipo).GetIndexes()
            .FirstOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(propriedades));
    }
}
