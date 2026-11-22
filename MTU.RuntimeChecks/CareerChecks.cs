using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MTU.Core;
using MTU.Core.Data;

public static class CareerChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
        static async Task Rejects(Func<Task> action, string message)
        {
            try
            {
                await action();
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new Exception(message);
        }
        var nextId = 0;
        CareerTerm Term(
            int number,
            string service,
            bool advancement = false,
            bool commission = false
        ) => new(++nextId, 1, number, service, true, advancement, commission);

        // Rank rules.
        Check(
            CareerRules.Rank([], "Navy")
                is {
                    Terms: 0,
                    Enlisted: 0,
                    Officer: null,
                    Highest: 0,
                    Label: "E-0",
                    Military: true
                },
            "A recruit with no terms is E-0"
        );
        var enlisted = new[] { Term(1, "Navy", true), Term(2, "Navy"), Term(3, "Navy", true) };
        Check(
            CareerRules.Rank(enlisted, "navy")
                is { Terms: 3, Enlisted: 2, Officer: null, Label: "E-2" },
            "Each successful Advancement Check adds one enlisted rank"
        );
        var commissioned = enlisted.Append(Term(4, "Navy", commission: true)).ToArray();
        Check(
            CareerRules.Rank(commissioned, "Navy")
                is { Enlisted: 2, Officer: 1, Highest: 2, Label: "E-2" },
            "A commission starts officer rank at O-1, and the highest rank is the larger number"
        );
        var promoted = commissioned
            .Append(Term(5, "Navy", true))
            .Append(Term(6, "Navy", true))
            .ToArray();
        Check(
            CareerRules.Rank(promoted, "Navy")
                is { Enlisted: 2, Officer: 3, Highest: 3, Label: "O-3" },
            "Advancement after a commission raises officer rank, not enlisted rank"
        );
        Check(
            CareerRules.Rank([Term(1, "Marine", true, true)], "Marine")
                is { Enlisted: 0, Officer: 1, Label: "O-1" },
            "A commission is the term's one promotion, so an advancement in the same term is ignored"
        );
        Check(
            CareerRules.Rank(
                [Term(1, "Army", commission: true), Term(2, "Army", commission: true)],
                "Army"
            )
                is { Officer: 1 },
            "A second commission does not add rank"
        );
        Check(
            CareerRules.Rank([Term(1, "Scout", true, true), Term(2, "Scout", true)], "Scout")
                is { Enlisted: 2, Officer: null, Highest: 2, Label: "Rank 2", Military: false },
            "Non-military services ignore commissions and count advancements"
        );
        var mixed = new[]
        {
            Term(1, "Navy", true),
            Term(2, "Scout", true),
            Term(3, "Navy", true),
            Term(4, "Belter"),
            Term(5, "Military Academy", commission: true),
        };
        Check(
            CareerRules.HighestRank(mixed, "Navy") == 2
                && CareerRules.HighestRank(mixed, "Scout") == 1
                && CareerRules.HighestRank(mixed, "Army") == 0
                && CareerRules.HighestRank(null, "Navy") == 0
                && CareerRules.Rank(mixed, "Military Academy") is { Officer: 1, Military: true },
            "Rank is counted within each service, including non-consecutive terms"
        );
        // Military rank transfers between the military services.
        var graduate = new[]
        {
            Term(1, "Military Academy", commission: true),
            Term(2, "Navy", true),
        };
        Check(
            CareerRules.Rank(graduate.Take(1), "Navy") is { Terms: 0, Highest: 0 }
                && CareerRules.Rank(graduate, "Navy") is { Terms: 1, Officer: 2, Label: "O-2" }
                && CareerRules.HighestRank(graduate, "Navy") == 2,
            "A commissioned Academy graduate starts the Navy at O-1 and advances to O-2 in the first term"
        );
        Check(
            CareerRules.RankLabels(graduate).SequenceEqual(["Military Academy O-1", "Navy O-2"])
                && CareerRules.RankLabels(graduate.Take(1)).SequenceEqual(["Military Academy O-1"])
                && CareerRules.RankLabels([Term(1, "Military Academy")]).Count == 0
                && CareerRules.RankLabels(null).Count == 0,
            "Military Academy rank is displayed, so a graduate who never serves keeps O-1"
        );
        Check(
            !CareerServices.HasAdvancement(" university ")
                && !CareerServices.HasAdvancement("Military Academy")
                && CareerServices.HasAdvancement("Navy")
                && CareerServices.HasAdvancement("Belter")
                && CareerRules.Rank([Term(1, "University", true, true)], "University")
                    is { Enlisted: 0, Officer: null, Highest: 0 }
                && CareerRules.Rank([Term(1, "Military Academy", true, true)], "Military Academy")
                    is { Enlisted: 0, Officer: 1 },
            "University and Military Academy have no Advancement Check, and University no commission"
        );
        var career = new[]
        {
            Term(1, "Army"),
            Term(2, "Army", commission: true),
            Term(3, "Navy"),
            Term(4, "Scout"),
        };
        Check(
            !CareerRules.CommissionAvailable(career, career[0].Id, "Army")
                && CareerRules.CommissionAvailable(career, career[1].Id, "Army")
                && !CareerRules.CommissionAvailable(career, career[2].Id, "Navy")
                && !CareerRules.CommissionAvailable(career, 0, "Marine")
                && !CareerRules.CommissionAvailable(career, career[3].Id, "Scout")
                && CareerRules.CommissionAvailable(career.Take(1), 0, "Military Academy")
                && !CareerRules.CommissionAvailable([], 0, "University"),
            "A commission is only available on a military term while no other term holds it"
        );
        var transfer = new[]
        {
            Term(1, "Army", true, true),
            Term(2, "Navy"),
            Term(3, "Marine", true),
        };
        Check(
            CareerRules.Rank(transfer, "Army") is { Terms: 1, Officer: 1, Label: "O-1" }
                && CareerRules.Rank(transfer, "Navy") is { Terms: 1, Officer: 1, Label: "O-1" }
                && CareerRules.Rank(transfer, "Marine") is { Terms: 1, Officer: 2, Label: "O-2" }
                && CareerRules
                    .RankLabels(transfer)
                    .SequenceEqual(["Army O-1", "Navy O-1", "Marine O-2"]),
            "An officer moving from the Army to the Navy starts there at the same rank"
        );
        var enlistedTransfer = new[]
        {
            Term(1, "Navy", true),
            Term(2, "Navy", true),
            Term(3, "Army", true),
            Term(4, "Army", true),
        };
        Check(
            CareerRules.Rank(enlistedTransfer, "Navy") is { Enlisted: 2, Label: "E-2" }
                && CareerRules.Rank(enlistedTransfer, "Army") is { Enlisted: 4, Label: "E-4" },
            "Enlisted rank transfers too, and a service keeps the rank held when the character left it"
        );
        var civilian = new[]
        {
            Term(1, "Navy", true, true),
            Term(2, "Scout", true),
            Term(3, "Merchant"),
            Term(4, "Merchant", true),
            Term(5, "Navy"),
        };
        Check(
            CareerRules.Rank(civilian, "Scout") is { Enlisted: 1, Officer: null, Label: "Rank 1" }
                && CareerRules.Rank(civilian, "Merchant") is { Enlisted: 1, Label: "Rank 1" }
                && CareerRules.Rank(civilian.Take(3), "Merchant") is { Enlisted: 0, Highest: 0 }
                && CareerRules.Rank(civilian, "Navy") is { Terms: 2, Officer: 1 },
            "Rank does not transfer to or between non-military services, which start at 0"
        );
        var transferProgress = CareerRules.RankAfterEachTerm(transfer);
        Check(
            transfer.Select(t => transferProgress[t.Id].Label).SequenceEqual(["O-1", "O-1", "O-2"]),
            "Rank after each term carries across military services"
        );
        Check(
            CareerRules.Summary(mixed) == "Navy 2, Scout 1, Belter 1, Military Academy 1"
                && CareerRules.Summary(null) == "",
            "Summary lists terms per service in order of first term"
        );
        Check(
            CareerRules
                .ServiceLines(civilian)
                .SequenceEqual([
                    "2 Terms in Navy (O-1)",
                    "1 Term in Scout (Rank 1)",
                    "2 Terms in Merchant (Rank 1)",
                ])
                && CareerRules
                    .ServiceLines([Term(1, "Drifter"), Term(2, "Army", true)])
                    .SequenceEqual(["1 Term in Drifter", "1 Term in Army (E-1)"])
                && CareerRules.ServiceLines(null).Count == 0,
            "Service lines give terms and rank per service, leaving out a rank of 0"
        );
        var progress = CareerRules.RankAfterEachTerm(promoted);
        Check(
            promoted
                .Select(t => progress[t.Id].Label)
                .SequenceEqual(["E-1", "E-1", "E-2", "E-2", "O-2", "O-3"]),
            "Rank after each term follows the career in order"
        );
        var promotions = CareerRules.PromotionInEachTerm(promoted);
        Check(
            promoted
                .Select(t => promotions.GetValueOrDefault(t.Id))
                .SequenceEqual([
                    "Promoted to E-1",
                    null,
                    "Promoted to E-2",
                    "Commissioned to O-1",
                    "Promoted to O-2",
                    "Promoted to O-3",
                ]),
            "Each term shows its commission or promotion, naming the rank gained, or nothing"
        );
        var civilianPromotions = CareerRules.PromotionInEachTerm(civilian);
        Check(
            civilian
                .Select(t => civilianPromotions.GetValueOrDefault(t.Id))
                .SequenceEqual([
                    "Commissioned to O-1",
                    "Promoted to Rank 1",
                    null,
                    "Promoted to Rank 1",
                    null,
                ]),
            "A commission hides the same term's advancement, and other services promote to Rank n"
        );
        Check(
            CareerRules
                .PromotionInEachTerm([
                    Term(1, "University", true, true),
                    Term(2, "Military Academy", true),
                    Term(3, "Army", commission: true),
                    Term(4, "Navy", true, true),
                ])
                .Values.SequenceEqual(["Commissioned to O-1", "Promoted to O-2"]),
            "Study terms never promote, and only the first commission counts"
        );
        Check(
            CareerServices.All.Count == 15
                && CareerServices.All.Contains("Military Academy")
                && !CareerServices.All.Contains(CareerServices.Other)
                && CareerServices.Military.SequenceEqual([
                    "Army",
                    "Marine",
                    "Navy",
                    "Military Academy",
                ])
                && CareerServices.IsMilitary(" navy ")
                && !CareerServices.IsMilitary("Scout")
                && CareerServices.Normalize(" MARINE ") == "Marine"
                && CareerServices.Normalize(" Belter ") == "Belter",
            "Service list, military services and name normalisation"
        );

        // Age.
        Check(
            CareerRules.AgeAtStart(1) == 16
                && CareerRules.AgeAtStart(2) == 20
                && CareerRules.AgeAtStart(5) == 32,
            "A career starts at 16 and each term lasts four years"
        );
        Check(
            CharacterAge.Calculate(0, 1, 1105) == 16
                && CharacterAge.Calculate(4, 1, 1105, 1) == 32
                && CharacterAge.Calculate(4, 1, 1105, 365) == 32
                && CharacterAge.Calculate(4, 1, 1106, 1) == 33,
            "A day-1 birthday ages the character at each new year"
        );
        Check(
            CharacterAge.Calculate(4, 100, 1105, 99) == 32
                && CharacterAge.Calculate(4, 100, 1105, 100) == 33
                && CharacterAge.Calculate(4, 100, 1106, 99) == 33
                && CharacterAge.Calculate(4, 100, 1106, 100) == 34
                && CharacterAge.Calculate(4, 365, 1105, 364) == 32
                && CharacterAge.Calculate(4, 365, 1105, 365) == 33,
            "A later birthday ages the character on that day of the year"
        );

        // Storage.
        using (var connection = new SqliteConnection("Data Source=:memory:"))
        {
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(connection).Options;
            await using var db = new MtuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Campaigns.AddRange(
                new CampaignEntity { Id = 1, Name = "Careers" },
                new CampaignEntity { Id = 2, Name = "Elsewhere" }
            );
            await db.SaveChangesAsync();
            var characters = new CharacterService(db);
            var npcs = new NpcService(db, characters);
            var careers = new CareerService(db);
            var id = await characters.SaveAsync(
                new Character(0, 1, "", "Veteran", "", "777777", 0, Birthday: 120)
            );
            var otherId = await characters.SaveAsync(
                new Character(0, 2, "", "Outsider", "", "", 0)
            );
            var npcId = await npcs.SaveAsync(
                new Npc(new Character(0, 1, "", "Contact", "Human", "", 0, Birthday: 200))
            );
            Check(
                (await characters.ListAsync(1)).Single() is { Birthday: 120, Career.Count: 0 }
                    && (await npcs.ListAsync(1)).Single().Character.Birthday == 200,
                "Birthday is saved for Travellers and NPCs"
            );
            foreach (var birthday in new[] { 0, 366, -1 })
            {
                await Rejects(
                    () =>
                        characters.SaveAsync(
                            new Character(0, 1, "", "Bad", "", "", 0, Birthday: birthday)
                        ),
                    "Invalid Traveller birthday accepted"
                );
                await Rejects(
                    () =>
                        npcs.SaveAsync(
                            new Npc(new Character(0, 1, "", "Bad", "", "", 0, Birthday: birthday))
                        ),
                    "Invalid NPC birthday accepted"
                );
            }
            var first = await careers.SaveAsync(1, new CareerTerm(0, id, 0, " navy ", true, true));
            var second = await careers.SaveAsync(
                1,
                new CareerTerm(0, id, 0, "Scout", false, true, true, "  Lost in jumpspace.  ")
            );
            var third = await careers.SaveAsync(1, new CareerTerm(0, id, 0, "Belter"));
            Check(
                first is { Number: 1, Service: "Navy", Survival: true, Advancement: true }
                    && second
                        is {
                            Number: 2,
                            Service: "Scout",
                            Survival: false,
                            Commission: false,
                            Notes: "Lost in jumpspace."
                        }
                    && third is { Number: 3, Service: "Belter" },
                "Terms are added in order, with listed spelling, trimmed notes and no commission outside the military"
            );
            var edited = await careers.SaveAsync(
                1,
                second with
                {
                    Service = "Navy",
                    Commission = true,
                    Notes = "",
                }
            );
            var listed = await careers.ListAsync(1, id);
            Check(
                edited is { Id: var editedId, Number: 2, Commission: true, Advancement: false }
                    && editedId == second.Id
                    && listed.Select(t => t.Service).SequenceEqual(["Navy", "Navy", "Belter"])
                    && CareerRules.Rank(listed, "Navy")
                        is { Enlisted: 1, Officer: 1, Label: "O-1" },
                "Editing a term keeps its place and updates the rank"
            );
            // Checks a term does not have are stored as not passed.
            var study = await careers.SaveAsync(
                2,
                new CareerTerm(0, otherId, 0, "University", true, true, true)
            );
            var academy = await careers.SaveAsync(
                2,
                new CareerTerm(0, otherId, 0, "Military Academy", true, true, true)
            );
            var service = await careers.SaveAsync(
                2,
                new CareerTerm(0, otherId, 0, "Navy", true, true, true)
            );
            Check(
                study is { Advancement: false, Commission: false }
                    && academy is { Advancement: false, Commission: true }
                    && service is { Advancement: true, Commission: false }
                    && CareerRules
                        .RankLabels(await careers.ListAsync(2, otherId))
                        .SequenceEqual(["Military Academy O-1", "Navy O-2"]),
                "University and Academy drop Advancement, and a second commission is not stored"
            );
            Check(
                await careers.SaveAsync(2, academy with { Notes = "Graduated" })
                    is { Commission: true, Notes: "Graduated" },
                "Re-saving the commissioned term keeps its commission"
            );
            foreach (var term in new[] { study, academy, service })
                await careers.DeleteAsync(2, otherId, term.Id);
            var traveller = (await characters.ListAsync(1)).Single();
            Check(
                traveller.Career!.Select(t => t.Id).SequenceEqual(listed.Select(t => t.Id))
                    && CharacterAge.Calculate(
                        traveller.Career!.Count,
                        traveller.Birthday,
                        1105,
                        119
                    ) == 28
                    && CharacterAge.Calculate(
                        traveller.Career!.Count,
                        traveller.Birthday,
                        1105,
                        120
                    ) == 29,
                "The Traveller list carries the career used for age and rank"
            );
            Check(
                await characters.MailModifiersAsync(1) is (1, _),
                "Mail uses the Naval rank worked out from the career"
            );
            await careers.SaveAsync(1, new CareerTerm(0, npcId, 0, "Rogue", Advancement: true));
            Check(
                (await npcs.ListAsync(1)).Single().Character.Career is [{ Service: "Rogue" }]
                    && (await careers.ListAsync(1, id)).Count == 3
                    && await characters.MailModifiersAsync(1) is (1, _),
                "NPCs have their own career, which does not count towards mail"
            );
            await Rejects(
                () => careers.SaveAsync(1, new CareerTerm(0, id, 0, "  ")),
                "Blank service accepted"
            );
            await Rejects(
                () => careers.SaveAsync(1, new CareerTerm(0, id, 0, "other")),
                "Unnamed other service accepted"
            );
            await Rejects(
                () => careers.SaveAsync(1, new CareerTerm(0, id, 0, new string('x', 121))),
                "Overlong service accepted"
            );
            await Rejects(
                () =>
                    careers.SaveAsync(
                        1,
                        new CareerTerm(0, id, 0, "Navy", Notes: new string('x', 4001))
                    ),
                "Overlong notes accepted"
            );
            await Rejects(
                () => careers.SaveAsync(2, new CareerTerm(0, id, 0, "Navy")),
                "Term added through another campaign"
            );
            await Rejects(
                () => careers.SaveAsync(2, first with { Service = "Army" }),
                "Term edited through another campaign"
            );
            await Rejects(
                () => careers.SaveAsync(1, first with { CharacterId = otherId }),
                "Term moved to a character in another campaign"
            );
            await careers.DeleteAsync(2, id, first.Id);
            Check(
                (await careers.ListAsync(2, id)).Count == 0
                    && (await careers.ListAsync(1, id)).Count == 3,
                "Careers are isolated by campaign"
            );
            var animalId = await new AnimalService(db).SaveAsync(
                new Animal(
                    new Character(0, 1, "", "Beast", "Animal", "", 0),
                    10,
                    AnimalTypes.All[0],
                    AnimalTypes.Behaviours[0],
                    "",
                    []
                )
            );
            await Rejects(
                () => careers.SaveAsync(1, new CareerTerm(0, animalId, 0, "Drifter")),
                "Animal given a career"
            );
            await careers.DeleteAsync(1, id, first.Id);
            var remaining = await careers.ListAsync(1, id);
            Check(
                remaining
                    .Select(t => (t.Number, t.Service))
                    .SequenceEqual([(1, "Navy"), (2, "Belter")])
                    && CareerRules.Rank(remaining, "Navy") is { Enlisted: 0, Officer: 1 },
                "Deleting a term renumbers the later ones"
            );
            await characters.DeleteAsync(1, id);
            Check(
                await db.CareerTerms.CountAsync() == 1,
                "Deleting a Traveller deletes its career terms"
            );
        }

        // Migration: Terms Served text and stored ranks become career terms, in both directions.
        using (var connection = new SqliteConnection("Data Source=:memory:"))
        {
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(connection).Options;
            await using var db = new MtuDbContext(options);
            var migrator = db.GetService<IMigrator>();
            const string previous = "20261107050000_SceneFogEnabled";
            await migrator.MigrateAsync(previous);
            // Insert with SQL: the Characters table still has the old columns.
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Campaigns (Id, Name, Day, Year, IsActive) VALUES (1, 'Legacy', 1, 1105, 1);
                INSERT INTO Characters (Id, CampaignId, Name, PlayerName, Race, Upp, TermsServed, HighestNavalRank, HighestScoutRank, NaturalDr, Inactive, IsNpc, IsAnimal, InjuryTolerance, Speed, Description) VALUES
                    (1, 1, 'Officer', '', '', '777777', 'Navy 3, Diplomat 1', 2, 0, 0, 0, 0, 0, 0, 6, ''),
                    (2, 1, 'Plain', '', '', '777777', '3', 0, 0, 0, 0, 0, 0, 0, 6, ''),
                    (3, 1, 'Ranked', '', '', '777777', ' scouts 2 ,Navy', 5, 1, 0, 0, 0, 0, 0, 6, ''),
                    (4, 1, 'Fresh', '', '', '777777', '0', 3, 0, 0, 0, 0, 0, 0, 6, ''),
                    (5, 1, 'Blank', '', '', '777777', '', 0, 0, 0, 0, 0, 0, 0, 6, ''),
                    (6, 1, 'Ancient', '', '', '777777', 'Navy 300', 0, 0, 0, 0, 0, 0, 0, 6, ''),
                    (7, 1, 'Contact', '', '', '777777', 'Rogue 2', 0, 0, 0, 0, 1, 0, 0, 6, ''),
                    (8, 1, 'Beast', '', 'Animal', '', '4', 0, 0, 0, 0, 0, 1, 0, 6, '');
                """
            );
            for (var pass = 0; pass < 2; pass++)
            {
                await migrator.MigrateAsync();
                var careers = new CareerService(db);
                var officer = await careers.ListAsync(1, 1);
                Check(
                    officer
                        .Select(t => (t.Service, t.Advancement))
                        .SequenceEqual([
                            ("Navy", true),
                            ("Navy", true),
                            ("Navy", false),
                            ("Diplomat", false),
                        ])
                        && officer.All(t => t is { Survival: true, Commission: false, Notes: "" })
                        && CareerRules.HighestRank(officer, "Navy") == 2,
                    "Migration turns the text into terms and reproduces the stored Naval rank"
                );
                Check(
                    CareerRules.Summary(await careers.ListAsync(1, 2)) == "Unknown 3",
                    "A plain number becomes that many terms in an unknown service"
                );
                var ranked = await careers.ListAsync(1, 3);
                Check(
                    ranked
                        .Select(t => (t.Service, t.Advancement))
                        .SequenceEqual([("Scout", true), ("Scout", false), ("Navy", true)])
                        && CareerRules.HighestRank(ranked, "Scout") == 1
                        && CareerRules.HighestRank(ranked, "Navy") == 1,
                    "Plural names, loose spacing and a name with no number are converted"
                );
                if (pass == 0)
                    Check(
                        ranked[0].Notes
                            == "Highest Naval Rank was 5 before career terms were added."
                            && ranked.Skip(1).All(t => t.Notes == ""),
                        "A rank that cannot be reproduced is recorded in the first term's notes"
                    );
                Check(
                    (await careers.ListAsync(1, 4)).Count == 0
                        && (await careers.ListAsync(1, 5)).Count == 0
                        && (await careers.ListAsync(1, 6)).Count == 20
                        && CareerRules.Summary(await careers.ListAsync(1, 7)) == "Rogue 2"
                        && await db.CareerTerms.CountAsync(t => t.CharacterId == 8) == 0,
                    "Zero and blank give no terms, long entries are capped, NPCs convert and animals do not"
                );
                Check(
                    (await new CharacterService(db).ListAsync(1)).All(c => c.Birthday == 1)
                        && await db
                            .Database.SqlQueryRaw<int>(
                                "SELECT COUNT(*) AS Value FROM pragma_table_info('Characters') WHERE name IN ('TermsServed','HighestNavalRank','HighestScoutRank')"
                            )
                            .SingleAsync() == 0,
                    "Existing characters get birthday 1 and the old columns are removed"
                );
                if (pass == 0)
                {
                    await migrator.MigrateAsync(previous);
                    Check(
                        await db
                            .Database.SqlQueryRaw<string>(
                                "SELECT TermsServed || '|' || HighestNavalRank || '|' || HighestScoutRank AS Value FROM Characters WHERE Id = 1"
                            )
                            .SingleAsync() == "Navy 3, Diplomat 1|2|0"
                            && await db
                                .Database.SqlQueryRaw<string>(
                                    "SELECT TermsServed || '|' || HighestScoutRank AS Value FROM Characters WHERE Id = 3"
                                )
                                .SingleAsync() == "Scout 2, Navy 1|1"
                            && await db
                                .Database.SqlQueryRaw<int>(
                                    "SELECT COUNT(*) AS Value FROM sqlite_master WHERE name = 'CareerTerms'"
                                )
                                .SingleAsync() == 0,
                        "Rollback rebuilds the Terms Served text and ranks from the terms"
                    );
                }
            }
        }
        Console.WriteLine(
            "Career ranks, commissions, age and birthdays, term storage, campaign isolation and the terms migration checks passed."
        );
    }
}
