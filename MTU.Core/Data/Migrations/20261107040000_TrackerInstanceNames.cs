using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MTU.Core.Data.Migrations;

[DbContext(typeof(MtuDbContext))]
[Migration("20261107040000_TrackerInstanceNames")]
public sealed class TrackerInstanceNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            UPDATE SceneTokens SET Name = (SELECT Name FROM Characters WHERE Id = SceneTokens.CharacterId)
                || CASE WHEN (SELECT COUNT(*) FROM SceneTokens AS other
                    WHERE other.SceneId = SceneTokens.SceneId AND other.CharacterId = SceneTokens.CharacterId) > 1
                    THEN ' ' || InstanceNumber ELSE '' END
            WHERE InstanceNumber IS NOT NULL AND HasCustomName = 0;
            """
        );

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            UPDATE SceneTokens SET Name = (SELECT Name FROM Characters WHERE Id = SceneTokens.CharacterId)
            WHERE InstanceNumber IS NOT NULL AND HasCustomName = 0
              AND ("Column" IS NULL OR (SELECT COUNT(*) FROM SceneTokens AS other
                WHERE other.SceneId = SceneTokens.SceneId AND other.CharacterId = SceneTokens.CharacterId
                  AND other."Column" IS NOT NULL AND other."Row" IS NOT NULL) <= 1);
            """
        );
}
