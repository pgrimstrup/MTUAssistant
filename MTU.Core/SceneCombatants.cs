using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

public sealed record SceneCombatant(
    int Id,
    int CharacterId,
    string Name,
    bool IsNpc,
    bool HasTokenImage,
    bool IsVisible,
    int? Column,
    int? Row,
    string Upp = "",
    int? MilitaryTactics = null,
    string PlayerName = "",
    string Occupation = "",
    int? Tactics = null,
    IReadOnlyList<CharacterSkill>? Skills = null,
    int Speed = 6
)
{
    public bool OnMap => Column.HasValue && Row.HasValue;
}

public sealed record SceneNpcChoice(
    int CharacterId,
    string Name,
    string Race,
    string Occupation,
    bool IsNpc = true
);

public sealed partial class SceneService
{
    public async Task<IReadOnlyList<SceneNpcChoice>> NpcChoicesAsync(int campaignId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        return await db
            .Characters.AsNoTracking()
            .Where(c => c.CampaignId == campaignId && (c.IsNpc || !c.Inactive))
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new SceneNpcChoice(
                c.Id,
                c.Name,
                c.Race,
                db.NPCs.Where(n => n.CharacterId == c.Id).Select(n => n.Occupation).FirstOrDefault()
                    ?? "",
                c.IsNpc
            ))
            .ToArrayAsync();
    }

    public async Task<IReadOnlyList<SceneCombatant>> CombatantsAsync(int campaignId, int sceneId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var result = await Combatants(db, campaignId, sceneId)
            .OrderBy(t => t.Character.IsNpc)
            .ThenBy(t => t.Id)
            .Select(t => new SceneCombatant(
                t.Id,
                t.CharacterId,
                t.Character.IsNpc ? t.Name : t.Character.Name,
                t.Character.IsNpc,
                t.Character.TokenImage != null,
                t.IsVisible,
                t.Column,
                t.Row,
                t.Character.Upp,
                db.CharacterSkills.Where(s =>
                        s.CharacterId == t.CharacterId && s.Name == "Tactics (Military)"
                    )
                    .Select(s => (int?)s.Level)
                    .Max(),
                t.Character.PlayerName,
                db.NPCs.Where(n => n.CharacterId == t.CharacterId)
                    .Select(n => n.Occupation)
                    .FirstOrDefault()
                    ?? "",
                db.CharacterSkills.Where(s => s.CharacterId == t.CharacterId && s.Name == "Tactics")
                    .Select(s => (int?)s.Level)
                    .Max(),
                null,
                t.Character.Speed
            ))
            .ToArrayAsync();
        return await WithSkillChecks(db, result);
    }

    // Opening a scene only removes invalid assignments; adding Travellers is explicit.
    public async Task PruneInactiveTravellersAsync(int campaignId, int sceneId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        await RequireScene(db, campaignId, sceneId);
        var existing = await db
            .SceneTokens.Where(t => t.SceneId == sceneId && !t.Character.IsNpc)
            .Include(t => t.Character)
            .ToListAsync();
        db.SceneTokens.RemoveRange(
            existing.Where(t => t.Character.Inactive || t.Character.CampaignId != campaignId)
        );
        await db.SaveChangesAsync();
    }

    public async Task<SceneCombatant> AddNpcInstanceAsync(
        int campaignId,
        int sceneId,
        int characterId
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        await RequireScene(db, campaignId, sceneId);
        var character =
            await db
                .Characters.AsNoTracking()
                .SingleOrDefaultAsync(c =>
                    c.Id == characterId && c.CampaignId == campaignId && c.IsNpc
                )
            ?? throw new ArgumentException("Choose an NPC from this campaign.");
        var number =
            (
                await db
                    .SceneTokens.Where(t => t.SceneId == sceneId && t.CharacterId == characterId)
                    .MaxAsync(t => t.InstanceNumber)
                ?? 0
            ) + 1;
        var token = new SceneTokenEntity
        {
            SceneId = sceneId,
            CharacterId = characterId,
            InstanceNumber = number,
            Name = $"{character.Name} {number}",
        };
        db.SceneTokens.Add(token);
        await db.SaveChangesAsync();
        var entry = new SceneCombatant(
            token.Id,
            characterId,
            token.Name,
            true,
            character.TokenImage != null,
            true,
            null,
            null,
            character.Upp,
            await db
                .CharacterSkills.Where(s =>
                    s.CharacterId == characterId && s.Name == "Tactics (Military)"
                )
                .Select(s => (int?)s.Level)
                .MaxAsync(),
            character.PlayerName,
            await db
                .NPCs.Where(n => n.CharacterId == characterId)
                .Select(n => n.Occupation)
                .FirstOrDefaultAsync()
                ?? "",
            await db
                .CharacterSkills.Where(s => s.CharacterId == characterId && s.Name == "Tactics")
                .Select(s => (int?)s.Level)
                .MaxAsync(),
            Speed: character.Speed
        );
        return (await WithSkillChecks(db, [entry]))[0];
    }

    public async Task<SceneCombatant> PositionCombatantAsync(
        int campaignId,
        int sceneId,
        int id,
        int? column,
        int? row,
        EncounterSnapshot? encounter = null
    )
    {
        if (column.HasValue != row.HasValue || column < 0 || row < 0)
            throw new ArgumentException("Choose a grid square on the map.");
        return await EditCombatant(
            campaignId,
            sceneId,
            id,
            t =>
            {
                t.Column = column;
                t.Row = row;
            },
            encounter
        );
    }

    public Task<SceneCombatant> SetCombatantVisibilityAsync(
        int campaignId,
        int sceneId,
        int id,
        bool visible
    ) => EditCombatant(campaignId, sceneId, id, t => t.IsVisible = visible);

    public Task<SceneCombatant> RenameNpcInstanceAsync(
        int campaignId,
        int sceneId,
        int id,
        string name
    ) =>
        EditCombatant(
            campaignId,
            sceneId,
            id,
            t =>
            {
                if (!t.Character.IsNpc)
                    throw new ArgumentException("Only NPC instance names can be edited here.");
                var trimmed = name.Trim();
                if (trimmed.Length is 0 or > 160)
                    throw new ArgumentException("Enter a name of up to 160 characters.");
                t.Name = trimmed;
            }
        );

    public async Task RemoveNpcInstanceAsync(int campaignId, int sceneId, int id)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var token =
            await Combatants(db, campaignId, sceneId)
                .SingleOrDefaultAsync(t => t.Id == id && t.Character.IsNpc)
            ?? throw new ArgumentException("Choose an NPC instance from this scene.");
        db.SceneTokens.Remove(token);
        await db.SaveChangesAsync();
    }

    private async Task<SceneCombatant> EditCombatant(
        int campaignId,
        int sceneId,
        int id,
        Action<SceneTokenEntity> edit,
        EncounterSnapshot? encounter = null
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var token =
            await Combatants(db, campaignId, sceneId)
                .Include(t => t.Character)
                .SingleOrDefaultAsync(t => t.Id == id)
            ?? throw new ArgumentException("This combatant is no longer in this scene.");
        edit(token);
        if (encounter is not null)
        {
            var scene = await db.Scenes.SingleAsync(s =>
                s.Id == sceneId && s.CampaignId == campaignId
            );
            scene.EncounterState = System.Text.Json.JsonSerializer.Serialize(encounter);
        }
        await db.SaveChangesAsync();
        var entry = new SceneCombatant(
            token.Id,
            token.CharacterId,
            token.Character.IsNpc ? token.Name : token.Character.Name,
            token.Character.IsNpc,
            token.Character.TokenImage != null,
            token.IsVisible,
            token.Column,
            token.Row,
            token.Character.Upp,
            await db
                .CharacterSkills.Where(s =>
                    s.CharacterId == token.CharacterId && s.Name == "Tactics (Military)"
                )
                .Select(s => (int?)s.Level)
                .MaxAsync(),
            token.Character.PlayerName,
            await db
                .NPCs.Where(n => n.CharacterId == token.CharacterId)
                .Select(n => n.Occupation)
                .FirstOrDefaultAsync()
                ?? "",
            await db
                .CharacterSkills.Where(s =>
                    s.CharacterId == token.CharacterId && s.Name == "Tactics"
                )
                .Select(s => (int?)s.Level)
                .MaxAsync(),
            Speed: token.Character.Speed
        );
        return (await WithSkillChecks(db, [entry]))[0];
    }

    private static async Task<SceneCombatant[]> WithSkillChecks(
        MtuDbContext db,
        IReadOnlyList<SceneCombatant> entries
    )
    {
        var ids = entries.Select(e => e.CharacterId).Distinct().ToArray();
        var skills = await db
            .CharacterSkills.AsNoTracking()
            .Where(s => ids.Contains(s.CharacterId))
            .Select(s => new CharacterSkill(s.Id, s.CharacterId, s.Name, s.Level))
            .ToArrayAsync();
        var byCharacter = skills.ToLookup(s => s.CharacterId);
        return entries
            .Select(e =>
                e with
                {
                    MilitaryTactics = SkillRules.Rank(
                        byCharacter[e.CharacterId],
                        "Tactics (Military)"
                    ),
                    Tactics = SkillRules.Rank(byCharacter[e.CharacterId], "Tactics"),
                    Skills = byCharacter[e.CharacterId].ToArray(),
                }
            )
            .ToArray();
    }

    private static IQueryable<SceneTokenEntity> Combatants(
        MtuDbContext db,
        int campaignId,
        int sceneId
    ) =>
        db.SceneTokens.Where(t =>
            t.SceneId == sceneId
            && t.Scene.CampaignId == campaignId
            && t.Character.CampaignId == campaignId
            && (t.Character.IsNpc || !t.Character.Inactive)
        );

    private static async Task RequireScene(MtuDbContext db, int campaignId, int sceneId)
    {
        if (!await db.Scenes.AnyAsync(s => s.Id == sceneId && s.CampaignId == campaignId))
            throw new ArgumentException("Choose a scene from this campaign.");
    }

    public async Task<SceneSummary> SaveGridAsync(
        int campaignId,
        int sceneId,
        string colour,
        double width,
        int opacity,
        int? gridSize = null
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var scene =
            await FindAsync(campaignId, sceneId)
            ?? throw new ArgumentException("Choose a scene from this campaign.");
        var size = gridSize ?? scene.GridSize;
        if (
            size < 1
            || !System.Text.RegularExpressions.Regex.IsMatch(colour, "^#[0-9a-fA-F]{6}$")
            || !double.IsFinite(width)
            || width <= 0
            || width > size
            || opacity is < 0 or > 100
        )
            throw new ArgumentException(
                "Grid size must be positive. Choose a colour, a positive line width no larger than a square, and opacity from 0 to 100%."
            );
        // Avoid loading/replacing the large image or changing its revision/camera key.
        var changed = await db
            .Scenes.Where(s => s.Id == sceneId && s.CampaignId == campaignId)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(s => s.GridSize, size)
                    .SetProperty(s => s.GridColour, colour)
                    .SetProperty(s => s.GridlineWidth, width)
                    .SetProperty(s => s.GridOpacity, opacity)
            );
        if (changed == 0)
            throw new ArgumentException("This scene is no longer available.");
        return scene with
        {
            GridSize = size,
            GridColour = colour,
            GridlineWidth = width,
            GridOpacity = opacity,
        };
    }
}
