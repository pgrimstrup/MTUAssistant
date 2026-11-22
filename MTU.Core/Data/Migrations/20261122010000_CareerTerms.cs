using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations;

/// <summary>Adds per-term career history and a Birthday, and replaces the Terms Served text and
/// the stored Highest Naval and Scout Ranks, which are now worked out from the terms.</summary>
public partial class CareerTerms : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Birthday",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1
        );

        migrationBuilder.CreateTable(
            name: "CareerTerms",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                Service = table.Column<string>(type: "TEXT", nullable: false),
                SurvivalCheck = table.Column<bool>(type: "INTEGER", nullable: false),
                AdvancementCheck = table.Column<bool>(type: "INTEGER", nullable: false),
                CommissionCheck = table.Column<bool>(type: "INTEGER", nullable: false),
                Notes = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CareerTerms", x => x.Id);
                table.ForeignKey(
                    name: "FK_CareerTerms_Characters_CharacterId",
                    column: x => x.CharacterId,
                    principalTable: "Characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_CareerTerms_CharacterId_Sequence",
            table: "CareerTerms",
            columns: new[] { "CharacterId", "Sequence" }
        );

        // Turn the Terms Served text ("Navy 3, Diplomat 1", or a plain "3") into one row per term.
        // Each comma-separated entry is a service name followed by its number of terms. A name with
        // no number counts as one term, a number with no name becomes "Unknown", and a single entry
        // is capped at 20 terms. Listed services get their listed spelling; anything else is kept
        // as an unlisted (Other) service. Converted terms are marked as survived.
        migrationBuilder.Sql(
            """
            WITH RECURSIVE
            split(CharacterId, Position, Entry, Rest) AS (
                SELECT Id, 0, '', TermsServed || ','
                FROM Characters
                WHERE IsAnimal = 0 AND trim(TermsServed) <> ''
                UNION ALL
                SELECT CharacterId, Position + 1,
                    trim(substr(Rest, 1, instr(Rest, ',') - 1)),
                    substr(Rest, instr(Rest, ',') + 1)
                FROM split
                WHERE Rest <> ''
            ),
            parsed AS (
                SELECT CharacterId, Position,
                    trim(rtrim(Entry, '0123456789')) AS Name,
                    substr(Entry, length(rtrim(Entry, '0123456789')) + 1) AS Digits
                FROM split
                WHERE Position > 0 AND Entry <> ''
            ),
            named AS (
                SELECT CharacterId, Position,
                    CASE lower(Name)
                        WHEN '' THEN 'Unknown'
                        WHEN 'agent' THEN 'Agent' WHEN 'agents' THEN 'Agent'
                        WHEN 'army' THEN 'Army'
                        WHEN 'citizen' THEN 'Citizen' WHEN 'citizens' THEN 'Citizen'
                        WHEN 'drifter' THEN 'Drifter' WHEN 'drifters' THEN 'Drifter'
                        WHEN 'entertainer' THEN 'Entertainer' WHEN 'entertainers' THEN 'Entertainer'
                        WHEN 'marine' THEN 'Marine' WHEN 'marines' THEN 'Marine'
                        WHEN 'merchant' THEN 'Merchant' WHEN 'merchants' THEN 'Merchant'
                        WHEN 'military academy' THEN 'Military Academy'
                        WHEN 'navy' THEN 'Navy'
                        WHEN 'noble' THEN 'Noble' WHEN 'nobles' THEN 'Noble' WHEN 'nobility' THEN 'Noble'
                        WHEN 'rogue' THEN 'Rogue' WHEN 'rogues' THEN 'Rogue'
                        WHEN 'scholar' THEN 'Scholar' WHEN 'scholars' THEN 'Scholar'
                        WHEN 'scout' THEN 'Scout' WHEN 'scouts' THEN 'Scout'
                        WHEN 'university' THEN 'University'
                        WHEN 'prisoner' THEN 'Prisoner'
                        WHEN 'other' THEN 'Unknown'
                        ELSE Name
                    END AS Service,
                    CASE
                        WHEN Digits = '' THEN 1
                        WHEN length(Digits) > 2 THEN 20
                        ELSE min(CAST(Digits AS INTEGER), 20)
                    END AS Terms
                FROM parsed
            ),
            expanded(CharacterId, Position, Service, Term, Terms) AS (
                SELECT CharacterId, Position, Service, 1, Terms FROM named WHERE Terms >= 1
                UNION ALL
                SELECT CharacterId, Position, Service, Term + 1, Terms FROM expanded WHERE Term < Terms
            )
            INSERT INTO CareerTerms (CharacterId, Sequence, Service, SurvivalCheck, AdvancementCheck, CommissionCheck, Notes)
            SELECT CharacterId,
                ROW_NUMBER() OVER (PARTITION BY CharacterId ORDER BY Position, Term),
                Service, 1, 0, 0, ''
            FROM expanded;
            """
        );

        // Reproduce the stored ranks as successful Advancement Checks, one per term from the first
        // Navy (or Scout) term onward, for as many terms as that service has.
        migrationBuilder.Sql(
            """
            UPDATE CareerTerms SET AdvancementCheck = 1
            WHERE Service = 'Navy'
              AND (SELECT COUNT(*) FROM CareerTerms earlier
                   WHERE earlier.CharacterId = CareerTerms.CharacterId AND earlier.Service = 'Navy'
                     AND earlier.Sequence <= CareerTerms.Sequence)
                  <= (SELECT HighestNavalRank FROM Characters WHERE Id = CareerTerms.CharacterId);
            UPDATE CareerTerms SET AdvancementCheck = 1
            WHERE Service = 'Scout'
              AND (SELECT COUNT(*) FROM CareerTerms earlier
                   WHERE earlier.CharacterId = CareerTerms.CharacterId AND earlier.Service = 'Scout'
                     AND earlier.Sequence <= CareerTerms.Sequence)
                  <= (SELECT HighestScoutRank FROM Characters WHERE Id = CareerTerms.CharacterId);
            """
        );

        // A stored rank higher than the number of terms in that service cannot be reproduced, so it
        // is written into the notes of the character's first term instead of being dropped silently.
        migrationBuilder.Sql(
            """
            UPDATE CareerTerms SET Notes = trim(Notes || ' Highest Naval Rank was '
                || (SELECT HighestNavalRank FROM Characters WHERE Id = CareerTerms.CharacterId)
                || ' before career terms were added.')
            WHERE Sequence = (SELECT MIN(Sequence) FROM CareerTerms first WHERE first.CharacterId = CareerTerms.CharacterId)
              AND (SELECT HighestNavalRank FROM Characters WHERE Id = CareerTerms.CharacterId)
                  > (SELECT COUNT(*) FROM CareerTerms navy WHERE navy.CharacterId = CareerTerms.CharacterId AND navy.Service = 'Navy');
            UPDATE CareerTerms SET Notes = trim(Notes || ' Highest Scout Rank was '
                || (SELECT HighestScoutRank FROM Characters WHERE Id = CareerTerms.CharacterId)
                || ' before career terms were added.')
            WHERE Sequence = (SELECT MIN(Sequence) FROM CareerTerms first WHERE first.CharacterId = CareerTerms.CharacterId)
              AND (SELECT HighestScoutRank FROM Characters WHERE Id = CareerTerms.CharacterId)
                  > (SELECT COUNT(*) FROM CareerTerms scout WHERE scout.CharacterId = CareerTerms.CharacterId AND scout.Service = 'Scout');
            """
        );

        // SQLite drops these directly; DropColumn would need a table rebuild.
        migrationBuilder.Sql(
            """
            ALTER TABLE Characters DROP COLUMN TermsServed;
            ALTER TABLE Characters DROP COLUMN HighestNavalRank;
            ALTER TABLE Characters DROP COLUMN HighestScoutRank;
            """
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TermsServed",
            table: "Characters",
            type: "TEXT",
            nullable: false,
            defaultValue: "0"
        );

        migrationBuilder.AddColumn<int>(
            name: "HighestNavalRank",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0
        );

        migrationBuilder.AddColumn<int>(
            name: "HighestScoutRank",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0
        );

        // Rebuild the text and ranks from the terms. Notes, Survival and Commission Checks, and
        // the order of interleaved services cannot be kept in the old columns.
        migrationBuilder.Sql(
            """
            UPDATE Characters SET TermsServed = (
                SELECT group_concat(Service || ' ' || Terms, ', ')
                FROM (SELECT Service, COUNT(*) AS Terms FROM CareerTerms
                      WHERE CharacterId = Characters.Id GROUP BY Service ORDER BY MIN(Sequence)))
            WHERE EXISTS (SELECT 1 FROM CareerTerms WHERE CharacterId = Characters.Id);
            UPDATE Characters SET
                HighestNavalRank = (SELECT COUNT(*) FROM CareerTerms
                    WHERE CharacterId = Characters.Id AND Service = 'Navy' AND AdvancementCheck = 1),
                HighestScoutRank = (SELECT COUNT(*) FROM CareerTerms
                    WHERE CharacterId = Characters.Id AND Service = 'Scout' AND AdvancementCheck = 1);
            """
        );

        migrationBuilder.DropTable(name: "CareerTerms");

        migrationBuilder.Sql("ALTER TABLE Characters DROP COLUMN Birthday;");
    }
}
