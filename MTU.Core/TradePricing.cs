namespace MTU.Core;

/// <summary>2016 trade-good values and DMs based on the credited trade-goods aid.</summary>
public sealed record TradePriceInfo(
    long? BasePrice,
    string PurchaseModifiers,
    string SaleModifiers
);

public static class TradePricing
{
    private static readonly Dictionary<int, TradePriceInfo> Data = new()
    {
        [11] = new(20000, "IN:2 HT:3 RI:1", "NI:2 LT:1 PO:1"),
        [12] = new(10000, "NA:2 IN:5", "NI:3 AG:2"),
        [13] = new(20000, "NA:2 IN:5", "NI:3 HI:2"),
        [14] = new(5000, "AG:3 GA:2", "IN:2 PO:2"),
        [15] = new(500, "AG:3 WA:2 GA:1 AS:4", "AS:1 Fl:1 IC:1 HI:1"),
        [16] = new(1000, "AS:4", "IN:3 NI:1"),
        [21] = new(100000, "IN:2 HT:3", "NI:1 RI:2 AS:3"),
        [22] = new(75000, "IN:2 HT:1", "AS:2 NI:1"),
        [23] = new(100000, "IN:1", "HI:1 RI:2"),
        [24] = new(150000, "HT:2", "PO:1 AmberZone:2 RedZone:4"),
        [25] = new(180000, "HT:2", "AS:2 RI:2"),
        [26] = new(50000, "AG:1 WA:2", "IN:2"),
        [31] = new(20000, "AS:2 DE:1 IC:1", "IN:3 RI:2"),
        [32] = new(250000, "HT:1", "AS:1 IC:1 RI:2"),
        [33] = new(10000, "AG:2", "LO:3"),
        [34] = new(20000, "AG:2 WA:1", "RI:2 HI:2"),
        [35] = new(200000, "HI:1", "RI:4"),
        [36] = new(50000, "HT:2", "IN:2 PO:1 RI:1"),
        [41] = new(10000, "DE:2", "IN:2 AG:1 LT:2"),
        [42] = new(100000, "AS:2 HI:1", "RI:2 LT:1"),
        [43] = new(7000, "IN:1", "RI:2 NI:1"),
        [44] = new(50000, "AS:3 DE:1 IC:2", "RI:3 IN:2 HT:1"),
        [45] = new(1000000, "AS:2 LO:2", "IN:3 HT:1 NI:-2 AG:-3"),
        [46] = new(400000, "IN:1", "AG:2 HT:1"),
        [51] = new(6000, "DE:2", "HI:2 RI:3 PO:3"),
        [52] = new(3000, "AG:7", "HI:3 NA:2"),
        [53] = new(5000, "AS:4", "IN:3 NI:1"),
        [54] = new(20000, "AG:2 WA:1", "IN:2 HT:1"),
        [55] = new(1000, "AG:6", "RI:2 IN:1"),
        [56] = new(15000, "IN:2 HT:1", "NI:2 HI:1"),
        [61] = new(50000, "WA:2", "IN:6"),
        [62] = new(250000, "HT:1", "AS:4 IC:4 RI:8 AmberZone:6 RedZone:6"),
        [63] = new(100000, "AS:1 DE:1 GA:1 WA:1", "RI:6 HI:6"),
        [64] = new(50000, "AG:2 WA:1", "RI:6 HI:4"),
        [65] = new(150000, "HT:2", "PO:6 AmberZone:8 RedZone:10"),
        [66] = new(null, "", ""),
    };

    // Purchase price as percent of base, indexed by modified roll -1 through 23.
    private static readonly int[] Percentages =
    [
        200,
        175,
        150,
        135,
        125,
        120,
        115,
        110,
        105,
        100,
        95,
        90,
        85,
        80,
        75,
        70,
        65,
        60,
        55,
        50,
        45,
        40,
        35,
        30,
        25,
    ];

    public static int Percentage(int modifiedRoll) =>
        Percentages[Math.Clamp(modifiedRoll, -1, 23) + 1];

    public static TradePriceInfo Get(int d66) => Data[d66];

