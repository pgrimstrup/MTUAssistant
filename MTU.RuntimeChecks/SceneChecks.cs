using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class SceneChecks
{
    public static async Task RunAsync()
    {
        SceneImageChecks.Run();
        CombatActionChecks.Run();
        static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
        static async Task Reject(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new Exception("Invalid scene operation accepted");
        }
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<MtuDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<SceneService>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103071653_NpcType");
        db.Campaigns.AddRange(
            new CampaignEntity { Id = 1, Name = "Scenes" },
            new CampaignEntity { Id = 2, Name = "Other" }
        );
        await db.SaveChangesAsync();
        await migrator.MigrateAsync("20261103095749_Scenes");
        // Insert using the historical schema before the encounter-state column exists.
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Scenes" ("CampaignId", "Name", "Image", "ContentType", "GridSize", "GridColour", "GridOpacity", "GridlineWidth", "Revision")
            VALUES (1, 'Existing scene', X'01', 'image/png', 100, '#000000', 50, 2, 1)
            """
        );
        await migrator.MigrateAsync("20261103103811_SceneTokens");
        var legacyScene = await db
            .Scenes.Where(s => s.Name == "Existing scene")
            .Select(s => new { s.Id })
            .SingleAsync();
        // Historical fixture must not reference character columns added by later migrations.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Characters (Id, CampaignId, Name, PlayerName, Race, Upp, TermsServed, HighestNavalRank, HighestScoutRank, NaturalDr, Inactive, IsNpc) VALUES (301,1,'Legacy Traveller','','','','0',0,0,0,0,0),(302,1,'Legacy Mercenary','','','','0',0,0,0,0,1)"
        );
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO SceneTokens (SceneId, CharacterId, \"Column\", \"Row\") VALUES ({legacyScene.Id}, {301}, {2}, {3}), ({legacyScene.Id}, {302}, {4}, {5})"
        );
        await migrator.MigrateAsync();
        Check(await db.Campaigns.CountAsync() == 2, "Scene migration preserves campaigns");
        Check(
            await db.Scenes.AnyAsync(s => s.Name == "Existing scene"),
            "Token migration preserves existing scenes"
        );
        var store = scope.ServiceProvider.GetRequiredService<SceneService>();
        var png = SceneImageChecks.Png(16, 16);
        var input = new SceneInput { CampaignId = 1, Name = "Hangar" };
        var saved = await store.SaveAsync(input, png);
        Check(
            saved.GridSize == 100
                && saved.GridColour == "#000000"
                && saved.GridOpacity == 50
                && saved.GridlineWidth == 2,
            "Scene defaults are 100px, black, 50 percent and 2px"
        );
        Check(
            (await store.ImageAsync(1, saved.Id))!.Bytes.SequenceEqual(png),
            "Background image persists"
        );
        Check(
            !(await store.ListAsync(2)).Any()
                && await store.FindAsync(2, saved.Id) is null
                && await store.ImageAsync(2, saved.Id) is null,
            "Scenes and images are campaign scoped"
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput
                {
                    Id = saved.Id,
                    CampaignId = 2,
                    Name = "Wrong campaign",
                }
            )
        );
        await Reject(() => store.DeleteAsync(2, saved.Id));
        var updated = await store.SaveAsync(
            new SceneInput
            {
                Id = saved.Id,
                CampaignId = 1,
                Name = "Deck",
                GridSize = 75,
                GridColour = "#AABBCC",
                GridOpacity = 0,
                GridlineWidth = 1.5,
            }
        );
        Check(
            updated.Name == "Deck"
                && updated.GridSize == 75
                && updated.GridColour == "#AABBCC"
                && updated.GridOpacity == 0
                && updated.GridlineWidth == 1.5
                && updated.Revision > saved.Revision
                && (await store.ImageAsync(1, saved.Id))!.Bytes.SequenceEqual(png),
            "Editing grid settings retains image and advances revision"
        );
        var replacement = Convert.FromBase64String(
            "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"
        );
        await store.SaveAsync(
            new SceneInput
            {
                Id = saved.Id,
                CampaignId = 1,
                Name = "Deck",
            },
            replacement
        );
        Check(
            (await store.ImageAsync(1, saved.Id)) is { ContentType: "image/gif" },
            "Image replacement updates its content type"
        );
        await Reject(() => store.SaveAsync(new SceneInput { CampaignId = 1, Name = "" }, png));
        await Reject(() => store.SaveAsync(new SceneInput { CampaignId = 1, Name = "No image" }));
        await Reject(() =>
            store.SaveAsync(new SceneInput { CampaignId = 1, Name = "Invalid image" }, [1, 2, 3])
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput
                {
                    CampaignId = 1,
                    Name = "Grid",
                    GridSize = 0,
                },
                png
            )
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput
                {
                    CampaignId = 1,
                    Name = "Grid",
                    GridOpacity = 101,
                },
                png
            )
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput
                {
                    CampaignId = 1,
                    Name = "Grid",
                    GridColour = "red",
                },
                png
            )
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput
                {
                    CampaignId = 1,
                    Name = "Grid",
                    GridlineWidth = double.NaN,
                },
                png
            )
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput
                {
                    CampaignId = 1,
                    Name = "Grid",
                    GridlineWidth = 101,
                },
                png
            )
        );
        var maximum = SceneImageChecks.Png(
            3000,
            2000,
            System.IO.Compression.CompressionLevel.NoCompression
        );
        var large = await store.SaveAsync(
            new SceneInput { CampaignId = 1, Name = "Large" },
            maximum
        );
        Check(
            large.ImageBytes > 20 * 1024 * 1024,
            "Valid images larger than the former 20 MB limit are accepted"
        );
        var square = await store.SaveAsync(
            new SceneInput { CampaignId = 1, Name = "Maximum dimensions" },
            SceneImageChecks.Png(4096, 4096)
        );
        Check(square.ImageBytes > 0, "4096 by 4096 images can be saved");
        await store.DeleteAsync(1, square.Id);
        await Reject(() =>
            store.SaveAsync(
                new SceneInput { CampaignId = 1, Name = "Too wide" },
                SceneImageChecks.Png(4097, 1)
            )
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput { CampaignId = 1, Name = "Too tall" },
                SceneImageChecks.Png(1, 4097)
            )
        );
        var legacyEntries = await store.CombatantsAsync(1, legacyScene.Id);
        Check(
            legacyEntries.Count == 2
                && legacyEntries.Select(t => t.Id).Distinct().Count() == 2
                && legacyEntries.Single(t => t.CharacterId == 302)
                    is { Name: "Legacy Mercenary 1", Column: 4, Row: 5, IsVisible: true }
                && legacyEntries.Single(t => t.CharacterId == 301) is { Column: 2, Row: 3 },
            "Migration preserves placements and allocates independent instance IDs"
        );
        db.Characters.AddRange(
            new CharacterEntity
            {
                Id = 101,
                CampaignId = 1,
                Name = "Traveller",
                TokenImage = png,
            },
            new CharacterEntity
            {
                Id = 102,
                CampaignId = 1,
                Name = "Mercenary",
                IsNpc = true,
            },
            new CharacterEntity
            {
                Id = 103,
                CampaignId = 2,
                Name = "Other campaign",
                IsNpc = true,
            },
            new CharacterEntity
            {
                Id = 104,
                CampaignId = 1,
                Name = "Inactive",
                Inactive = true,
            }
        );
        await db.SaveChangesAsync();
        var choices = await store.NpcChoicesAsync(1);
        Check(
            choices.Any(c => c.CharacterId == 102)
                && choices.Any(c => c.CharacterId == 101 && !c.IsNpc)
                && !choices.Any(c => c.CharacterId is 103 or 104),
            "Character search includes active Travellers and NPCs and excludes inactive Travellers and other campaigns"
        );
        Check(
            (await store.TokenImageAsync(1, 101))!.Bytes.SequenceEqual(png)
                && await store.TokenImageAsync(2, 101) is null,
            "Token images remain campaign scoped"
        );
        await store.PruneInactiveTravellersAsync(1, saved.Id);
        await store.PruneInactiveTravellersAsync(1, saved.Id);
        Check(
            !(await store.CombatantsAsync(1, saved.Id)).Any(),
            "Opening a new scene does not automatically add active Travellers"
        );
        await store.AddCharacterAsync(1, saved.Id, 101);
        await store.AddCharacterAsync(1, saved.Id, 101);
        var roster = await store.CombatantsAsync(1, saved.Id);
        Check(
            roster.Count == 1
                && roster.All(t => !t.IsNpc && !t.OnMap)
                && !roster.Any(t => t.CharacterId == 104),
            "Explicit picker additions add Travellers once, unplaced, without inactive players"
        );
        db.CharacterSkills.Add(
            new CharacterSkillEntity
            {
                CharacterId = 101,
                Name = "Tactics",
                Level = 0,
            }
        );
        await db.SaveChangesAsync();
        Check(
            (await store.CombatantsAsync(1, saved.Id)).Single(t => t.CharacterId == 101).Tactics
                == 0,
            "Scene roster loads generic Tactics for pre-engagement training details"
        );
        var traveller = roster.Single(t => t.CharacterId == 101);
        var squad = new List<SceneCombatant>();
        for (var i = 0; i < 4; i++)
            squad.Add(await store.AddNpcInstanceAsync(1, saved.Id, 102));
        Check(
            squad.Select(t => t.Id).Distinct().Count() == 4
                && squad
                    .Select(t => t.Name)
                    .SequenceEqual(
                        new[] { "Mercenary 1", "Mercenary 2", "Mercenary 3", "Mercenary 4" }
                    ),
            "Repeated NPCs have unique identities and numbered names"
        );
        await store.RenameNpcInstanceAsync(1, saved.Id, squad[1].Id, "Squad leader");
        Check(
            (await db.Characters.AsNoTracking().SingleAsync(c => c.Id == 102)).Name == "Mercenary",
            "Instance rename does not change the source NPC"
        );
        await store.PositionCombatantAsync(1, saved.Id, squad[0].Id, 2, 3);
        await store.PositionCombatantAsync(1, saved.Id, squad[1].Id, 4, 5);
        await store.SetCombatantVisibilityAsync(1, saved.Id, squad[0].Id, false);
        await store.PositionCombatantAsync(1, saved.Id, squad[0].Id, 6, 7);
        roster = await store.CombatantsAsync(1, saved.Id);
        Check(
            roster
                .Select(t => t.IsNpc)
                .SequenceEqual(roster.OrderBy(t => t.IsNpc).Select(t => t.IsNpc)),
            "Travellers sort before NPC instances"
        );
        Check(
            roster.Single(t => t.Id == squad[0].Id) is { Column: 6, Row: 7, IsVisible: false }
                && roster.Single(t => t.Id == squad[1].Id)
                    is { Name: "Squad leader", Column: 4, Row: 5, IsVisible: true },
            "Instance names, moves and visibility persist independently"
        );
        await store.PositionCombatantAsync(1, saved.Id, squad[1].Id, null, null);
        Check(
            (await store.CombatantsAsync(1, saved.Id)).Single(t => t.Id == squad[1].Id)
                is { OnMap: false, Name: "Squad leader" },
            "Unplacing retains the instance in the tracker"
        );
        Check(
            (await store.AddNpcInstanceAsync(1, saved.Id, 102)).Name == "Mercenary 5",
            "Numbering is independent of edited names"
        );
        await Reject(() => store.AddNpcInstanceAsync(1, saved.Id, 101));
        await Reject(() => store.AddNpcInstanceAsync(1, saved.Id, 103));
        await Reject(() => store.PruneInactiveTravellersAsync(2, saved.Id));
        await Reject(() => store.PositionCombatantAsync(2, saved.Id, squad[0].Id, 0, 0));
        await Reject(() => store.PositionCombatantAsync(1, large.Id, squad[0].Id, 0, 0));
        await Reject(() => store.PositionCombatantAsync(1, saved.Id, squad[0].Id, -1, 0));
        await Reject(() => store.PositionCombatantAsync(1, saved.Id, squad[0].Id, null, 0));
        await Reject(() => store.SetCombatantVisibilityAsync(2, saved.Id, squad[0].Id, true));
        await Reject(() => store.RenameNpcInstanceAsync(2, saved.Id, squad[0].Id, "Wrong"));
        await Reject(() => store.RenameNpcInstanceAsync(1, saved.Id, squad[0].Id, " "));
        await Reject(() => store.RenameNpcInstanceAsync(1, saved.Id, traveller.Id, "Not an NPC"));
        await Reject(() => store.RemoveNpcInstanceAsync(1, saved.Id, traveller.Id));
        await Reject(() => store.RemoveNpcInstanceAsync(2, saved.Id, squad[0].Id));
        Check(!(await store.CombatantsAsync(2, saved.Id)).Any(), "Rosters are campaign scoped");
        var beforeGrid = (await store.FindAsync(1, saved.Id))!;
        var liveGrid = await store.SaveGridAsync(1, saved.Id, "#ffffff", 3.5, 30);
        Check(
            liveGrid.Revision == beforeGrid.Revision
                && liveGrid.GridlineWidth == 3.5
                && liveGrid.GridOpacity == 30
                && liveGrid.GridColour == "#ffffff"
                && (await store.ImageAsync(1, saved.Id))!.Bytes.SequenceEqual(replacement),
            "Live grid edits persist without resetting camera revision or replacing the image"
        );
        await Reject(() => store.SaveGridAsync(2, saved.Id, "#ffffff", 2, 30));
        await Reject(() => store.SaveGridAsync(1, saved.Id, "red", 2, 30));
        await Reject(() => store.SaveGridAsync(1, saved.Id, "#ffffff", 0, 30));
        await Reject(() => store.SaveGridAsync(1, saved.Id, "#ffffff", double.NaN, 30));
        await Reject(() => store.SaveGridAsync(1, saved.Id, "#ffffff", 2, 101));
        var resizedGrid = await store.SaveGridAsync(1, saved.Id, "#ffffff", 2, 30, gridSize: 10);
        Check(
            resizedGrid.GridSize == 10
                && resizedGrid.Revision == beforeGrid.Revision
                && (await store.FindAsync(1, saved.Id))!.GridSize == 10
                && (await store.CombatantsAsync(1, saved.Id)).Single(t => t.Id == squad[0].Id)
                    is { Column: 6, Row: 7 },
            "Toolbar grid size persists without changing token cells or camera revision"
        );
        await Reject(() => store.SaveGridAsync(1, saved.Id, "#ffffff", 2, 30, gridSize: 0));
        await Reject(() => store.SaveGridAsync(1, saved.Id, "#ffffff", 2, 30, gridSize: -1));
        await Reject(() => store.SaveGridAsync(1, saved.Id, "#ffffff", 3, 30, gridSize: 2));
        foreach (var inCombat in new[] { false, true })
        {
            var visibilityRoster = (await store.CombatantsAsync(1, saved.Id))
                .Select(e => e with { Upp = "777777" })
                .ToArray();
            var visibilityEncounter = new Encounter();
            if (inCombat)
                visibilityEncounter.Begin(
                    visibilityRoster,
                    true,
                    true,
                    0,
                    new Dictionary<int, int>(),
                    () => 7
                );
            await store.SaveEncounterAsync(1, saved.Id, visibilityEncounter.Snapshot());
            foreach (var id in new[] { traveller.Id, squad[0].Id })
            {
                var beforeVisibility = visibilityRoster.Single(e => e.Id == id);
                var hiddenEntry = await store.SetCombatantVisibilityAsync(1, saved.Id, id, false);
                Check(
                    !hiddenEntry.IsVisible
                        && hiddenEntry.Column == beforeVisibility.Column
                        && hiddenEntry.Row == beforeVisibility.Row,
                    "Visibility can be cleared without moving Traveller or NPC tokens in either combat mode"
                );
                Check(
                    !(await store.CombatantsAsync(1, saved.Id)).Single(e => e.Id == id).IsVisible,
                    "Hidden token visibility persists"
                );
                Check(
                    (await store.SetCombatantVisibilityAsync(1, saved.Id, id, true)).IsVisible,
                    "Traveller and NPC tokens can be revealed again in either combat mode"
                );
            }
        }
        await store.SaveEncounterAsync(1, saved.Id, new Encounter().Snapshot());
        await store.RemoveNpcInstanceAsync(1, saved.Id, squad[0].Id);
        Check(
            (await store.CombatantsAsync(1, saved.Id)).Count == 5,
            "Removing an instance leaves other instances and Travellers intact"
        );
        var player = await db.Characters.SingleAsync(c => c.Id == 101);
        player.Inactive = true;
        await db.SaveChangesAsync();
        await store.PruneInactiveTravellersAsync(1, saved.Id);
        Check(
            !(await store.CombatantsAsync(1, saved.Id)).Any(t => t.CharacterId == 101),
            "Newly inactive players are removed from the encounter"
        );
        player.Inactive = false;
        await db.SaveChangesAsync();
        await store.PruneInactiveTravellersAsync(1, saved.Id);
        Check(
            !(await store.CombatantsAsync(1, saved.Id)).Any(t => t.CharacterId == 101),
            "Reactivating a Traveller does not automatically add them to a scene"
        );
        await store.AddCharacterAsync(1, saved.Id, 101);
        Check(
            (await store.CombatantsAsync(1, saved.Id)).Count(t => t.CharacterId == 101) == 1,
            "Reactivated Travellers can be added explicitly"
        );
        db.Characters.Remove(await db.Characters.SingleAsync(c => c.Id == 102));
        await db.SaveChangesAsync();
        Check(
            !(await store.CombatantsAsync(1, saved.Id)).Any(t => t.CharacterId == 102),
            "Character deletion removes all instances"
        );
        var healthEntry = new SceneCombatant(
            801,
            101,
            "Health test",
            false,
            false,
            true,
            0,
            0,
            "896777"
        );
        var healthRoster = new[] { healthEntry };
        var healthEncounter = new Encounter();
        void ExpectHealth(int str, int dex, int end, string message)
        {
            var actual = healthEncounter.Health(healthEntry);
            Check(
                (actual.Strength, actual.Dexterity, actual.Endurance) == (str, dex, end),
                message
            );
            Check(
                healthEncounter.Hp(healthEntry) == str + dex + end,
                "HP is calculated from current attributes"
            );
        }
        healthEncounter.SetHp(801, 18, healthRoster);
        ExpectHealth(8, 9, 1, "Damage reduces END first");
        healthEncounter.SetHp(801, 10, healthRoster);
        ExpectHealth(8, 2, 0, "Damage spills from END to higher DEX");
        healthEncounter.SetHp(801, 7, healthRoster);
        ExpectHealth(5, 2, 0, "Next adjustment chooses the currently higher STR");
        healthEncounter.SetHp(801, 11, healthRoster);
        ExpectHealth(8, 3, 0, "HP increases undo reductions in reverse chronological order");
        var healthJson = System.Text.Json.JsonSerializer.Serialize(healthEncounter.Snapshot());
        Check(
            !healthJson.Contains("HitPoints"),
            "New encounter saves store attributes instead of a scalar HP total"
        );
        var resumedHealth = new Encounter();
        resumedHealth.Restore(
            System.Text.Json.JsonSerializer.Deserialize<EncounterSnapshot>(healthJson)!,
            healthRoster
        );
        resumedHealth.SetHp(801, 23, healthRoster);
        Check(
            resumedHealth.Health(healthEntry).Strength == 8
                && resumedHealth.Health(healthEntry).Dexterity == 9
                && resumedHealth.Health(healthEntry).Endurance == 6,
            "Damage reversal history survives serialization and reload"
        );
        healthEncounter.SetHp(801, -100, healthRoster);
        ExpectHealth(0, 0, 0, "All attributes have a zero lower bound");
        healthEncounter.SetHp(801, 100, healthRoster);
        ExpectHealth(8, 9, 6, "Healing stops at original attributes");
        var legacy = new EncounterSnapshot(
            false,
            0,
            null,
            0,
            [],
            new Dictionary<int, int> { [801] = 10 },
            []
        );
        healthEncounter.Restore(legacy, healthRoster);
        ExpectHealth(8, 2, 0, "Legacy scalar HP converts using END then higher STR/DEX");
        healthEncounter.SetHp(801, 23, healthRoster);
        ExpectHealth(8, 9, 6, "Converted legacy damage also supports reverse healing");
        var tiedEntry = healthEntry with { Upp = "776777" };
        var tiedHealth = new Encounter();
        tiedHealth.SetHp(801, 12, new[] { tiedEntry });
        Check(
            tiedHealth.Health(tiedEntry).Strength == 5
                && tiedHealth.Health(tiedEntry).Dexterity == 7
                && tiedHealth.Health(tiedEntry).Endurance == 0,
            "STR is reduced first when STR and DEX are tied"
        );
        Console.WriteLine(
            "Attribute damage order, reverse healing, derived HP, bounds, legacy saves and persisted recovery history passed."
        );

        var turnRoster = new[]
        {
            new SceneCombatant(901, 101, "First", false, false, true, 0, 0, "777777"),
            new SceneCombatant(902, 102, "Second", true, false, true, 1, 0, "777777"),
            new SceneCombatant(903, 103, "Third", true, false, true, 2, 0, "777777"),
        };
        var turns = new Encounter();
        turns.SetHp(901, 0, turnRoster);
        turns.Begin(turnRoster, true, true, 0, new Dictionary<int, int> { [901] = 20 }, () => 7);
        Check(turns.SelectedId == 902, "Begin skips dead highest-initiative combatants");
        turns.SetHp(902, -1, turnRoster);
        Check(
            turns.SelectedId == 903 && turns.Round == 1,
            "Death of current combatant selects next living combatant"
        );
        turns.Next(turnRoster);
        Check(turns.SelectedId == 903 && turns.Round == 2, "Round wrap skips all dead rows");
        turns.SetHp(903, 0, turnRoster);
        var lastRound = turns.Round;
        turns.Next(turnRoster);
        Check(
            turns.SelectedId is null && turns.Round == lastRound,
            "All dead clears selection without endlessly advancing rounds"
        );
        turns.SetHp(901, 1, turnRoster);
        Check(turns.SelectedId == 901, "Healing restores turn eligibility");
        var restoredTurns = new Encounter();
        restoredTurns.Restore(turns.Snapshot(), turnRoster);
        restoredTurns.Next(turnRoster);
        Check(
            restoredTurns.SelectedId == 901 && restoredTurns.IsDead(turnRoster[1]),
            "Death status and skipping survive restore"
        );
        var ambushedTurns = new Encounter();
        ambushedTurns.Begin(
            turnRoster,
            false,
            true,
            0,
            new Dictionary<int, int> { [901] = 7 },
            () => 7
        );
        ambushedTurns.SetHp(902, 0, turnRoster);
        ambushedTurns.SetHp(903, -2, turnRoster);
        ambushedTurns.Next(turnRoster);
        Check(
            ambushedTurns.Round == 2
                && ambushedTurns.SelectedId == 901
                && ambushedTurns.Initiative[901] == 7,
            "Skipped dead rows still finish round one and clear ambush"
        );
        Console.WriteLine(
            "Dead combatant selection, turn skipping, round wrap, healing and restore checks passed."
        );

        var publicRoster = new[]
        {
            turnRoster[0] with
            {
                Column = null,
                Row = null,
            },
            turnRoster[1] with
            {
                IsVisible = false,
            },
            turnRoster[2],
        };
        var publicRows = turns.PlayerTracker(publicRoster);
        var hiddenCurrentRoster = publicRoster
            .Select(e => e.Id == turns.SelectedId ? e with { IsVisible = false } : e)
            .ToArray();
        Check(
            turns.PlayerTracker(hiddenCurrentRoster).All(e => !e.IsCurrent),
            "Selecting a hidden combatant clears all cast tracker selections"
        );
        var outsideSelection = new Encounter { SelectedId = publicRoster[0].Id };
        Check(
            outsideSelection
                .PlayerTracker(publicRoster)
                .Single(e => e.Id == publicRoster[0].Id)
                .IsCurrent,
            "Visible selections are shared outside combat as well"
        );
        var travellerRow = publicRows.Single(e => !e.IsNpc);
        Check(
            travellerRow.OriginalStrength == 7
                && travellerRow.OriginalDexterity == 7
                && travellerRow.OriginalEndurance == 7,
            "Traveller player rows include original attributes alongside current values"
        );
        Check(
            publicRows.Single(e => e.IsNpc).OriginalStrength is null
                && publicRows.Single(e => e.IsNpc).OriginalDexterity is null
                && publicRows.Single(e => e.IsNpc).OriginalEndurance is null,
            "NPC original attributes remain private"
        );
        Check(
            publicRows.Count == 2 && !publicRows.Any(e => e.Id == 902),
            "Player tracker excludes hidden combatants but includes visible unplaced Travellers"
        );
        Check(
            publicRows
                .Select(e => e.Id)
                .SequenceEqual(turns.Order(publicRoster).Where(e => e.IsVisible).Select(e => e.Id)),
            "Player tracker preserves initiative ordering"
        );
        Check(
            publicRows.Single(e => !e.IsNpc).CurrentHp == 1
                && publicRows.Single(e => e.IsNpc).CurrentHp is null,
            "Only Traveller rows contain current HP totals"
        );
        using (
            var publicJson = System.Text.Json.JsonDocument.Parse(
                System.Text.Json.JsonSerializer.Serialize(
                    publicRows,
                    new System.Text.Json.JsonSerializerOptions(
                        System.Text.Json.JsonSerializerDefaults.Web
                    )
                )
            )
        )
        {
            Check(
                !publicJson
                    .RootElement.EnumerateArray()
                    .Single(e => e.GetProperty("isNpc").GetBoolean())
                    .TryGetProperty("currentHp", out _),
                "NPC HP totals are absent from the serialized player payload"
            );
        }
        Check(
            publicRows.Single(e => !e.IsNpc).IsCurrent && publicRows.Single(e => e.IsNpc).IsDead,
            "Player tracker includes current turn and death styling"
        );
        Console.WriteLine(
            "Read-only player tracker visibility, initiative order, unplaced characters and HP privacy checks passed."
        );

        // A new service/scope must resume the stored combat without rolling again.
        var combatRoster = (await store.CombatantsAsync(1, saved.Id))
            .Select(e => e with { Upp = "777777" })
            .ToArray();
        var combat = new Encounter();
        combat.Begin(
            combatRoster,
            false,
            true,
            2,
            combatRoster.Where(e => !e.IsNpc).ToDictionary(e => e.Id, _ => 9),
            () => 7
        );
        combat.SetHp(combatRoster[0].Id, 3, combatRoster);
        combat.SelectedId = combatRoster.Last().Id;
        var imageRevision = (await store.FindAsync(1, saved.Id))!.Revision;
        var before = combat.Snapshot();
        await store.SaveEncounterAsync(1, saved.Id, before);
        using (var reloadScope = provider.CreateScope())
        {
            var reloadedStore = reloadScope.ServiceProvider.GetRequiredService<SceneService>();
            var persisted = await reloadedStore.LoadEncounterAsync(1, saved.Id);
            var resumed = new Encounter();
            resumed.Restore(persisted!, combatRoster);
            Check(
                resumed.Active && resumed.Round == 1 && resumed.SelectedId == before.SelectedId,
                "Encounter activity, round and selection survive reload"
            );
            Check(
                resumed
                    .Order(combatRoster)
                    .Select(e => e.Id)
                    .SequenceEqual(combat.Order(combatRoster).Select(e => e.Id))
                    && resumed
                        .Initiative.OrderBy(p => p.Key)
                        .SequenceEqual(combat.Initiative.OrderBy(p => p.Key)),
                "Initiative values and ordering survive reload"
            );
            Check(resumed.Hp(combatRoster[0]) == 3, "Current HP survives reload");
            Check(
                resumed.Health(combatRoster[0]).Strength == combat.Health(combatRoster[0]).Strength
                    && resumed.Health(combatRoster[0]).Dexterity
                        == combat.Health(combatRoster[0]).Dexterity
                    && resumed.Health(combatRoster[0]).Endurance
                        == combat.Health(combatRoster[0]).Endurance,
                "Current attributes survive database reload"
            );
            // Clear selection to advance through a complete first round from its beginning.
            resumed.SelectedId = resumed.Order(combatRoster).First().Id;
            for (var i = 0; i < combatRoster.Length; i++)
                resumed.Next(combatRoster);
            Check(resumed.Round == 2, "Resumed combat advances the round");
            foreach (var e in combatRoster)
                Check(
                    resumed.Initiative[e.Id] == before.Initiative[e.Id] - before.Ambush[e.Id],
                    "First-round ambush removal survives reload"
                );
            await reloadedStore.SaveEncounterAsync(1, saved.Id, resumed.Snapshot());
            var roundTwo = new Encounter();
            roundTwo.Restore((await reloadedStore.LoadEncounterAsync(1, saved.Id))!, combatRoster);
            Check(
                roundTwo.Round == 2 && roundTwo.SelectedId == resumed.SelectedId,
                "Later rounds survive reload"
            );
            roundTwo.Exit();
            await reloadedStore.SaveEncounterAsync(1, saved.Id, roundTwo.Snapshot());
        }
        var exited = new Encounter();
        exited.Restore((await store.LoadEncounterAsync(1, saved.Id))!, combatRoster);
        Check(
            !exited.Active && exited.Hp(combatRoster[0]) == 3,
            "Exit stays exited after reload and retains HP"
        );
        Check(
            await store.LoadEncounterAsync(1, large.Id) is null,
            "Encounter state is isolated by scene and defaults empty"
        );
        await Reject(() => store.LoadEncounterAsync(2, saved.Id));
        await Reject(() => store.SaveEncounterAsync(2, saved.Id, before));
        Check(
            (await store.FindAsync(1, saved.Id))!.Revision == imageRevision,
            "State save does not change image revision"
        );
        Console.WriteLine(
            "Encounter persistence, resumed rounds/ambush, HP, exit and scene/campaign isolation checks passed."
        );

        await store.PruneInactiveTravellersAsync(1, large.Id);
        Check(
            !(await store.CombatantsAsync(1, large.Id)).Any(e => !e.IsNpc),
            "Opening another scene does not automatically duplicate assigned Travellers"
        );
        await Reject(() => store.MoveTravellersHereAsync(2, large.Id));
        var activeTarget = new Encounter();
        activeTarget.Begin(combatRoster, true, true, 0, new Dictionary<int, int>(), () => 7);
        await store.SaveEncounterAsync(1, large.Id, activeTarget.Snapshot());
        await Reject(() => store.MoveTravellersHereAsync(1, large.Id));
        Check(
            (await store.CombatantsAsync(1, saved.Id)).Any(e => e.CharacterId == 101),
            "Rejected moves preserve the original scene"
        );
        await store.SaveEncounterAsync(1, large.Id, new Encounter().Snapshot());
        var npcCountBefore = await db.SceneTokens.CountAsync(t => t.Character.IsNpc);
        await store.MoveTravellersHereAsync(1, large.Id);
        var movedRoster = await store.CombatantsAsync(1, large.Id);
        Check(
            movedRoster.Count(e => !e.IsNpc) == 2
                && movedRoster.Where(e => !e.IsNpc).All(e => e.Column is null && e.Row is null),
            "Add Travellers moves all active Travellers here unplaced"
        );
        Check(!movedRoster.Any(e => e.CharacterId == 104), "Inactive Travellers are not added");
        Check(
            !await db.SceneTokens.AnyAsync(t =>
                t.SceneId != large.Id
                && t.Character.CampaignId == 1
                && !t.Character.IsNpc
                && !t.Character.Inactive
            ),
            "Active Travellers are removed from every other scene"
        );
        Check(
            await db.SceneTokens.CountAsync(t => t.Character.IsNpc) == npcCountBefore,
            "Moving Travellers leaves all NPC instances intact"
        );
        var movedEncounter = new Encounter();
        movedEncounter.Restore((await store.LoadEncounterAsync(1, large.Id))!, movedRoster);
        Check(
            movedEncounter.Hp(movedRoster.Single(e => e.CharacterId == 101)) == 3,
            "Moving scenes preserves current Traveller attributes"
        );
        var movedIds = movedRoster.Select(e => e.Id).Order().ToArray();
        var placed = movedRoster.First(e => !e.IsNpc);
        await store.PositionCombatantAsync(1, large.Id, placed.Id, 3, 4);
        await store.SetCombatantVisibilityAsync(1, large.Id, placed.Id, false);
        var duplicate = new SceneTokenEntity
        {
            SceneId = saved.Id,
            CharacterId = placed.CharacterId,
            Column = 9,
            Row = 9,
        };
        db.SceneTokens.Add(duplicate);
        await db.SaveChangesAsync();
        db.Entry(duplicate).State = EntityState.Detached;
        await store.MoveTravellersHereAsync(1, large.Id);
        Check(
            (await store.CombatantsAsync(1, large.Id))
                .Select(e => e.Id)
                .Order()
                .SequenceEqual(movedIds)
                && (await store.CombatantsAsync(1, large.Id)).Single(e => e.Id == placed.Id)
                    is { Column: 3, Row: 4, IsVisible: false }
                && (await store.CombatantsAsync(1, large.Id))
                    .Where(e => e.Id != placed.Id)
                    .All(e => !e.OnMap),
            "Repeated Add Travellers preserves existing placement and visibility, avoids duplicates and keeps incoming Travellers unplaced"
        );
        await store.PruneInactiveTravellersAsync(1, saved.Id);
        Check(
            !(await store.CombatantsAsync(1, saved.Id)).Any(e => !e.IsNpc),
            "Returning to the previous scene does not pull moved Travellers back"
        );
        Console.WriteLine(
            "Add Travellers transfer, unplacement, current health, idempotence, NPC preservation and campaign/combat guards passed."
        );
        var chosenTraveller = movedRoster.Single(e => e.CharacterId == 301);
        var individuallyAdded = await store.AddCharacterAsync(
            1,
            saved.Id,
            chosenTraveller.CharacterId
        );
        Check(
            individuallyAdded.Id == chosenTraveller.Id && !individuallyAdded.OnMap,
            "Adding one Traveller through the picker moves that identity unplaced"
        );
        Check(
            !(await store.CombatantsAsync(1, large.Id)).Any(e => e.CharacterId == 301)
                && (await store.CombatantsAsync(1, large.Id)).Any(e => e.CharacterId == 101),
            "Picker transfer removes only the chosen Traveller from the previous scene"
        );
        await store.PositionCombatantAsync(1, saved.Id, individuallyAdded.Id, 5, 6);
        var addedAgain = await store.AddCharacterAsync(1, saved.Id, chosenTraveller.CharacterId);
        Check(
            addedAgain.Id == individuallyAdded.Id && addedAgain.Column == 5 && addedAgain.Row == 6,
            "Picking a Traveller already here preserves their placement without duplicates"
        );
        var sourceNpc = (await store.CombatantsAsync(1, legacyScene.Id)).Single(e =>
            e.CharacterId == 302
        );
        var pickerNpc = await store.AddCharacterAsync(1, saved.Id, 302);
        Check(
            pickerNpc.IsNpc
                && pickerNpc.Id != sourceNpc.Id
                && (await store.CombatantsAsync(1, legacyScene.Id)).Any(e => e.Id == sourceNpc.Id),
            "Picker NPC additions create new instances without removing any other scene's NPC"
        );
        await Reject(() => store.AddCharacterAsync(1, saved.Id, 104));
        await Reject(() => store.AddCharacterAsync(1, saved.Id, 103));
        Console.WriteLine(
            "Traveller picker transfers, existing-placement preservation, NPC independence and character filtering passed."
        );
        var largePlayer = (await store.CombatantsAsync(1, large.Id)).First();
        await store.PositionCombatantAsync(1, large.Id, largePlayer.Id, 0, 0);
        await store.DeleteAsync(1, large.Id);
        Check(
            !(await db.SceneTokens.AnyAsync(t => t.SceneId == large.Id)),
            "Scene deletion cascades tokens"
        );

        Check(
            await store.FindAsync(1, large.Id) is null
                && await store.ImageAsync(1, large.Id) is null,
            "Delete removes the scene and background"
        );
        db.Campaigns.Remove(await db.Campaigns.SingleAsync(c => c.Id == 1));
        await db.SaveChangesAsync();
        Check(!(await store.ListAsync(1)).Any(), "Campaign deletion cascades scenes");
        Check(!await db.SceneTokens.AnyAsync(), "Campaign deletion cascades tokens");
        Console.WriteLine(
            "Combat Tracker migration, instances, active Travellers, visibility, placement, live grid, image limits, cascades and campaign isolation checks passed."
        );
    }
}
