using System;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelmCare.Api.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento do <see cref="Turno"/>.
/// </summary>
public sealed class TurnoConfiguracao : IEntityTypeConfiguration<Turno>
{
    /// <summary>
    /// Nome do índice único que garante no máximo um turno ativo por entregador.
    /// </summary>
    public const string IndiceTurnoAtivo = "IX_Turnos_EntregadorId_Ativo";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Turno> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Turnos");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Explicacao).HasMaxLength(Turno.TamanhoMaximoExplicacao);

        // No máximo um turno ativo (Fim nulo) por entregador, garantido pelo banco mesmo se
        // duas requisições tentarem abrir turno ao mesmo tempo.
        builder.HasIndex(t => t.EntregadorId, IndiceTurnoAtivo)
            .IsUnique()
            .HasFilter("\"Fim\" IS NULL");

        // Índice comum para o histórico de turnos do entregador (e para a chave estrangeira).
        builder.HasIndex(t => new { t.EntregadorId, t.Inicio });

        // Restrict: o histórico de turnos não some em cascata.
        builder.HasOne<Entregador>()
            .WithMany()
            .HasForeignKey(t => t.EntregadorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
