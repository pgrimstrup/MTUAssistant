using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class SectorMapMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Stylesheet",
                table: "Sectors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Base",
                table: "Allegiances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "Allegiances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Borders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    Allegiance = table.Column<string>(type: "TEXT", nullable: true),
                    Color = table.Column<string>(type: "TEXT", nullable: true),
                    Style = table.Column<string>(type: "TEXT", nullable: true),
                    RawJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Borders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Borders_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Start = table.Column<string>(type: "TEXT", nullable: false),
                    End = table.Column<string>(type: "TEXT", nullable: false),
                    StartOffsetX = table.Column<int>(type: "INTEGER", nullable: false),
                    StartOffsetY = table.Column<int>(type: "INTEGER", nullable: false),
                    EndOffsetX = table.Column<int>(type: "INTEGER", nullable: false),
                    EndOffsetY = table.Column<int>(type: "INTEGER", nullable: false),
                    Allegiance = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: true),
                    Color = table.Column<string>(type: "TEXT", nullable: true),
                    Style = table.Column<string>(type: "TEXT", nullable: true),
                    Width = table.Column<double>(type: "REAL", nullable: true),
                    RawJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Routes_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Borders_SectorId",
                table: "Borders",
                column: "SectorId");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_SectorId",
                table: "Routes",
                column: "SectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Borders");

            migrationBuilder.DropTable(
                name: "Routes");

            migrationBuilder.DropColumn(
                name: "Stylesheet",
                table: "Sectors");

            migrationBuilder.DropColumn(
                name: "Base",
                table: "Allegiances");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "Allegiances");
        }
    }
}
