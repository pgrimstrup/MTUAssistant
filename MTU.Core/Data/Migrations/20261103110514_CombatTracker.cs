using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations;

public partial class CombatTracker : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Allocate instance IDs while preserving every existing placement.
        migrationBuilder.Sql(
            """
            CREATE TABLE "SceneTokens_Instances" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SceneTokens" PRIMARY KEY AUTOINCREMENT,
                "SceneId" INTEGER NOT NULL,
                "CharacterId" INTEGER NOT NULL,
                "Name" TEXT NOT NULL,
                "InstanceNumber" INTEGER NULL,
                "IsVisible" INTEGER NOT NULL DEFAULT 1,
                "Column" INTEGER NULL,
                "Row" INTEGER NULL,
                CONSTRAINT "CK_SceneTokens_Position" CHECK (("Column" IS NULL AND "Row" IS NULL) OR ("Column" IS NOT NULL AND "Row" IS NOT NULL AND "Column" >= 0 AND "Row" >= 0)),
                CONSTRAINT "FK_SceneTokens_Scenes_SceneId" FOREIGN KEY ("SceneId") REFERENCES "Scenes" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SceneTokens_Characters_CharacterId" FOREIGN KEY ("CharacterId") REFERENCES "Characters" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "SceneTokens_Instances" ("SceneId", "CharacterId", "Name", "InstanceNumber", "IsVisible", "Column", "Row")
            SELECT t."SceneId", t."CharacterId", CASE WHEN c."IsNpc" = 1 THEN c."Name" || ' 1' ELSE c."Name" END,
                CASE WHEN c."IsNpc" = 1 THEN 1 ELSE NULL END, 1, t."Column", t."Row"
            FROM "SceneTokens" t INNER JOIN "Characters" c ON c."Id" = t."CharacterId"
            ORDER BY t."SceneId", c."IsNpc", t."CharacterId";
            DROP TABLE "SceneTokens";
            ALTER TABLE "SceneTokens_Instances" RENAME TO "SceneTokens";
            CREATE INDEX "IX_SceneTokens_CharacterId" ON "SceneTokens" ("CharacterId");
            CREATE UNIQUE INDEX "IX_SceneTokens_SceneId_CharacterId" ON "SceneTokens" ("SceneId", "CharacterId") WHERE "InstanceNumber" IS NULL;
            CREATE UNIQUE INDEX "IX_SceneTokens_SceneId_CharacterId_InstanceNumber" ON "SceneTokens" ("SceneId", "CharacterId", "InstanceNumber");
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The old schema can represent only the first placed instance per character.
        migrationBuilder.Sql(
            """
            CREATE TABLE "SceneTokens_Legacy" (
                "SceneId" INTEGER NOT NULL,
                "CharacterId" INTEGER NOT NULL,
                "Column" INTEGER NOT NULL,
                "Row" INTEGER NOT NULL,
                CONSTRAINT "PK_SceneTokens" PRIMARY KEY ("SceneId", "CharacterId"),
                CONSTRAINT "FK_SceneTokens_Scenes_SceneId" FOREIGN KEY ("SceneId") REFERENCES "Scenes" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SceneTokens_Characters_CharacterId" FOREIGN KEY ("CharacterId") REFERENCES "Characters" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "SceneTokens_Legacy" ("SceneId", "CharacterId", "Column", "Row")
            SELECT t."SceneId", t."CharacterId", t."Column", t."Row" FROM "SceneTokens" t
            WHERE t."Column" IS NOT NULL AND t."Row" IS NOT NULL AND t."Id" = (
                SELECT MIN(s."Id") FROM "SceneTokens" s WHERE s."SceneId" = t."SceneId" AND s."CharacterId" = t."CharacterId" AND s."Column" IS NOT NULL AND s."Row" IS NOT NULL);
            DROP TABLE "SceneTokens";
            ALTER TABLE "SceneTokens_Legacy" RENAME TO "SceneTokens";
            CREATE INDEX "IX_SceneTokens_CharacterId" ON "SceneTokens" ("CharacterId");
            """
        );
    }
}
