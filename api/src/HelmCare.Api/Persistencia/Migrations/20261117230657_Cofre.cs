using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelmCare.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Cofre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // As linhas antigas eram todas acessos concedidos (antes da 1.16 só havia o Conceder): o padrão
            // verdadeiro vale só para elas. Logo depois o padrão sai da coluna, como no modelo, para toda
            // linha nova gravar o Concedido que o EF manda (inclusive o falso das negações).
            migrationBuilder.AddColumn<bool>(
                name: "Concedido",
                table: "AcessosCofre",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("ALTER TABLE \"AcessosCofre\" ALTER COLUMN \"Concedido\" DROP DEFAULT;");

            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                table: "AcessosCofre",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Concedido",
                table: "AcessosCofre");

            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "AcessosCofre");
        }
    }
}
