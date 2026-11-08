using System;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelmCare.Api.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento do <see cref="AceiteSocorro"/>. A relação com a ocorrência está em
/// <see cref="OcorrenciaConfiguracao"/>.
/// </summary>
public sealed class AceiteSocorroConfiguracao : IEntityTypeConfiguration<AceiteSocorro>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AceiteSocorro> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AceitesSocorro");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        // Um aceite por ocorrência: segunda barreira, no banco, contra dois aceites
        // simultâneos (a primeira é o xmin da ocorrência).
        builder.HasIndex(a => a.OcorrenciaId).IsUnique();

        // Restrict: o aceite é o registro do indicador de impacto e não some em cascata.
        builder.HasOne<Voluntario>()
            .WithMany()
            .HasForeignKey(a => a.VoluntarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
