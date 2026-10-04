using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biblioteca.Api.Migraciones.Correo
{
    /// <inheritdoc />
    public partial class MigracionInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorreoEnCola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Destinatario = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Asunto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cuerpo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FechaEnvio = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Plantilla = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DatoPlantilla = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorreoEnCola", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorreoEnCola_Estado_CreadoEn",
                table: "CorreoEnCola",
                columns: new[] { "Estado", "CreadoEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorreoEnCola");
        }
    }
}
