using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biblioteca.Api.Migraciones.Permisos
{
    /// <inheritdoc />
    public partial class MigracionInicialPermisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Permiso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OtorgadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permiso", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudPermiso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AprobadaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AprobadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AplicadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RechazadaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RechazadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudPermiso", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Permiso_UsuarioId_Operacion",
                table: "Permiso",
                columns: new[] { "UsuarioId", "Operacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudPermiso_Estado",
                table: "SolicitudPermiso",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudPermiso_SolicitanteId",
                table: "SolicitudPermiso",
                column: "SolicitanteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Permiso");

            migrationBuilder.DropTable(
                name: "SolicitudPermiso");
        }
    }
}
