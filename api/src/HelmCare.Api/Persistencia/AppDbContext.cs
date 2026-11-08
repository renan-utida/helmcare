using System;
using HelmCare.Api.Persistencia.Conversores;
using HelmCare.Api.Persistencia.Modelos;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.EntityFrameworkCore;

namespace HelmCare.Api.Persistencia;

/// <summary>
/// Contexto do EF Core para o banco <c>helmcare_api</c> (PostgreSQL). O mapeamento de cada
/// classe fica em <c>Persistencia/Configuracoes</c>, uma classe por entidade.
/// </summary>
public sealed class AppDbContext : DbContext
{
    /// <summary>
    /// Cria o contexto com as opções registradas na injeção de dependência.
    /// </summary>
    /// <param name="options">Opções do contexto (provedor e connection string).</param>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Entregadores cadastrados.
    /// </summary>
    public DbSet<Entregador> Entregadores => Set<Entregador>();

    /// <summary>
    /// Contatos de emergência dos entregadores.
    /// </summary>
    public DbSet<ContatoEmergencia> ContatosEmergencia => Set<ContatoEmergencia>();

    /// <summary>
    /// Fichas médicas (PII sensível).
    /// </summary>
    public DbSet<FichaMedica> FichasMedicas => Set<FichaMedica>();

    /// <summary>
    /// Capacetes.
    /// </summary>
    public DbSet<Dispositivo> Dispositivos => Set<Dispositivo>();

    /// <summary>
    /// Turnos de trabalho.
    /// </summary>
    public DbSet<Turno> Turnos => Set<Turno>();

    /// <summary>
    /// Impactos detectados pelos capacetes.
    /// </summary>
    public DbSet<EventoImpacto> EventosImpacto => Set<EventoImpacto>();

    /// <summary>
    /// Ocorrências de SOS.
    /// </summary>
    public DbSet<Ocorrencia> Ocorrencias => Set<Ocorrencia>();

    /// <summary>
    /// Voluntários da rede de socorro.
    /// </summary>
    public DbSet<Voluntario> Voluntarios => Set<Voluntario>();

    /// <summary>
    /// Aceites de socorro.
    /// </summary>
    public DbSet<AceiteSocorro> AceitesSocorro => Set<AceiteSocorro>();

    /// <summary>
    /// Socorristas com acesso ao Cofre.
    /// </summary>
    public DbSet<Socorrista> Socorristas => Set<Socorrista>();

    /// <summary>
    /// Auditoria dos acessos ao Cofre.
    /// </summary>
    public DbSet<AcessoCofre> AcessosCofre => Set<AcessoCofre>();

    /// <summary>
    /// Registro de idempotência dos eventos MQTT.
    /// </summary>
    public DbSet<EventoMqttProcessado> EventosMqttProcessados => Set<EventoMqttProcessado>();

    /// <inheritdoc/>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Todo enum do contrato vira texto com o nome exato do contrato (ex.: SOS_CAMADA_1).
        // A convenção por tipo também vale para as propriedades anuláveis (ex.: Turno.FaixaRisco).
        UsarNomeContrato<EstadoDispositivo>(configurationBuilder);
        UsarNomeContrato<EstadoOcorrencia>(configurationBuilder);
        UsarNomeContrato<ResultadoImpacto>(configurationBuilder);
        UsarNomeContrato<FaixaRisco>(configurationBuilder);
        UsarNomeContrato<TipoVoluntario>(configurationBuilder);
        UsarNomeContrato<OrigemAcessoCofre>(configurationBuilder);
        UsarNomeContrato<TipoEventoMqtt>(configurationBuilder);
        UsarNomeContrato<TipoComandoMqtt>(configurationBuilder);
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private static void UsarNomeContrato<TEnum>(ModelConfigurationBuilder configurationBuilder)
        where TEnum : struct, Enum
    {
        configurationBuilder.Properties<TEnum>()
            .HaveConversion<NomeContratoConverter<TEnum>>()
            .HaveMaxLength(NomeContratoConverter<TEnum>.TamanhoMaximo);
    }
}
