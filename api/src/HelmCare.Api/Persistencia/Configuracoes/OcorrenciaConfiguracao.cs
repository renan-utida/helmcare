using System;
using HelmCare.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HelmCare.Api.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento da <see cref="Ocorrencia"/>, com controle de concorrência otimista.
/// </summary>
public sealed class OcorrenciaConfiguracao : IEntityTypeConfiguration<Ocorrencia>
{
    /// <summary>
    /// Nome da propriedade de sombra ligada à coluna de sistema <c>xmin</c> do PostgreSQL.
    /// </summary>
    public const string PropriedadeVersao = "Versao";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Ocorrencia> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Ocorrencias");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        // Concorrência otimista: dois voluntários podem aceitar o mesmo SOS no mesmo instante.
        // Os dois leem a ocorrência em SOS_CAMADA_1, mas o UPDATE leva o xmin lido no WHERE;
        // o segundo encontra o xmin já trocado pelo primeiro e recebe DbUpdateConcurrencyException.
        // O xmin é uma coluna de sistema do PostgreSQL: não cria coluna nova na tabela.
        builder.Property<uint>(PropriedadeVersao).IsRowVersion();

        builder.HasIndex(o => o.Estado);

        // Um SOS por impacto.
        builder.HasIndex(o => o.EventoImpactoId).IsUnique();

        // Restrict em todas as relações: o histórico de SOS nunca some em cascata.
        builder.HasOne(o => o.EventoImpacto)
            .WithOne()
            .HasForeignKey<Ocorrencia>(o => o.EventoImpactoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Entregador>()
            .WithMany()
            .HasForeignKey(o => o.EntregadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Aceites)
            .WithOne()
            .HasForeignKey(a => a.OcorrenciaId)
            .OnDelete(DeleteBehavior.Restrict);

        // A coleção pública é só leitura; o EF lê e grava pela lista privada.
        builder.Navigation(o => o.Aceites)
            .HasField("_aceites")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
