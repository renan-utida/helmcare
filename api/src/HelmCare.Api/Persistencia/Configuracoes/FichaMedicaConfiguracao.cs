using System;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelmCare.Api.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento da <see cref="FichaMedica"/> (PII sensível). A relação 1:1 com o entregador
/// está em <see cref="EntregadorConfiguracao"/>.
/// </summary>
public sealed class FichaMedicaConfiguracao : IEntityTypeConfiguration<FichaMedica>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FichaMedica> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("FichasMedicas");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.TipoSanguineo).HasMaxLength(FichaMedica.TamanhoMaximoTipoSanguineo).IsRequired();
        builder.Property(f => f.Alergias).HasMaxLength(FichaMedica.TamanhoMaximoTexto).IsRequired();
        builder.Property(f => f.CondicoesCriticas).HasMaxLength(FichaMedica.TamanhoMaximoTexto).IsRequired();
        builder.Property(f => f.Medicamentos).HasMaxLength(FichaMedica.TamanhoMaximoTexto).IsRequired();
    }
}
