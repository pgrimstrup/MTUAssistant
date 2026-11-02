using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class TransportMarkets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransportMarkets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Hex = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportMarkets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransportMarkets_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransportMarkets_SectorId_Hex_Kind",
                table: "TransportMarkets",
                columns: new[] { "SectorId", "Hex", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransportMarkets");
        }
    }
}
