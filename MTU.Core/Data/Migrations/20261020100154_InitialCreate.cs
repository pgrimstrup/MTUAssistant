using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    X = table.Column<int>(type: "INTEGER", nullable: false),
                    Y = table.Column<int>(type: "INTEGER", nullable: false),
                    Milieu = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Abbreviation = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    Credits = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    DataImportedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sectors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Allegiances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allegiances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Allegiances_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SectorNames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Lang = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorNames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectorNames_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subsectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Letter = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subsectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subsectors_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Systems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubsectorLetter = table.Column<string>(type: "TEXT", nullable: false),
                    Hex = table.Column<string>(type: "TEXT", nullable: false),
                    RelX = table.Column<int>(type: "INTEGER", nullable: false),
                    RelY = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Uwp = table.Column<string>(type: "TEXT", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", nullable: false),
                    Ix = table.Column<string>(type: "TEXT", nullable: false),
                    Ex = table.Column<string>(type: "TEXT", nullable: false),
                    Cx = table.Column<string>(type: "TEXT", nullable: false),
                    N = table.Column<string>(type: "TEXT", nullable: false),
                    B = table.Column<string>(type: "TEXT", nullable: false),
                    Z = table.Column<string>(type: "TEXT", nullable: false),
                    Pbg = table.Column<string>(type: "TEXT", nullable: false),
                    W = table.Column<string>(type: "TEXT", nullable: false),
                    A = table.Column<string>(type: "TEXT", nullable: false),
                    Stellar = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Systems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Systems_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Allegiances_SectorId_Code",
                table: "Allegiances",
                columns: new[] { "SectorId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectorNames_SectorId",
                table: "SectorNames",
                column: "SectorId");

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_Milieu_X_Y",
                table: "Sectors",
                columns: new[] { "Milieu", "X", "Y" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_Name",
                table: "Sectors",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Subsectors_SectorId_Letter",
                table: "Subsectors",
                columns: new[] { "SectorId", "Letter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Systems_Name",
                table: "Systems",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Systems_SectorId_Hex",
                table: "Systems",
                columns: new[] { "SectorId", "Hex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Allegiances");

            migrationBuilder.DropTable(
                name: "SectorNames");

            migrationBuilder.DropTable(
                name: "Subsectors");

            migrationBuilder.DropTable(
                name: "Systems");

            migrationBuilder.DropTable(
                name: "Sectors");
        }
    }
}
