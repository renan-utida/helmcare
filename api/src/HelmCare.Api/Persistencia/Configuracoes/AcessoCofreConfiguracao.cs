using System;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelmCare.Api.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento do <see cref="AcessoCofre"/> (auditoria do Cofre).
/// </summary>
public sealed class AcessoCofreConfiguracao : IEntityTypeConfiguration<AcessoCofre>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AcessoCofre> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AcessosCofre");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Ip).HasMaxLength(AcessoCofre.TamanhoMaximoIp);

        // Consulta de GET /api/v1/cofre/auditoria/{entregadorId}, do mais recente ao mais antigo.
        builder.HasIndex(a => new { a.EntregadorId, a.AcessadoEm });

        // Restrict em todas as relações: a auditoria de acesso a dado de saúde nunca some em cascata.
        builder.HasOne<Entregador>()
            .WithMany()
            .HasForeignKey(a => a.EntregadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Ocorrencia>()
            .WithMany()
            .HasForeignKey(a => a.OcorrenciaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Socorrista>()
            .WithMany()
            .HasForeignKey(a => a.SocorristaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
