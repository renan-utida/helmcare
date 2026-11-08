using System;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelmCare.Api.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento do <see cref="ContatoEmergencia"/>. A relação 1:N com o entregador está em
/// <see cref="EntregadorConfiguracao"/>.
/// </summary>
public sealed class ContatoEmergenciaConfiguracao : IEntityTypeConfiguration<ContatoEmergencia>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ContatoEmergencia> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContatosEmergencia");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Nome).HasMaxLength(ContatoEmergencia.TamanhoMaximoNome).IsRequired();
        builder.Property(c => c.Telefone).HasMaxLength(11).IsRequired();
        builder.Property(c => c.Parentesco).HasMaxLength(ContatoEmergencia.TamanhoMaximoParentesco).IsRequired();
    }
}
