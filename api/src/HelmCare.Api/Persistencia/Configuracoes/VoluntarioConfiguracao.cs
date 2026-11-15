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
    /// <summary>
    /// Nome, no banco, do índice único de <c>EntregadorId</c> (o nome padrão do EF Core, já criado
    /// pela migration inicial). O <c>TratadorExcecoes</c> usa o nome para responder 409 com a
    /// mensagem certa quando dois cadastros do mesmo entregador chegam juntos.
    /// </summary>
    public const string IndiceEntregadorUnico = "IX_Voluntarios_EntregadorId";

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
