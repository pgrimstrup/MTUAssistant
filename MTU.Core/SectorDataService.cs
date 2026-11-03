using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

public sealed class SectorRefreshState
{
    public int Id { get; set; }
    public DateTime LastRefreshUtc { get; set; }
}

public sealed record SectorDataStatistics(
    int Sectors,
    int SectorNames,
    int Subsectors,
    int Systems,
    int Allegiances,
    int Borders,
    int Routes,
    IReadOnlyList<string> Milieus,
    DateTime? LastRefreshUtc,
    IReadOnlyList<MilieuPopulationStatistics> PopulationByMilieu
);

/// <summary>Reads the persisted sector data and refresh timestamp using independent database scopes.</summary>
public sealed class SectorDataService(IServiceScopeFactory scopeFactory)
{
    public async Task<SectorDataStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var sectors = await db.Sectors.CountAsync(cancellationToken);
        var names = await db.SectorNames.CountAsync(cancellationToken);
        var subsectors = await db.Subsectors.CountAsync(cancellationToken);
        var systems = await db.Systems.CountAsync(cancellationToken);
        var allegiances = await db.Allegiances.CountAsync(cancellationToken);
        var borders = await db.Borders.CountAsync(cancellationToken);
        var routes = await db.Routes.CountAsync(cancellationToken);
        var milieus = await db
            .Sectors.Select(s => s.Milieu)
            .Distinct()
            .OrderBy(m => m)
            .ToArrayAsync(cancellationToken);
        var lastRefresh = await db
            .SectorRefreshState.AsNoTracking()
            .Where(s => s.Id == 1)
            .Select(s => (DateTime?)s.LastRefreshUtc)
            .SingleOrDefaultAsync(cancellationToken);
        var populationWorlds = await db
            .Systems.AsNoTracking()
            .Select(s => new PopulationWorld(s.Sector.Milieu, s.SectorId, s.Uwp, s.Pbg, s.A))
            .ToArrayAsync(cancellationToken);
        var populationAllegiances = await db
            .Allegiances.AsNoTracking()
            .Select(a => new PopulationAllegiance(a.Sector.Milieu, a.SectorId, a.Code, a.Name))
            .ToArrayAsync(cancellationToken);
        var population = SectorPopulation.Calculate(
            milieus,
            populationWorlds,
            populationAllegiances
        );
        await transaction.CommitAsync(cancellationToken);
        return new(
            sectors,
            names,
            subsectors,
            systems,
            allegiances,
            borders,
            routes,
            milieus,
            lastRefresh is { } utc ? DateTime.SpecifyKind(utc, DateTimeKind.Utc) : null,
            population
        );
    }

    public async Task RecordCompletedRefreshAsync(DateTime completedUtc)
    {
        if (completedUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Refresh completion must use UTC.", nameof(completedUtc));
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var state = await db.SectorRefreshState.SingleOrDefaultAsync(s => s.Id == 1);
        if (state is null)
        {
            state = new SectorRefreshState { Id = 1 };
            db.SectorRefreshState.Add(state);
        }
        state.LastRefreshUtc = completedUtc;
        await db.SaveChangesAsync();
    }
}
