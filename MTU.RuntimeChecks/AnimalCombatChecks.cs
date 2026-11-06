using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class AnimalCombatChecks
{
    private static void Check(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    public static async Task RunAsync()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<MtuDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<SceneService>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        await db.Database.MigrateAsync();
        db.Campaigns.AddRange(
            new CampaignEntity { Id = 1, Name = "Animals" },
            new CampaignEntity { Id = 2, Name = "Other" }
        );
        await db.SaveChangesAsync();
        var animals = new AnimalService(db);
        var tolerance =
            InjuryTolerance.MinusOnePerDie | InjuryTolerance.HalveInjury | InjuryTolerance.Kinetic;
        await animals.SaveAsync(
            new Animal(
                new Character(
                    0,
                    1,
                    "",
                    "Tentacled Hunter",
                    "Animal",
                    "",
                    "0",
                    0,
                    0,
                    0,
                    Skills:
                    [
                        new CharacterSkill(0, 0, "Melee (Natural)", 3),
                        new CharacterSkill(0, 0, "Tactics (Military)", 2),
                    ],
                    InjuryTolerance: tolerance
                ),
                30,
                "Carnivores",
                "Hunter",
                "Armour (4), Large 2, Small: 1, Long Reach 3",
                [new NaturalAttack("Bite", "2D"), new NaturalAttack("Tentacle", "3D+1")]
            )
        );
        var animal = (await animals.ListAsync(1)).Single();
        var scenes = scope.ServiceProvider.GetRequiredService<SceneService>();
        var scene = await scenes.SaveAsync(
            new SceneInput { CampaignId = 1, Name = "Wilds" },
            SceneImageChecks.Png(16, 16)
        );
        var choices = await scenes.NpcChoicesAsync(1);
        Check(
            AnimalTypes.Normalize(choices.Single().AnimalType) == "Carnivore"
                && choices.Single().Behaviour == "Hunter",
            "Animal picker carries type and behaviour"
        );
        Check(
            choices.Single().IsAnimal && choices.Single().IsNpc,
            "Animals use the same instance picker as NPCs"
        );
        var first = await scenes.AddCharacterAsync(1, scene.Id, animal.Character.Id);
        var second = await scenes.AddCharacterAsync(1, scene.Id, animal.Character.Id);
        Check(
            first.Id != second.Id && first.Name.EndsWith(" 1") && second.Name.EndsWith(" 2"),
            "Animal instances are independently numbered"
        );
        Check(
            first.IsAnimal
                && first.AnimalHits == 30
                && first.InjuryTolerance == tolerance
                && first.NaturalAttacks!.Count == 2,
            "Animal scene metadata includes Hits, attacks and tolerance"
        );
        Check(
            SkillRules.Dm(first.Skills!, "Melee (Natural)") == 3
                && SkillRules.Dm(first.Skills!, "Melee (Blade)") == 0,
            "Animals have basic Melee with the saved Natural rank"
        );
        await scenes.MoveTravellersHereAsync(1, scene.Id);
        await scenes.PruneInactiveTravellersAsync(1, scene.Id);
        Check(
            (await scenes.CombatantsAsync(1, scene.Id)).Count == 2,
            "Traveller transfer and prune preserve animal instances"
        );
        await scenes.RenameNpcInstanceAsync(1, scene.Id, second.Id, "Tentacle Alpha");
        first = await scenes.PositionCombatantAsync(1, scene.Id, first.Id, 3, 0);
        second = (await scenes.CombatantsAsync(1, scene.Id)).Single(e => e.Id == second.Id);
        Check(
            second.Name == "Tentacle Alpha" && second.AnimalName == first.AnimalName,
            "Instance rename preserves species grouping"
        );
        var hero = new SceneCombatant(
            1000,
            1000,
            "Traveller",
            false,
            false,
            true,
            0,
            0,
            "777777",
            Skills: [new CharacterSkill(0, 1000, "Melee (Blade)", 2)]
        );
        var otherSpecies = second with
        {
            Id = 1001,
            AnimalName = "Grazer",
            Skills = [new CharacterSkill(0, 0, "Melee", 0)],
        };
        var sameName = second with { Id = 1002, CharacterId = 1002 };
        var roster = new[] { hero, first, second, otherSpecies, sameName };
        var calls = 0;
        var encounter = new Encounter();
        encounter.Begin(
            roster,
            true,
            true,
            0,
            new Dictionary<int, int> { [hero.Id] = 10 },
            () =>
            {
                calls++;
                return 8;
            }
        );
        Check(
            calls == 5,
            "One Tactics roll per named species, plus independent animal initiatives"
        );
        Check(
            encounter.Initiative[first.Id] == 10
                && encounter.Initiative[second.Id] == 10
                && encounter.Initiative[sameName.Id] == 10,
            "Animal species share Tactics without attribute modifiers or NPC team Tactics"
        );
        Check(
            encounter.Initiative[otherSpecies.Id] == 8,
            "Animals without Tactics roll unmodified initiative"
        );
        Check(
            Encounter.MaximumHp(first) == 30 && encounter.Hp(first) == 30,
            "Animal HP comes from Hits"
        );
        encounter.SetHp(second.Id, 9, roster);
        Check(
            encounter.Hp(second) == 9 && encounter.Hp(first) == 30,
            "Each animal instance has independent Hits"
        );
        encounter.SetCoverStatus(second.Id, true, true, roster);
        encounter.ChooseAction(second.Id, "Change Stance", roster);
        encounter.SetStance(second.Id, "Crouching", roster);
        encounter.SetElevated(second.Id, true, roster);
        encounter.SetFlying(second.Id, true, roster);
        encounter.ResolveActions(second.Id, roster);
        Check(
            encounter.ActionFor(second.Id).Hiding
                && encounter.ActionFor(second.Id).BehindCover
                && encounter.ActionFor(second.Id).Elevated
                && encounter.ActionFor(second.Id).Flying
                && encounter.ActionFor(second.Id).Stance == "Crouching",
            "Animals resolve the shared states and stance"
        );
        var loadout = AnimalCombat.Loadout(first);
        var oneAttack = first with { NaturalAttacks = [new NaturalAttack("Claws", "1D")] };
        Check(
            AnimalCombat
                .Loadout(oneAttack)
                .WeaponFor(new CombatantActionState(MajorAction: "Melee Attack"))!
                .Equipment.Name == "Claws",
            "A single natural attack is selected automatically"
        );
        Check(loadout.ArmourProtection == 4, "Armour trait becomes Protection");
        Check(
            AnimalCombat.Trait("Large (3), Small=2; Long Reach: 4", "Long Reach") == 4
                && AnimalCombat.Trait("Armour 999999999999", "Armour") == 0,
            "Trait scores parse safely"
        );
        encounter.ChooseAction(first.Id, "Melee Attack", roster, 2);
        encounter.SetTarget(first.Id, hero.Id, roster);
        encounter.SetNaturalAttack(first.Id, 2, roster);
        var state = encounter.ActionFor(first.Id);
        Check(
            loadout.WeaponFor(state)!.Equipment.Name == "Tentacle"
                && loadout.WeaponFor(state)!.Equipment.RequiredSkill == "Melee (Natural)",
            "Selected natural attack is used as Melee (Natural)"
        );
        var modifiers = AttackModifierCalculator.Calculate(encounter, first, roster, loadout)!;
        Check(
            modifiers.CanResolve
                && modifiers.Total == 3
                && modifiers.Rows.All(r => r.Name is not ("STR" or "DEX")),
            "Long Reach permits natural attacks beyond adjacent squares with skill only"
        );
        var noSkills = first with { Skills = [] };
        var unskilledRoster = roster.Select(e => e.Id == first.Id ? noSkills : e).ToArray();
        Check(
            AttackModifierCalculator.Calculate(encounter, noSkills, unskilledRoster, loadout)!.Total
                == 0,
            "An animal without recorded skills has Melee 0"
        );
        Check(
            !AnimalCombat.InMeleeReach(first with { Column = 3, Row = 3 }, hero),
            "Long Reach respects measured distance beyond adjacent squares"
        );
        var beyond = first with { Column = 4 };
        Check(
            !AnimalCombat.InMeleeReach(beyond, hero) && !AnimalCombat.InMeleeReach(hero, first),
            "Reach is directional and limited"
        );
        var natural = encounter.BeginAttack(first.Id, roster, loadout, new CombatLoadout([]), 0);
        natural.SetAttackRoll(10);
        natural.SetDamageRoll(10);
        Check(natural.Weapon.Damage == "3D+1", "Attack resolution uses selected natural damage");
        encounter.SetFlying(first.Id, true, roster);
        modifiers = AttackModifierCalculator.Calculate(encounter, first, roster, loadout)!;
        Check(
            modifiers.Rows.Single(r => r.Name == "Elevated").Dm == 1,
            "Flying grants Elevated against grounded targets"
        );
        encounter.SetFlying(hero.Id, true, roster);
        Check(
            AttackModifierCalculator
                .Calculate(encounter, first, roster, loadout)!
                .Rows.All(r => r.Name != "Elevated"),
            "Flying gives no bonus against Flying targets"
        );
        encounter.SetElevated(first.Id, true, roster);
        Check(
            AttackModifierCalculator
                .Calculate(encounter, first, roster, loadout)!
                .Rows.Count(r => r.Name == "Elevated") == 1,
            "Elevated and Flying never stack"
        );
        encounter.ResolveActions(first.Id, roster, attackModifiers: modifiers);
        encounter.ResetActions(first.Id, roster);
        Check(
            encounter.ActionFor(first.Id).Flying
                && encounter.ActionFor(first.Id).NaturalAttackId == 2,
            "Reset preserves committed Flying and natural attack choice"
        );
        encounter.SetFlying(first.Id, false, roster);
        encounter.ResetActions(first.Id, roster);
        Check(
            encounter.ActionFor(first.Id).SelectedFlying,
            "Reset discards pending Flying changes"
        );
        encounter.ChooseAction(hero.Id, "Melee Attack", roster, 2);
        // Move the hero alongside the animal for a normal melee attack.
        hero = hero with
        {
            Column = 2,
        };
        roster[0] = hero;
        encounter.SetTarget(hero.Id, first.Id, roster);
        var sword = new CombatLoadout([
            new CharacterEquipmentDetails
            {
                Equipped = true,
                Equipment = new EquipmentItem
                {
                    Kind = EquipmentKind.Weapon,
                    Name = "Blade",
                    RequiredSkill = "Melee (Blade)",
                    Damage = "3D",
                },
            },
        ]);
        var incoming = AttackModifierCalculator.Calculate(encounter, hero, roster, sword)!;
        Check(
            incoming.Rows.Single(r => r.Name == "Target Large").Dm == 2
                && incoming.Rows.Single(r => r.Name == "Target Small").Dm == -1,
            "Large and Small apply to incoming attacks"
        );
        var hit = encounter.BeginAttack(hero.Id, roster, sword, loadout, 0, first.InjuryTolerance);
        hit.SetAttackRoll(8);
        hit.SetDamageRoll(12);
        var raw = hit.Outcome!.InjuryAfterProtection;
        Check(
            hit.OverallProtection == 4 && hit.Injury == Math.Max(0, raw - 3) / 2,
            "Animal armour and tolerance reduce incoming injury"
        );
        encounter.ApplyAttack(hit, roster);
        await scenes.SaveEncounterAsync(1, scene.Id, encounter.Snapshot());
        var restored = new Encounter();
        restored.Restore((await scenes.LoadEncounterAsync(1, scene.Id))!, roster);
        Check(
            restored.Hp(first) == encounter.Hp(first)
                && restored.Hp(second) == 9
                && restored.ActionFor(first.Id).Flying,
            "Animal health, states and attack selection survive encounter persistence"
        );
        Check(
            restored.ActionFor(first.Id).NaturalAttackId == 2,
            "Selected natural attack persists"
        );
        var flyer = first with
        {
            AnimalTraits = "Fast Metabolism 3, Slow Metabolism (1), Flyer 4",
            Speed = 6,
        };
        var flyingRoster = new[] { hero, flyer };
        var flying = new Encounter();
        flying.Begin(
            flyingRoster,
            true,
            true,
            0,
            new Dictionary<int, int> { [hero.Id] = 20 },
            () => 8
        );
        Check(
            flying.Initiative[flyer.Id] == 12,
            "Initiative adds Fast Metabolism and subtracts Slow Metabolism"
        );
        Check(flying.SpeedFor(flyer) == 6, "Flyer does not change grounded Speed");
        flying.SetFlying(flyer.Id, true, flyingRoster);
        Check(flying.SpeedFor(flyer) == 10, "Flying adds the Flyer score to base Speed");
        flying.ChooseAction(flyer.Id, "Move", flyingRoster);
        Check(
            flying.ActionFor(flyer.Id).MovementDiameter == 21.5,
            "Flying Speed expands manual Move circles"
        );
        flying.SetFlying(flyer.Id, false, flyingRoster);
        Check(
            flying.ActionFor(flyer.Id).MovementDiameter == 13.5,
            "Landing restores normal Move distance"
        );
        flying.SetFlying(flyer.Id, true, flyingRoster);
        flying.ResolveActions(flyer.Id, flyingRoster);
        flying.ResetActions(flyer.Id, flyingRoster);
        flying.SelectCombatant(flyer.Id, flyingRoster);
        Check(
            flying.AutomaticMoveFor(flyingRoster)!.Speed == 10,
            "Automatic movement uses Flying Speed"
        );
        var flyingRestored = new Encounter();
        flyingRestored.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(flying.Snapshot())
            )!,
            flyingRoster
        );
        Check(
            flyingRestored.SpeedFor(flyer) == 10
                && flyingRestored.AutomaticMoveFor(flyingRoster)!.Speed == 10,
            "Flying Speed survives save and restore"
        );
        var late = flyer with { Id = 2001, Name = "Late Hunter" };
        var joinedRoster = flyingRoster.Append(late).ToArray();
        var originalInitiative = flyingRestored.Initiative.ToDictionary();
        var lateCalls = 0;
        flyingRestored.AddCombatant(
            late,
            joinedRoster,
            () =>
            {
                lateCalls++;
                return 7;
            }
        );
        Check(
            lateCalls == 1 && flyingRestored.Initiative[late.Id] == 11,
            "Late animals roll initiative with metabolism and the saved species Tactics result"
        );
        Check(
            originalInitiative.All(pair => flyingRestored.Initiative[pair.Key] == pair.Value)
                && flyingRestored.SelectedId == flyer.Id
                && flyingRestored.Round == 1,
            "Reinforcements preserve existing initiative, current actor and round"
        );
        flyingRestored.AddCombatant(
            late,
            joinedRoster,
            () =>
            {
                lateCalls++;
                return 12;
            }
        );
        Check(
            lateCalls == 1,
            "Existing combatants cannot reroll their initiative by joining again"
        );
        var newSpecies = flyer with { Id = 2002, AnimalName = "New Species" };
        joinedRoster = joinedRoster.Append(newSpecies).ToArray();
        lateCalls = 0;
        flyingRestored.AddCombatant(
            newSpecies,
            joinedRoster,
            () =>
            {
                lateCalls++;
                return 9;
            }
        );
        Check(
            lateCalls == 2 && flyingRestored.Initiative[newSpecies.Id] == 14,
            "First arrival of a species rolls Tactics once and rolls its own initiative"
        );
        var npcReinforcement = new SceneCombatant(
            2003,
            2003,
            "Late NPC",
            true,
            false,
            true,
            null,
            null,
            "7A7777"
        );
        joinedRoster = joinedRoster.Append(npcReinforcement).ToArray();
        flyingRestored.AddCombatant(npcReinforcement, joinedRoster, () => 7);
        Check(
            flyingRestored.Initiative[npcReinforcement.Id] == 8,
            "Late NPC initiative includes its normal characteristic DM"
        );
        await scenes.SaveEncounterAsync(1, scene.Id, flyingRestored.Snapshot());
        var resumed = new Encounter();
        resumed.Restore((await scenes.LoadEncounterAsync(1, scene.Id))!, joinedRoster);
        Check(
            resumed.Initiative[late.Id] == 11
                && resumed.Snapshot().AnimalTactics!["New Species"] == 3,
            "Reinforcement initiatives and species Tactics persist"
        );
        var third = await scenes.AddCharacterAsync(1, scene.Id, animal.Character.Id);
        Check(
            third.IsAnimal && third.Name.EndsWith(" 3"),
            "Animals can be instanced during active encounters"
        );
        await scenes.RemoveNpcInstanceAsync(1, scene.Id, third.Id);
        await scenes.RemoveNpcInstanceAsync(1, scene.Id, second.Id);
        Check(
            (await scenes.CombatantsAsync(1, scene.Id)).Single().Id == first.Id,
            "Removing one animal preserves other instances"
        );
        var rejected = false;
        try
        {
            await scenes.AddCharacterAsync(2, scene.Id, animal.Character.Id);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Check(rejected, "Animals cannot cross campaign boundaries");
        Console.WriteLine(
            "Animal instances, species Tactics, natural attacks, Hits, traits, tolerance and Flying checks passed."
        );
    }
}
