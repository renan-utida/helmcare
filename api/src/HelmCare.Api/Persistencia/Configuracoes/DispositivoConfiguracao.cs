using System;
using System.Buffers.Text;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelmCare.Api.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento do <see cref="Dispositivo"/> (capacete).
/// </summary>
public sealed class DispositivoConfiguracao : IEntityTypeConfiguration<Dispositivo>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Dispositivo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Dispositivos");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Codigo).HasMaxLength(Dispositivo.TamanhoCodigo).IsRequired();

        // 32 bytes em Base64Url sem preenchimento dão exatamente 43 caracteres.
        builder.Property(d => d.QrToken)
            .HasMaxLength(Base64Url.GetEncodedLength(Dispositivo.BytesQrToken))
            .IsRequired();

        builder.HasIndex(d => d.Codigo).IsUnique();
        builder.HasIndex(d => d.QrToken).IsUnique();

        // Um capacete por entregador.
        builder.HasIndex(d => d.EntregadorId).IsUnique();

        // Restrict: nenhum histórico (impactos, ocorrências) some em cascata.
        builder.HasOne<Entregador>()
            .WithMany()
            .HasForeignKey(d => d.EntregadorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
