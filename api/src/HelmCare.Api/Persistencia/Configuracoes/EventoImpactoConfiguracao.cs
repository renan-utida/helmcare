using System;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelmCare.Api.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento do <see cref="EventoImpacto"/>.
/// </summary>
public sealed class EventoImpactoConfiguracao : IEntityTypeConfiguration<EventoImpacto>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EventoImpacto> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EventosImpacto");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Busca do impacto em aberto mais recente de um capacete, a cada evento MQTT.
        builder.HasIndex(e => new { e.DispositivoId, e.OcorridoEm });

        // Restrict: o registro de impactos é histórico e nunca some em cascata.
        builder.HasOne<Dispositivo>()
            .WithMany()
            .HasForeignKey(e => e.DispositivoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
