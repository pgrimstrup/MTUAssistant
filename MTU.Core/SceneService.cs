using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

public sealed class SceneInput
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public string Name { get; set; } = "";
    public int GridSize { get; set; } = 100;
    public string GridColour { get; set; } = "#000000";
    public int GridOpacity { get; set; } = 50;
    public double GridlineWidth { get; set; } = 2;
}

public sealed record SceneSummary(
    int Id,
    int CampaignId,
    string Name,
    int GridSize,
    string GridColour,
    int GridOpacity,
    double GridlineWidth,
    long Revision,
    int ImageBytes
);

public sealed record SceneImage(byte[] Bytes, string ContentType);

public sealed record SceneCharacter(int CharacterId, string Name, bool IsNpc, bool HasTokenImage);

public sealed partial class SceneService(IServiceScopeFactory scopes)
{
    public const int MaxImageDimension = SceneImageInfo.MaxDimension;

    public async Task<IReadOnlyList<SceneCharacter>> CharactersAsync(int campaignId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        return await db
            .Characters.AsNoTracking()
            .Where(c => c.CampaignId == campaignId)
            .OrderBy(c => c.IsNpc)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new SceneCharacter(c.Id, c.Name, c.IsNpc, c.TokenImage != null))
            .ToArrayAsync();
    }

    public async Task<SceneImage?> TokenImageAsync(int campaignId, int characterId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var bytes = await db
            .Characters.AsNoTracking()
            .Where(c => c.Id == characterId && c.CampaignId == campaignId)
            .Select(c => c.TokenImage)
            .SingleOrDefaultAsync();
        return bytes is not null && CharacterToken.ContentType(bytes) is { } type
            ? new SceneImage(bytes, type)
            : null;
    }

    public async Task<IReadOnlyList<SceneSummary>> ListAsync(int campaignId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        return await db
            .Scenes.AsNoTracking()
            .Where(s => s.CampaignId == campaignId)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Select(s => new SceneSummary(
                s.Id,
                s.CampaignId,
                s.Name,
                s.GridSize,
                s.GridColour,
                s.GridOpacity,
                s.GridlineWidth,
                s.Revision,
                s.Image.Length
            ))
            .ToArrayAsync();
    }

    public async Task<SceneSummary?> FindAsync(int campaignId, int id) =>
        (await ListAsync(campaignId)).SingleOrDefault(s => s.Id == id);

    public async Task<SceneImage?> ImageAsync(int campaignId, int id)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        return await db
            .Scenes.AsNoTracking()
            .Where(s => s.Id == id && s.CampaignId == campaignId)
            .Select(s => new SceneImage(s.Image, s.ContentType))
            .SingleOrDefaultAsync();
    }

    public async Task<SceneSummary> SaveAsync(SceneInput input, byte[]? image = null)
    {
        var name = input.Name.Trim();
        if (name.Length is 0 or > 120)
            throw new ArgumentException("Enter a scene name of up to 120 characters.");
        if (
            input.GridSize < 1
            || input.GridOpacity is < 0 or > 100
            || !double.IsFinite(input.GridlineWidth)
            || input.GridlineWidth <= 0
            || input.GridlineWidth > input.GridSize
        )
            throw new ArgumentException(
                "Grid size must be positive, opacity 0–100%, and line width greater than zero and no larger than a square."
            );
        if (!System.Text.RegularExpressions.Regex.IsMatch(input.GridColour, "^#[0-9a-fA-F]{6}$"))
            throw new ArgumentException("Choose a valid grid colour.");
        string? contentType = null;
        if (image is not null)
        {
            contentType = SceneImageInfo.Read(image).ContentType;
        }
        if (input.Id == 0 && image is null)
            throw new ArgumentException("Choose a background image.");
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        if (!await db.Campaigns.AnyAsync(c => c.Id == input.CampaignId))
            throw new ArgumentException("Select an existing campaign.");
        var entity =
            input.Id == 0
                ? new SceneEntity { CampaignId = input.CampaignId }
                : await db.Scenes.SingleOrDefaultAsync(s =>
                    s.Id == input.Id && s.CampaignId == input.CampaignId
                ) ?? throw new ArgumentException("This scene is not in the selected campaign.");
        if (input.Id == 0)
            db.Scenes.Add(entity);
        entity.Name = name;
        entity.GridSize = input.GridSize;
        entity.GridColour = input.GridColour;
        entity.GridOpacity = input.GridOpacity;
        entity.GridlineWidth = input.GridlineWidth;
        entity.Revision++;
        if (image is not null)
        {
            entity.Image = image;
            entity.ContentType = contentType!;
        }
        await db.SaveChangesAsync();
        return new(
            entity.Id,
            entity.CampaignId,
            entity.Name,
            entity.GridSize,
            entity.GridColour,
            entity.GridOpacity,
            entity.GridlineWidth,
            entity.Revision,
            entity.Image.Length
        );
    }

    public async Task DeleteAsync(int campaignId, int id)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var scene =
            await db.Scenes.SingleOrDefaultAsync(s => s.Id == id && s.CampaignId == campaignId)
            ?? throw new ArgumentException("This scene is not in the selected campaign.");
        db.Scenes.Remove(scene);
        await db.SaveChangesAsync();
    }
}
