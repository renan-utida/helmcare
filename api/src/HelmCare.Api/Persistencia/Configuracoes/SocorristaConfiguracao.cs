using System;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelmCare.Api.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento do <see cref="Socorrista"/>.
/// </summary>
public sealed class SocorristaConfiguracao : IEntityTypeConfiguration<Socorrista>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Socorrista> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Socorristas");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Nome).HasMaxLength(Socorrista.TamanhoMaximoNome).IsRequired();
        builder.Property(s => s.RegistroProfissional).HasMaxLength(Socorrista.TamanhoMaximoRegistro).IsRequired();
        builder.Property(s => s.Organizacao).HasMaxLength(Socorrista.TamanhoMaximoOrganizacao).IsRequired();
        builder.Property(s => s.UsuarioId).HasMaxLength(Socorrista.TamanhoMaximoUsuarioId);

        // Um usuário do Identity pertence a no máximo um socorrista. O filtro deixa explícito
        // que socorristas ainda sem usuário (nulo) não entram na regra.
        builder.HasIndex(s => s.UsuarioId)
            .IsUnique()
            .HasFilter("\"UsuarioId\" IS NOT NULL");
    }
}
