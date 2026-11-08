using System;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelmCare.Api.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento do <see cref="Voluntario"/>.
/// </summary>
public sealed class VoluntarioConfiguracao : IEntityTypeConfiguration<Voluntario>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Voluntario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Voluntarios");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        // Cada entregador tem no máximo um cadastro de voluntário.
        builder.HasIndex(v => v.EntregadorId).IsUnique();

        // Restrict: um voluntário com aceites registrados não some em cascata.
        builder.HasOne<Entregador>()
            .WithMany()
            .HasForeignKey(v => v.EntregadorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
