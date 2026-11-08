using System;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelmCare.Api.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento do <see cref="Entregador"/> e das partes do cadastro que pertencem a ele
/// (ficha médica e contatos de emergência).
/// </summary>
public sealed class EntregadorConfiguracao : IEntityTypeConfiguration<Entregador>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Entregador> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Entregadores");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Nome).HasMaxLength(Entregador.TamanhoMaximoNome).IsRequired();
        builder.Property(e => e.Cpf).HasMaxLength(11).IsFixedLength().IsRequired();
        builder.Property(e => e.Cnh).HasMaxLength(11).IsFixedLength().IsRequired();
        builder.Property(e => e.Telefone).HasMaxLength(11).IsRequired();

        builder.HasIndex(e => e.Cpf).IsUnique();

        // Ficha médica e contatos fazem parte do cadastro: apagar o entregador apaga os dois.
        builder.HasOne(e => e.FichaMedica)
            .WithOne()
            .HasForeignKey<FichaMedica>(f => f.EntregadorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Contatos)
            .WithOne()
            .HasForeignKey(c => c.EntregadorId)
            .OnDelete(DeleteBehavior.Cascade);

        // A coleção pública é só leitura; o EF lê e grava pela lista privada.
        builder.Navigation(e => e.Contatos)
            .HasField("_contatos")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
