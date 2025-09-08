using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIOGHOASIS.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "PAIS",
                schema: "dbo",
                columns: table => new
                {
                    PaisID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CodigoNumerico = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAIS", x => x.PaisID);
                });

            migrationBuilder.CreateTable(
                name: "PERSONA",
                schema: "dbo",
                columns: table => new
                {
                    PersonaID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PrimerNombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SegundoNombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PrimerApellido = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SegundoApellido = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApellidoCasada = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Telefono1 = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Telefono2 = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    TipoDocumentoID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nit = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    FechaNacimiento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaisID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DepartamentoID = table.Column<int>(type: "int", nullable: true),
                    MunicipioID = table.Column<int>(type: "int", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FotoPath = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PERSONA", x => x.PersonaID);
                });

            migrationBuilder.CreateTable(
                name: "PUESTO",
                schema: "dbo",
                columns: table => new
                {
                    PuestoID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Estado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PUESTO", x => x.PuestoID);
                });

            migrationBuilder.CreateTable(
                name: "ROL",
                schema: "dbo",
                columns: table => new
                {
                    RolID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROL", x => x.RolID);
                });

            migrationBuilder.CreateTable(
                name: "TIPO_DOCUMENTO",
                schema: "dbo",
                columns: table => new
                {
                    TipoDocumentoID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TIPO_DOCUMENTO", x => x.TipoDocumentoID);
                });

            migrationBuilder.CreateTable(
                name: "DEPARTAMENTO",
                schema: "dbo",
                columns: table => new
                {
                    DepartamentoID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<bool>(type: "bit", nullable: false),
                    PaisID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DEPARTAMENTO", x => x.DepartamentoID);
                    table.ForeignKey(
                        name: "FK_DEPARTAMENTO_PAIS_PaisID",
                        column: x => x.PaisID,
                        principalSchema: "dbo",
                        principalTable: "PAIS",
                        principalColumn: "PaisID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EMPLEADO",
                schema: "dbo",
                columns: table => new
                {
                    EmpleadoID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PersonaID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PuestoID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FechaContratacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EMPLEADO", x => x.EmpleadoID);
                    table.ForeignKey(
                        name: "FK_EMPLEADO_PERSONA_PersonaID",
                        column: x => x.PersonaID,
                        principalSchema: "dbo",
                        principalTable: "PERSONA",
                        principalColumn: "PersonaID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EMPLEADO_PUESTO_PuestoID",
                        column: x => x.PuestoID,
                        principalSchema: "dbo",
                        principalTable: "PUESTO",
                        principalColumn: "PuestoID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MUNICIPIO",
                schema: "dbo",
                columns: table => new
                {
                    MunicipioID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<bool>(type: "bit", nullable: false),
                    DepartamentoID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MUNICIPIO", x => x.MunicipioID);
                    table.ForeignKey(
                        name: "FK_MUNICIPIO_DEPARTAMENTO_DepartamentoID",
                        column: x => x.DepartamentoID,
                        principalSchema: "dbo",
                        principalTable: "DEPARTAMENTO",
                        principalColumn: "DepartamentoID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "USUARIO",
                schema: "dbo",
                columns: table => new
                {
                    UsuarioID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contrasena = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    EmpleadoID = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    RolID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Estado = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USUARIO", x => x.UsuarioID);
                    table.ForeignKey(
                        name: "FK_USUARIO_EMPLEADO_EmpleadoID",
                        column: x => x.EmpleadoID,
                        principalSchema: "dbo",
                        principalTable: "EMPLEADO",
                        principalColumn: "EmpleadoID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_USUARIO_ROL_RolID",
                        column: x => x.RolID,
                        principalSchema: "dbo",
                        principalTable: "ROL",
                        principalColumn: "RolID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PASSWORD_RESET_TOKENS",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioID = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PASSWORD_RESET_TOKENS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PASSWORD_RESET_TOKENS_USUARIO_UsuarioID",
                        column: x => x.UsuarioID,
                        principalSchema: "dbo",
                        principalTable: "USUARIO",
                        principalColumn: "UsuarioID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DEPARTAMENTO_PaisID",
                schema: "dbo",
                table: "DEPARTAMENTO",
                column: "PaisID");

            migrationBuilder.CreateIndex(
                name: "IX_EMPLEADO_PersonaID",
                schema: "dbo",
                table: "EMPLEADO",
                column: "PersonaID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EMPLEADO_PuestoID",
                schema: "dbo",
                table: "EMPLEADO",
                column: "PuestoID");

            migrationBuilder.CreateIndex(
                name: "IX_MUNICIPIO_DepartamentoID",
                schema: "dbo",
                table: "MUNICIPIO",
                column: "DepartamentoID");

            migrationBuilder.CreateIndex(
                name: "IX_PASSWORD_RESET_TOKENS_ExpiresAtUtc",
                schema: "dbo",
                table: "PASSWORD_RESET_TOKENS",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PASSWORD_RESET_TOKENS_UsuarioID_TokenHash",
                schema: "dbo",
                table: "PASSWORD_RESET_TOKENS",
                columns: new[] { "UsuarioID", "TokenHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USUARIO_EmpleadoID",
                schema: "dbo",
                table: "USUARIO",
                column: "EmpleadoID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USUARIO_RolID",
                schema: "dbo",
                table: "USUARIO",
                column: "RolID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MUNICIPIO",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PASSWORD_RESET_TOKENS",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TIPO_DOCUMENTO",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DEPARTAMENTO",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "USUARIO",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAIS",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EMPLEADO",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ROL",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PERSONA",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PUESTO",
                schema: "dbo");
        }
    }
}
