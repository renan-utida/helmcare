using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelmCare.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Entregadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cpf = table.Column<string>(type: "character(11)", fixedLength: true, maxLength: 11, nullable: false),
                    Cnh = table.Column<string>(type: "character(11)", fixedLength: true, maxLength: 11, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entregadores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventosMqttProcessados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoDispositivo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Ts = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecebidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosMqttProcessados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Socorristas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RegistroProfissional = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Organizacao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Verificado = table.Column<bool>(type: "boolean", nullable: false),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Socorristas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContatosEmergencia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntregadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    Parentesco = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContatosEmergencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContatosEmergencia_Entregadores_EntregadorId",
                        column: x => x.EntregadorId,
                        principalTable: "Entregadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Dispositivos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    EntregadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    QrToken = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: false),
                    EstadoAtual = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EstadoAtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimoIndiceCalorC = table.Column<double>(type: "double precision", nullable: true),
                    UltimoIndiceCalorEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dispositivos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dispositivos_Entregadores_EntregadorId",
                        column: x => x.EntregadorId,
                        principalTable: "Entregadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FichasMedicas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntregadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoSanguineo = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Alergias = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CondicoesCriticas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Medicamentos = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FichasMedicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FichasMedicas_Entregadores_EntregadorId",
                        column: x => x.EntregadorId,
                        principalTable: "Entregadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Turnos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntregadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FimPrevisto = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IndiceRisco = table.Column<int>(type: "integer", nullable: true),
                    FaixaRisco = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ComponenteSinistro = table.Column<int>(type: "integer", nullable: true),
                    ComponenteChuva = table.Column<int>(type: "integer", nullable: true),
                    ComponenteCalor = table.Column<int>(type: "integer", nullable: true),
                    Explicacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProtecaoAtiva = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turnos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Turnos_Entregadores_EntregadorId",
                        column: x => x.EntregadorId,
                        principalTable: "Entregadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Voluntarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntregadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Disponivel = table.Column<bool>(type: "boolean", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Voluntarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Voluntarios_Entregadores_EntregadorId",
                        column: x => x.EntregadorId,
                        principalTable: "Entregadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventosImpacto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DispositivoId = table.Column<Guid>(type: "uuid", nullable: false),
                    OcorridoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OcorridoEmEstimado = table.Column<bool>(type: "boolean", nullable: false),
                    PicoG = table.Column<double>(type: "double precision", nullable: false),
                    OrientacaoAnormal = table.Column<bool>(type: "boolean", nullable: false),
                    PilotandoAntes = table.Column<bool>(type: "boolean", nullable: false),
                    PresencaAntes = table.Column<bool>(type: "boolean", nullable: false),
                    Resultado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ContagemTerminaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosImpacto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosImpacto_Dispositivos_DispositivoId",
                        column: x => x.DispositivoId,
                        principalTable: "Dispositivos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ocorrencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntregadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventoImpactoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Camada = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AceitaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EncerradaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TempoAteAceiteSegundos = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ocorrencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ocorrencias_Entregadores_EntregadorId",
                        column: x => x.EntregadorId,
                        principalTable: "Entregadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ocorrencias_EventosImpacto_EventoImpactoId",
                        column: x => x.EventoImpactoId,
                        principalTable: "EventosImpacto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AceitesSocorro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OcorrenciaId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoluntarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    AceitoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AceitesSocorro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AceitesSocorro_Ocorrencias_OcorrenciaId",
                        column: x => x.OcorrenciaId,
                        principalTable: "Ocorrencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AceitesSocorro_Voluntarios_VoluntarioId",
                        column: x => x.VoluntarioId,
                        principalTable: "Voluntarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AcessosCofre",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntregadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OcorrenciaId = table.Column<Guid>(type: "uuid", nullable: true),
                    SocorristaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Origem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AcessadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcessosCofre", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcessosCofre_Entregadores_EntregadorId",
                        column: x => x.EntregadorId,
                        principalTable: "Entregadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcessosCofre_Ocorrencias_OcorrenciaId",
                        column: x => x.OcorrenciaId,
                        principalTable: "Ocorrencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcessosCofre_Socorristas_SocorristaId",
                        column: x => x.SocorristaId,
                        principalTable: "Socorristas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AceitesSocorro_OcorrenciaId",
                table: "AceitesSocorro",
                column: "OcorrenciaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AceitesSocorro_VoluntarioId",
                table: "AceitesSocorro",
                column: "VoluntarioId");

            migrationBuilder.CreateIndex(
                name: "IX_AcessosCofre_EntregadorId_AcessadoEm",
                table: "AcessosCofre",
                columns: new[] { "EntregadorId", "AcessadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_AcessosCofre_OcorrenciaId",
                table: "AcessosCofre",
                column: "OcorrenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_AcessosCofre_SocorristaId",
                table: "AcessosCofre",
                column: "SocorristaId");

            migrationBuilder.CreateIndex(
                name: "IX_ContatosEmergencia_EntregadorId",
                table: "ContatosEmergencia",
                column: "EntregadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_Codigo",
                table: "Dispositivos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_EntregadorId",
                table: "Dispositivos",
                column: "EntregadorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_QrToken",
                table: "Dispositivos",
                column: "QrToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entregadores_Cpf",
                table: "Entregadores",
                column: "Cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventosImpacto_DispositivoId_OcorridoEm",
                table: "EventosImpacto",
                columns: new[] { "DispositivoId", "OcorridoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosMqttProcessados_CodigoDispositivo_Tipo_Ts",
                table: "EventosMqttProcessados",
                columns: new[] { "CodigoDispositivo", "Tipo", "Ts" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FichasMedicas_EntregadorId",
                table: "FichasMedicas",
                column: "EntregadorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_EntregadorId",
                table: "Ocorrencias",
                column: "EntregadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_Estado",
                table: "Ocorrencias",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_EventoImpactoId",
                table: "Ocorrencias",
                column: "EventoImpactoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Socorristas_UsuarioId",
                table: "Socorristas",
                column: "UsuarioId",
                unique: true,
                filter: "\"UsuarioId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_EntregadorId_Ativo",
                table: "Turnos",
                column: "EntregadorId",
                unique: true,
                filter: "\"Fim\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_EntregadorId_Inicio",
                table: "Turnos",
                columns: new[] { "EntregadorId", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Voluntarios_EntregadorId",
                table: "Voluntarios",
                column: "EntregadorId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AceitesSocorro");

            migrationBuilder.DropTable(
                name: "AcessosCofre");

            migrationBuilder.DropTable(
                name: "ContatosEmergencia");

            migrationBuilder.DropTable(
                name: "EventosMqttProcessados");

            migrationBuilder.DropTable(
                name: "FichasMedicas");

            migrationBuilder.DropTable(
                name: "Turnos");

            migrationBuilder.DropTable(
                name: "Voluntarios");

            migrationBuilder.DropTable(
                name: "Ocorrencias");

            migrationBuilder.DropTable(
                name: "Socorristas");

            migrationBuilder.DropTable(
                name: "EventosImpacto");

            migrationBuilder.DropTable(
                name: "Dispositivos");

            migrationBuilder.DropTable(
                name: "Entregadores");
        }
    }
}
