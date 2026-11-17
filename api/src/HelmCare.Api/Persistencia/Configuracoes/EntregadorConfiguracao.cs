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
    /// <summary>
    /// Nome do índice único do CPF (o 409 da corrida no <c>TratadorExcecoes</c>).
    /// </summary>
    public const string IndiceCpf = "IX_Entregadores_Cpf";

    /// <summary>
    /// Nome do índice único da CNH.
    /// </summary>
    public const string IndiceCnh = "IX_Entregadores_Cnh";

    /// <summary>
    /// Nome do índice único do telefone.
    /// </summary>
    public const string IndiceTelefone = "IX_Entregadores_Telefone";

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

        builder.Property(e => e.UsuarioId).HasMaxLength(Entregador.TamanhoMaximoUsuarioId);

        // CPF, CNH e telefone não se repetem entre entregadores (seção 3). Os índices seguram a corrida
        // de dois cadastros simultâneos, que passam juntos pela conferência do serviço.
        builder.HasIndex(e => e.Cpf).IsUnique().HasDatabaseName(IndiceCpf);
        builder.HasIndex(e => e.Cnh).IsUnique().HasDatabaseName(IndiceCnh);
        builder.HasIndex(e => e.Telefone).IsUnique().HasDatabaseName(IndiceTelefone);

        // Uma conta do Identity pertence a no máximo um entregador. O filtro deixa de fora os
        // entregadores anteriores à 1.15, ainda sem conta (nulo), como no Socorrista.
        builder.HasIndex(e => e.UsuarioId)
            .IsUnique()
            .HasFilter("\"UsuarioId\" IS NOT NULL");

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
