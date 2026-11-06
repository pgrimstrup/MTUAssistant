using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

public sealed record DescriptionField(string Label, string Value);

public sealed record CombatDescription(
    int Id,
    int CampaignId,
    int CharacterId,
    string Name,
    bool HasTokenImage,
    bool IsAnimal,
    IReadOnlyList<DescriptionField> Overview,
    IReadOnlyList<DescriptionField> Attributes,
    string Skills,
    IReadOnlyList<DescriptionField> Rows,
    string Description
);

public sealed partial class SceneService
{
    public async Task<CombatDescription> DescribeCombatantAsync(int campaignId, int sceneId, int id)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var token =
            await Combatants(db, campaignId, sceneId)
                .AsNoTracking()
                .Include(t => t.Character)
                .SingleOrDefaultAsync(t => t.Id == id)
            ?? throw new ArgumentException("Choose a combatant in this scene.");
        var character = token.Character;
        var skills = await db
            .CharacterSkills.AsNoTracking()
            .Where(s => s.CharacterId == character.Id)
            .OrderBy(s => s.Name)
            .Select(s => s.Name + " " + s.Level)
            .ToArrayAsync();
        var skillText = skills.Length == 0 ? "None" : string.Join(", ", skills);
        var name = character.IsNpc || character.IsAnimal ? token.Name : character.Name;
        var overview = new List<DescriptionField>();
        var attributes = new List<DescriptionField>();
        var rows = new List<DescriptionField>();
        string description;
        if (character.IsAnimal)
        {
            var animal = await db
                .Animals.AsNoTracking()
                .SingleAsync(a => a.CharacterId == character.Id);
            overview.Add(new("Animal", name));
            overview.Add(new("Hits", animal.Hits.ToString()));
            overview.Add(new("Speed", character.Speed + "m"));
            var attacks = await db
                .AnimalAttacks.AsNoTracking()
                .Where(a => a.CharacterId == character.Id)
                .OrderBy(a => a.Id)
                .Select(a => a.Name)
                .ToArrayAsync();
            rows.Add(new("Skills", skillText));
            rows.Add(new("Attacks", attacks.Length == 0 ? "None" : string.Join(", ", attacks)));
            rows.Add(
                new("Behaviour", AnimalTypes.Normalize(animal.AnimalType) + ", " + animal.Behaviour)
            );
            description = animal.Description;
        }
        else
        {
            var npc = await db
                .NPCs.AsNoTracking()
                .SingleOrDefaultAsync(n => n.CharacterId == character.Id);
            var year = await db
                .Campaigns.Where(c => c.Id == campaignId)
                .Select(c => c.Year)
                .SingleAsync();
            overview.Add(
                new("Species", string.IsNullOrWhiteSpace(character.Race) ? "—" : character.Race)
            );
            overview.Add(new("Gender", string.IsNullOrWhiteSpace(npc?.Gender) ? "—" : npc.Gender));
            overview.Add(
                new(
                    "Age",
                    npc is not null
                        ? npc.Age?.ToString() ?? "—"
                        : CharacterAge.Calculate(character.TermsServed, year)?.ToString() ?? "—"
                )
            );
            var labels = new[] { "STR", "DEX", "END", "INT", "EDU", "SOC", "PSI" };
            foreach (var index in new[] { 0, 3, 1, 4, 2, 5, 6 })
                if (index < 6 || character.Upp.Length > index)
                    attributes.Add(
                        new(
                            labels[index],
                            index < character.Upp.Length
                            && "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ".IndexOf(
                                char.ToUpperInvariant(character.Upp[index])
                            )
                                is var value
                            && value >= 0
                                ? value.ToString()
                                : "—"
                        )
                    );
            var gear = await db
                .CharacterEquipment.AsNoTracking()
                .Where(e => e.CharacterId == character.Id && e.Equipped)
                .Include(e => e.Equipment)
                .ToArrayAsync();
            foreach (var kind in new[] { EquipmentKind.Weapon, EquipmentKind.Armour })
            {
                var names = gear.Where(e => e.Equipment.Kind == kind)
                    .Select(e =>
                    {
                        var detail =
                            kind == EquipmentKind.Weapon
                                ? e.Equipment.Traits
                                : e.Equipment.RequiredSkill;
                        return string.IsNullOrWhiteSpace(detail)
                            ? e.Equipment.Name
                            : $"{e.Equipment.Name} ({detail.Trim()})";
                    })
                    .Distinct()
                    .OrderBy(n => n)
                    .ToArray();
                rows.Add(
                    new(
                        kind == EquipmentKind.Weapon ? "Weapons" : "Armour",
                        names.Length == 0 ? "None" : string.Join(", ", names)
                    )
                );
            }
            description = character.Description;
        }
        return new(
            token.Id,
            campaignId,
            character.Id,
            name,
            character.TokenImage != null,
            character.IsAnimal,
            overview,
            attributes,
            skillText,
            rows,
            description
        );
    }
}
