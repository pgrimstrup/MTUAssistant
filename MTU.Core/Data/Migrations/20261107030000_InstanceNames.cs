using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InstanceNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCustomName",
                table: "SceneTokens",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
            migrationBuilder.Sql(
                """
                UPDATE SceneTokens SET HasCustomName = 1
                WHERE InstanceNumber IS NOT NULL
                  AND Name <> (SELECT Name FROM Characters WHERE Id = SceneTokens.CharacterId)
                  AND Name <> (SELECT Name FROM Characters WHERE Id = SceneTokens.CharacterId) || ' ' || InstanceNumber;
                UPDATE SceneTokens SET Name = (SELECT Name FROM Characters WHERE Id = SceneTokens.CharacterId)
                WHERE InstanceNumber IS NOT NULL AND HasCustomName = 0
                  AND ("Column" IS NULL OR (SELECT COUNT(*) FROM SceneTokens AS other
                       WHERE other.SceneId = SceneTokens.SceneId AND other.CharacterId = SceneTokens.CharacterId
                         AND other."Column" IS NOT NULL AND other."Row" IS NOT NULL) <= 1);
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "HasCustomName", table: "SceneTokens");
        }
    }
}