    // Keep both contributions visible even when they cancel; missing values stay blank.
    public static string FormatTradeModifiers(MarketLine line, bool selling)
    {
        var modifiers = new[]
        {
            selling ? line.SaleDm : line.PurchaseDm,
            selling ? -line.PurchaseDm : -line.SaleDm,
        };
        return string.Join(
            "/",
            modifiers
                .Where(value => value.HasValue && value.Value != 0)
                .Select(value => value!.Value)
                .OrderByDescending(value => value)
                .Select(value =>
                    value.ToString("+#;-#", System.Globalization.CultureInfo.InvariantCulture)
                )
        );
    }

    private static readonly int[] SalePercentages =
    [
        30,
        40,
        45,
        50,
        55,
        60,
        65,
        70,
        75,
        80,
        85,
        90,
        100,
        105,
        110,
        115,
        120,
        125,
        130,
        135,
        140,
        145,
        150,
        155,
        160,
    ];

    public static int SalePercentage(int modifiedRoll) =>
        SalePercentages[Math.Clamp(modifiedRoll, -1, 23) + 1];

    private static HashSet<string> Codes(string? remarks)
    {
        var codes = new HashSet<string>(
            (remarks ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase
        );
        return codes;
    }

    public static string WithZone(string? remarks, string? zone)
    {
        // Zone modifiers come only from the world's Zone field, never its remarks.
        var codes = (remarks ?? "")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(code =>
                !code.Equals("AmberZone", StringComparison.OrdinalIgnoreCase)
                && !code.Equals("RedZone", StringComparison.OrdinalIgnoreCase)
            );
        var zoneCode = zone?.Trim().ToUpperInvariant() switch
        {
            "A" or "AMBER" or "AMBERZONE" => "AmberZone",
            "R" or "RED" or "REDZONE" => "RedZone",
            _ => null,
        };
        return string.Join(" ", zoneCode is null ? codes : codes.Append(zoneCode));
    }

    public static bool HasSaleDemand(int d66, string? remarks)
    {
        var codes = Codes(remarks);
        return MatchingModifier(Get(d66).SaleModifiers, codes) > 0;
    }

    public static MarketLine SalePrice(
        MarketLine line,
        int skillDm,
        string? remarks,
        Func<int> d6,
        int buyerDm = 0
    )
    {
        var info = Get(line.D66);
        if (info.BasePrice is null)
            return line;
        var codes = Codes(remarks);
        var roll = d6() + d6() + d6();
        var purchaseDm = MatchingModifier(info.PurchaseModifiers, codes);
        var saleDm = MatchingModifier(info.SaleModifiers, codes);
        var percent = SalePercentage(roll + skillDm + buyerDm + saleDm - purchaseDm);
        return line with
        {
            BasePrice = info.BasePrice,
            PricePercent = percent,
            SalePrice = info.BasePrice.Value * percent / 100,
            PriceRoll = roll,
            PurchaseDm = purchaseDm,
            SaleDm = saleDm,
        };
    }

    private static int MatchingModifier(string modifiers, HashSet<string> codes)
    {
        var matches = modifiers
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Split(':', 2))
            .Where(pair => codes.Contains(pair[0]))
            .Select(pair => int.Parse(pair[1], System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        return matches.Length == 0 ? 0 : matches.Max();
    }

    public static MarketLine Price(
        MarketLine line,
        int skillDm,
        string? remarks,
        Func<int> d6,
        int supplierDm = 0
    )
    {
        var info = Get(line.D66);
        if (info.BasePrice is null)
            return line with { BasePrice = null, PricePercent = null, BuyPrice = null };
        var codes = Codes(remarks);
        var roll = d6() + d6() + d6();
        var purchaseDm = MatchingModifier(info.PurchaseModifiers, codes);
        var saleDm = MatchingModifier(info.SaleModifiers, codes);
        var percent = Percentage(roll + skillDm + supplierDm + purchaseDm - saleDm);
        return line with
        {
            BasePrice = info.BasePrice,
            PricePercent = percent,
            BuyPrice = info.BasePrice.Value * percent / 100,
            PriceRoll = roll,
            PurchaseDm = purchaseDm,
            SaleDm = saleDm,
        };
    }
}
