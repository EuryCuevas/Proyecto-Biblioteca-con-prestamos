using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biblioteca.Api.Migraciones.Biblioteca
{
    /// <inheritdoc />
    public partial class CatalogoSociosEjemplares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recurso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Autor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Isbn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Editorial = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AnioPublicacion = table.Column<short>(type: "smallint", nullable: true),
                    Genero = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recurso", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Socio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroSocio = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AltaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    LimitePrestamosSimultaneos = table.Column<byte>(type: "tinyint", nullable: false),
                    LimitePrestamosVencidos = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Socio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ejemplar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecursoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IngresadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Retirado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ejemplar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ejemplar_Recurso_RecursoId",
                        column: x => x.RecursoId,
                        principalTable: "Recurso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ejemplar_RecursoId",
                table: "Ejemplar",
                column: "RecursoId");

            migrationBuilder.CreateIndex(
                name: "UQ_Ejemplar_Codigo",
                table: "Ejemplar",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Recurso_Isbn",
                table: "Recurso",
                column: "Isbn",
                unique: true,
                filter: "[Isbn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_Socio_NumeroSocio",
                table: "Socio",
                column: "NumeroSocio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Socio_UsuarioId",
                table: "Socio",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Prestamo_Ejemplar_EjemplarId",
                table: "Prestamo",
                column: "EjemplarId",
                principalTable: "Ejemplar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Prestamo_Socio_SocioId",
                table: "Prestamo",
                column: "SocioId",
                principalTable: "Socio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prestamo_Ejemplar_EjemplarId",
                table: "Prestamo");

            migrationBuilder.DropForeignKey(
                name: "FK_Prestamo_Socio_SocioId",
                table: "Prestamo");

            migrationBuilder.DropTable(
                name: "Ejemplar");

            migrationBuilder.DropTable(
                name: "Socio");

            migrationBuilder.DropTable(
                name: "Recurso");
        }
    }
}
