using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biblioteca.Api.Migraciones.Biblioteca
{
    /// <inheritdoc />
    public partial class MigracionInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Prestamo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SocioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EjemplarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SolicitadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EntregadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResueltoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FechaLimite = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prestamo", x => x.Id);
                    table.CheckConstraint("CK_Prestamo_Estado", "Estado IN ('Solicitado','Activo','Devuelto','Rechazado','Perdido')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prestamo_EjemplarId_Estado",
                table: "Prestamo",
                columns: new[] { "EjemplarId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Prestamo_SocioId_Estado",
                table: "Prestamo",
                columns: new[] { "SocioId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "UX_Prestamo_EjemplarVivo",
                table: "Prestamo",
                column: "EjemplarId",
                unique: true,
                filter: "[Estado] IN ('Solicitado','Activo')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Prestamo");
        }
    }
}
