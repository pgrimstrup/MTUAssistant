using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

public sealed record TradeGood(int D66, string Name, string Availability, int Dice, int Multiplier, bool Illegal = false);
public sealed record MarketLine(int D66, string Name, int Lots, int? Tons, bool Illegal, bool Stolen,
    long? BasePrice = null, int? PricePercent = null, long? BuyPrice = null, int? PriceRoll = null, int? PurchaseDm = null, int? SaleDm = null, long? SalePrice = null);
public sealed record MarketResult(string Mode, int SkillDm, DateTime RolledUtc, int RandomRolls, IReadOnlyList<MarketLine> Goods, int PricingVersion = 0, int? SupplierDm = null,
    [property: System.Text.Json.Serialization.JsonIgnore] bool IsOutdated = false, int? BuyerDm = null, int DemandVersion = 0, long? TargetDay = null);

/// <summary>Goods and lot sizes based on Tim Heiderich's Traveller 2e (2016) trade-goods aid,
/// https://github.com/timtoon/trade-goods, CC BY-NC 4.0 as stated in the aid.</summary>
public static class TradeGoods
{
    public static readonly IReadOnlyList<TradeGood> All = Array.AsReadOnly<TradeGood>([
        new(11,"Common Electronics","All",2,10), new(12,"Common Industrial Goods","All",2,10),
        new(13,"Common Manufactured Goods","All",2,10), new(14,"Common Raw Materials","All",2,20),
        new(15,"Common Consumables","All",2,20), new(16,"Common Ore","All",2,20),
        new(21,"Advanced Electronics","In Ht",1,5), new(22,"Advanced Machine Parts","In Ht",1,5),
        new(23,"Advanced Manufactured Goods","In Ht",1,5), new(24,"Advanced Weapons","In Ht",1,5),
        new(25,"Advanced Vehicles","In Ht",1,5), new(26,"Biochemicals","Ag Wa",1,5),
        new(31,"Crystals & Gems","As De Ic",1,5), new(32,"Cybernetics","Ht",1,1),
        new(33,"Live Animals","Ag Ga",1,10), new(34,"Luxury Consumables","Ag Ga Wa",1,10),
        new(35,"Luxury Goods","Hi",1,1), new(36,"Medical Supplies","Ht Hi",1,5),
        new(41,"Petrochemicals","De Fl Ic Wa",1,10), new(42,"Pharmaceuticals","As De Hi Wa",1,1),
        new(43,"Polymers","In",1,10), new(44,"Precious Metals","As De Ic Fl",1,1),
        new(45,"Radioactives","As De Lo",1,1), new(46,"Robots","In",1,5),
        new(51,"Spices","Ga De Wa",1,10), new(52,"Textiles","Ag Ni",1,20),
        new(53,"Uncommon Ore","As Ic",1,20), new(54,"Uncommon Raw Materials","Ag De Wa",1,10),
        new(55,"Wood","Ag Ga",1,20), new(56,"Vehicles","In Ht",1,10),
        new(61,"Illegal Biochemicals","Ag Wa",1,5,true), new(62,"Illegal Cybernetics","Ht",1,1,true),
        new(63,"Illegal Drugs","As De Hi Wa",1,1,true), new(64,"Illegal Luxuries","Ag Ga Wa",1,1,true),
        new(65,"Illegal Weapons","In Ht",1,5,true), new(66,"Exotics","",0,0)
    ]);
}

public static class MarketGenerator
{
    public static MarketResult Generate(string mode, int skillDm, string? remarks, Func<int>? d6 = null, int supplierDm = -2)
    {
        if (mode is not ("Broker" or "Streetwise")) throw new ArgumentException("Unknown trading mode", nameof(mode));
        d6 ??= () => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 7);
        var codes = new HashSet<string>((remarks ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
        var goods = TradeGoods.All;
        var lots = new Dictionary<int, (int Count, int? Tons)>();
        void Add(TradeGood item)
        {
            int? tons = item.Dice == 0 ? null : Enumerable.Range(0, item.Dice).Sum(_ => d6()) * item.Multiplier;
            var old = lots.GetValueOrDefault(item.D66);
            lots[item.D66] = (old.Count + 1, tons is null ? null : (old.Tons ?? 0) + tons);
        }
        foreach (var item in goods)
        {
            var available = item.Availability.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(code => code.Equals("All", StringComparison.OrdinalIgnoreCase) || codes.Contains(code));
            if (available && (mode == "Broker" ? !item.Illegal : item.Illegal)) Add(item);
        }
        var rolls = d6();
        for (var i = 0; i < rolls; i++)
        {
            TradeGood item;
            do { var code = d6() * 10 + d6(); item = goods.Single(g => g.D66 == code); }
            while (mode == "Broker" && item.Illegal);
            Add(item);
        }
        var lines = lots.OrderBy(pair => pair.Key).Select(pair => {
            var good = goods.Single(g => g.D66 == pair.Key);
            return new MarketLine(good.D66, good.Name, pair.Value.Count, pair.Value.Tons, good.Illegal,
                mode == "Streetwise" && !good.Illegal);
        }).ToArray();
        return new MarketResult(mode, skillDm, DateTime.UtcNow, rolls,
            lines.Select(line => TradePricing.Price(line, skillDm, remarks, d6, supplierDm)).ToArray(), 1, supplierDm);
    }
    public static int? DemandTons(TradeGood good, Func<int> d6) => good.Dice == 0 ? null
        : Enumerable.Range(0, good.Dice).Sum(_ => d6()) * good.Multiplier;

    public static MarketResult GenerateSales(string mode, int skillDm, string? remarks, Func<int>? d6 = null, int buyerDm = -2)
    {
        if (mode is not ("Broker" or "Streetwise")) throw new ArgumentException("Unknown trading mode", nameof(mode));
        d6 ??= () => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 7);
        var lines = TradeGoods.All.Where(g => (mode == "Streetwise" || !g.Illegal)
                && (g.Availability == "All" ? mode == "Broker" : TradePricing.HasSaleDemand(g.D66, remarks)))
            .Select(g => TradePricing.SalePrice(new MarketLine(g.D66, g.Name, 0, DemandTons(g, d6), g.Illegal, false), skillDm, remarks, d6, buyerDm))
            .ToArray();
        return new MarketResult(mode, skillDm, DateTime.UtcNow, 0, lines, 1, BuyerDm: buyerDm, DemandVersion: 1);
    }

}

