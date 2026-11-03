using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace MTU.Core.Data.Migrations;

public partial class CharacterNaming : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Preserve existing IDs, data, and foreign-key relationships in place.
        migrationBuilder.RenameTable(name: "Travellers", newName: "Characters");
        migrationBuilder.RenameColumn(name: "TravellerName", table: "Characters", newName: "Name");
        migrationBuilder.RenameTable(name: "PatronsAndEnemies", newName: "NPCs");
        migrationBuilder.RenameColumn(name: "TravellerId", table: "NPCs", newName: "CharacterId");
        migrationBuilder.RenameTable(name: "TravellerSkills", newName: "CharacterSkills");
        migrationBuilder.RenameColumn(
            name: "TravellerId",
            table: "CharacterSkills",
            newName: "CharacterId"
        );
        migrationBuilder.RenameTable(name: "TravellerEquipment", newName: "CharacterEquipment");
        migrationBuilder.RenameColumn(
            name: "TravellerId",
            table: "CharacterEquipment",
            newName: "CharacterId"
        );
        migrationBuilder.DropIndex(
            name: "IX_Travellers_CampaignId_TravellerName",
            table: "Characters"
        );
        migrationBuilder.DropIndex(
            name: "IX_TravellerSkills_TravellerId_Name",
            table: "CharacterSkills"
        );
        migrationBuilder.DropIndex(
            name: "IX_TravellerEquipment_EquipmentId",
            table: "CharacterEquipment"
        );
        migrationBuilder.CreateIndex(
            name: "IX_Characters_CampaignId_Name",
            table: "Characters",
            columns: new[] { "CampaignId", "Name" }
        );
        migrationBuilder.CreateIndex(
            name: "IX_CharacterSkills_CharacterId_Name",
            table: "CharacterSkills",
            columns: new[] { "CharacterId", "Name" },
            unique: true
        );
        migrationBuilder.CreateIndex(
            name: "IX_CharacterEquipment_EquipmentId",
            table: "CharacterEquipment",
            column: "EquipmentId"
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Characters_CampaignId_Name", table: "Characters");
        migrationBuilder.DropIndex(
            name: "IX_CharacterSkills_CharacterId_Name",
            table: "CharacterSkills"
        );
        migrationBuilder.DropIndex(
            name: "IX_CharacterEquipment_EquipmentId",
            table: "CharacterEquipment"
        );
        migrationBuilder.RenameColumn(name: "Name", table: "Characters", newName: "TravellerName");
        migrationBuilder.RenameColumn(name: "CharacterId", table: "NPCs", newName: "TravellerId");
        migrationBuilder.RenameColumn(
            name: "CharacterId",
            table: "CharacterSkills",
            newName: "TravellerId"
        );
        migrationBuilder.RenameColumn(
            name: "CharacterId",
            table: "CharacterEquipment",
            newName: "TravellerId"
        );
        migrationBuilder.RenameTable(name: "NPCs", newName: "PatronsAndEnemies");
        migrationBuilder.RenameTable(name: "CharacterSkills", newName: "TravellerSkills");
        migrationBuilder.RenameTable(name: "CharacterEquipment", newName: "TravellerEquipment");
        migrationBuilder.RenameTable(name: "Characters", newName: "Travellers");
        migrationBuilder.CreateIndex(
            name: "IX_Travellers_CampaignId_TravellerName",
            table: "Travellers",
            columns: new[] { "CampaignId", "TravellerName" }
        );
        migrationBuilder.CreateIndex(
            name: "IX_TravellerSkills_TravellerId_Name",
            table: "TravellerSkills",
            columns: new[] { "TravellerId", "Name" },
            unique: true
        );
        migrationBuilder.CreateIndex(
            name: "IX_TravellerEquipment_EquipmentId",
            table: "TravellerEquipment",
            column: "EquipmentId"
        );
    }
}
