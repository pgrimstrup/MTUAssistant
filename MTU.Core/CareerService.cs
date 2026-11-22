using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

/// <summary>Stores the career terms of Travellers and of Patrons and Enemies.</summary>
public sealed class CareerService(MtuDbContext db)
{
    public const int MaxTerms = 40;

    /// <summary>Numbers stored terms 1, 2, 3… in Sequence order.</summary>
    public static IReadOnlyList<CareerTerm> Numbered(IEnumerable<CareerTermEntity> terms) =>
        terms
            .OrderBy(t => t.Sequence)
            .ThenBy(t => t.Id)
            .Select(
                (t, index) =>
                    new CareerTerm(
                        t.Id,
                        t.CharacterId,
                        index + 1,
                        t.Service,
                        t.SurvivalCheck,
                        t.AdvancementCheck,
                        t.CommissionCheck,
                        t.Notes
                    )
            )
            .ToArray();

    private IQueryable<CareerTermEntity> Terms(int campaignId, int characterId) =>
        db.CareerTerms.Where(t =>
            t.CharacterId == characterId
            && t.Character.CampaignId == campaignId
            && !t.Character.IsAnimal
        );

    public async Task<IReadOnlyList<CareerTerm>> ListAsync(int campaignId, int characterId) =>
        Numbered(await Terms(campaignId, characterId).AsNoTracking().ToArrayAsync());

    /// <summary>Adds a term at the end of the career (Id 0) or updates an existing one in place.
    /// A Commission Check is only kept for a military term while no other term holds the
    /// commission. An Advancement Check is only kept for services that have one, and never in the
    /// term that holds the commission.</summary>
    public async Task<CareerTerm> SaveAsync(int campaignId, CareerTerm term)
    {
        var service = CareerServices.Normalize(term.Service);
        if (service.Length is 0 or > 120)
            throw new ArgumentException("Enter a service name of up to 120 characters.");
        if (service.Equals(CareerServices.Other, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Enter the name of the other service.");
        var notes = term.Notes?.Trim() ?? "";
        if (notes.Length > 4000)
            throw new ArgumentException("Notes must be at most 4000 characters.");
        if (
            !await db.Characters.AnyAsync(c =>
                c.Id == term.CharacterId && c.CampaignId == campaignId && !c.IsAnimal
            )
        )
            throw new ArgumentException("This character is not in the current campaign.");
        var existing = await Terms(campaignId, term.CharacterId).ToListAsync();
        CareerTermEntity entity;
        if (term.Id == 0)
        {
            if (existing.Count >= MaxTerms)
                throw new ArgumentException($"A career can have at most {MaxTerms} terms.");
            entity = new CareerTermEntity
            {
                CharacterId = term.CharacterId,
                Sequence = existing.Select(t => t.Sequence).DefaultIfEmpty(0).Max() + 1,
            };
            db.CareerTerms.Add(entity);
            existing.Add(entity);
        }
        else
            entity =
                existing.SingleOrDefault(t => t.Id == term.Id)
                ?? throw new ArgumentException("This career term no longer exists.");
        entity.Service = service;
        entity.SurvivalCheck = term.Survival;
        // Checks a service or career stage does not have are stored as not passed.
        entity.CommissionCheck =
            term.Commission
            && CareerRules.CommissionAvailable(Numbered(existing), entity.Id, service);
        // A passed commission is the term's one promotion, so it rules out an advancement.
        entity.AdvancementCheck =
            term.Advancement && !entity.CommissionCheck && CareerServices.HasAdvancement(service);
        entity.Notes = notes;
        await db.SaveChangesAsync();
        return Numbered(existing).Single(t => t.Id == entity.Id);
    }

    public async Task DeleteAsync(int campaignId, int characterId, int termId)
    {
        var entity = await Terms(campaignId, characterId).SingleOrDefaultAsync(t => t.Id == termId);
        if (entity is null)
            return;
        db.CareerTerms.Remove(entity);
        await db.SaveChangesAsync();
    }
}
