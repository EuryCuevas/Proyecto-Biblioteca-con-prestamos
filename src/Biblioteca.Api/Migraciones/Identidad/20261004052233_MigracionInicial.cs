using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biblioteca.Api.Migraciones.Identidad
{
    /// <inheritdoc />
    public partial class MigracionInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "int", nullable: false),
                    BloqueadoHasta = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PasswordCambiadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CodigoRecuperacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    EmitidoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VenceEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodigoRecuperacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodigoRecuperacion_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sesion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiraEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CerradaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MotivoCierre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sesion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sesion_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TokenActivacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    EmitidoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VenceEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenActivacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokenActivacion_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodigoRecuperacion_UsuarioId_UsadoEn",
                table: "CodigoRecuperacion",
                columns: new[] { "UsuarioId", "UsadoEn" });

            migrationBuilder.CreateIndex(
                name: "UQ_CodigoRecuperacion_CodigoHash",
                table: "CodigoRecuperacion",
                column: "CodigoHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sesion_UsuarioId_CerradaEn",
                table: "Sesion",
                columns: new[] { "UsuarioId", "CerradaEn" });

            migrationBuilder.CreateIndex(
                name: "UQ_Sesion_TokenHash",
                table: "Sesion",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenActivacion_UsuarioId_UsadoEn",
                table: "TokenActivacion",
                columns: new[] { "UsuarioId", "UsadoEn" });

            migrationBuilder.CreateIndex(
                name: "UQ_TokenActivacion_TokenHash",
                table: "TokenActivacion",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_BloqueadoHasta",
                table: "Usuario",
                column: "BloqueadoHasta");

            migrationBuilder.CreateIndex(
                name: "UQ_Usuario_Correo",
                table: "Usuario",
                column: "Correo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodigoRecuperacion");

            migrationBuilder.DropTable(
                name: "Sesion");

            migrationBuilder.DropTable(
                name: "TokenActivacion");

            migrationBuilder.DropTable(
                name: "Usuario");
        }
    }
}