public sealed class TradingService(MtuDbContext db)
{
    public async Task<MarketResult?> GetAsync(int sectorId, string hex, string mode, string? remarks = null, CancellationToken ct = default)
    {
        var entity = await db.TradeMarkets.SingleOrDefaultAsync(m => m.SectorId == sectorId && m.Hex == hex && m.Mode == mode, ct);
        if (entity is null) return null;
        var market = JsonSerializer.Deserialize<MarketResult>(entity.ResultJson);
        if (market is null) throw new InvalidDataException("Invalid saved market data.");
        if (market.PricingVersion == 0)
        {
            // Add prices once to existing markets without changing their goods or tonnage.
            int Die() => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 7);
            market = market with { Goods = market.Goods.Select(line => TradePricing.Price(line, market.SkillDm, remarks, Die)).ToArray(), PricingVersion = 1 };
            entity.ResultJson = JsonSerializer.Serialize(market);
            await db.SaveChangesAsync(ct);
        }
        // A stored market is always considered outdated when reopened.
        return market with { IsOutdated = true };
    }
    public async Task<MarketResult> RollAsync(int sectorId, string hex, string mode, int skillDm, string? remarks, int supplierDm = -2, CancellationToken ct = default, long? targetDay = null)
    {
        var result = MarketGenerator.Generate(mode, skillDm, remarks, supplierDm: supplierDm) with { TargetDay = targetDay };
        var market = await db.TradeMarkets.SingleOrDefaultAsync(m => m.SectorId == sectorId && m.Hex == hex && m.Mode == mode, ct);
        if (market is null) { market = new TradeMarketEntity { SectorId = sectorId, Hex = hex, Mode = mode }; db.TradeMarkets.Add(market); }
        market.ResultJson = JsonSerializer.Serialize(result);
        await db.SaveChangesAsync(ct);
        return result;
    }
    public async Task<MarketResult?> GetSalesAsync(int sectorId, string hex, string mode, CancellationToken ct = default)
    {
        var key = "Sale:" + mode;
        var result = await GetAsync(sectorId, hex, key, ct: ct);
        if (result is null || result.DemandVersion != 0) return result;
        // Add demand once to older saved Imports without changing their price rolls.
        int Die() => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 7);
        result = result with { Goods = result.Goods.Select(line => line with {
            Tons = MarketGenerator.DemandTons(TradeGoods.All.Single(g => g.D66 == line.D66), Die) }).ToArray(), DemandVersion = 1 };
        var entity = await db.TradeMarkets.SingleAsync(m => m.SectorId == sectorId && m.Hex == hex && m.Mode == key, ct);
        entity.ResultJson = JsonSerializer.Serialize(result);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<MarketResult> RollSalesAsync(int sectorId, string hex, string mode, int skillDm, string? remarks, int buyerDm = -2, CancellationToken ct = default, long? targetDay = null)
    {
        var result = MarketGenerator.GenerateSales(mode, skillDm, remarks, buyerDm: buyerDm) with { TargetDay = targetDay };
        var key = "Sale:" + mode;
        var market = await db.TradeMarkets.SingleOrDefaultAsync(m => m.SectorId == sectorId && m.Hex == hex && m.Mode == key, ct);
        if (market is null) { market = new TradeMarketEntity { SectorId = sectorId, Hex = hex, Mode = key }; db.TradeMarkets.Add(market); }
        market.ResultJson = JsonSerializer.Serialize(result);
        await db.SaveChangesAsync(ct);
        return result;
    }

}
