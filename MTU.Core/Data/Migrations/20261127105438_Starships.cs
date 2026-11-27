using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations;

/// <summary>Adds a campaign's starships and their systems. At most one ship per campaign is
/// Equipped. Nothing existing changes.</summary>
public partial class Starships : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Starships",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CampaignId = table.Column<int>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                TechLevel = table.Column<int>(type: "INTEGER", nullable: false),
                Displacement = table.Column<int>(type: "INTEGER", nullable: false),
                DamageTaken = table.Column<int>(type: "INTEGER", nullable: false),
                PurchaseCost = table.Column<decimal>(type: "TEXT", nullable: false),
                MaintenanceCost = table.Column<decimal>(type: "TEXT", nullable: false),
                Crew = table.Column<string>(type: "TEXT", nullable: false),
                Equipped = table.Column<bool>(type: "INTEGER", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Starships", x => x.Id);
                table.ForeignKey(
                    name: "FK_Starships_Campaigns_CampaignId",
                    column: x => x.CampaignId,
                    principalTable: "Campaigns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "StarshipSystems",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                StarshipId = table.Column<int>(type: "INTEGER", nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                Displacement = table.Column<decimal>(type: "TEXT", nullable: false),
                Cost = table.Column<decimal>(type: "TEXT", nullable: false),
                PowerRequirement = table.Column<decimal>(type: "TEXT", nullable: false),
                Rating = table.Column<int>(type: "INTEGER", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StarshipSystems", x => x.Id);
                table.ForeignKey(
                    name: "FK_StarshipSystems_Starships_StarshipId",
                    column: x => x.StarshipId,
                    principalTable: "Starships",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_Starships_CampaignId_Equipped",
            table: "Starships",
            column: "CampaignId",
            unique: true,
            filter: "\"Equipped\" = 1"
        );

        migrationBuilder.CreateIndex(
            name: "IX_Starships_CampaignId_Name",
            table: "Starships",
            columns: new[] { "CampaignId", "Name" }
        );

        migrationBuilder.CreateIndex(
            name: "IX_StarshipSystems_StarshipId_Type_Sequence",
            table: "StarshipSystems",
            columns: new[] { "StarshipId", "Type", "Sequence" }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StarshipSystems");

        migrationBuilder.DropTable(name: "Starships");
    }
}
