using System;
using HelmCare.Api.Persistencia.Modelos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelmCare.Api.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento do <see cref="EventoMqttProcessado"/> (idempotência dos eventos MQTT).
/// </summary>
public sealed class EventoMqttProcessadoConfiguracao : IEntityTypeConfiguration<EventoMqttProcessado>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EventoMqttProcessado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EventosMqttProcessados");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.CodigoDispositivo).HasMaxLength(EventoMqttProcessado.TamanhoMaximoCodigo).IsRequired();

        // Seção 6.2: mesmo dispositivo, tipo e ts são um evento só. O banco recusa o repetido.
        builder.HasIndex(e => new { e.CodigoDispositivo, e.Tipo, e.Ts }).IsUnique();
    }
}
