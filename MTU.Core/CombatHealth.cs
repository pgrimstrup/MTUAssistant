using System.Text.Json.Serialization;

namespace MTU.Core;

public sealed record AttributeReduction(int Attribute, int Amount);

public sealed record CombatHealth(
    int Strength,
    int Dexterity,
    int Endurance,
    IReadOnlyList<AttributeReduction>? Reductions = null
)
{
    [JsonIgnore]
    public int Total => Strength + Dexterity + Endurance;

    public CombatHealth WithTotal(int target, CombatHealth maximum)
    {
        target = Math.Clamp(target, 0, maximum.Total);
        var values = new[] { Strength, Dexterity, Endurance };
        var limits = new[] { maximum.Strength, maximum.Dexterity, maximum.Endurance };
        var history = Reductions?.ToList() ?? [];
        var difference = target - Total;
        if (difference < 0)
        {
            var damage = -difference;
            var higher = Strength >= Dexterity ? 0 : 1;
            foreach (var attribute in new[] { 2, higher, 1 - higher })
            {
                var loss = Math.Min(damage, values[attribute]);
                if (loss == 0)
                    continue;
                values[attribute] -= loss;
                damage -= loss;
                history.Add(new(attribute, loss));
            }
        }
        else if (difference > 0)
        {
            var healing = difference;
            while (healing > 0 && history.Count > 0)
            {
                var reduction = history[^1];
                var restored = Math.Min(
                    healing,
                    Math.Min(
                        reduction.Amount,
                        Math.Max(0, limits[reduction.Attribute] - values[reduction.Attribute])
                    )
                );
                values[reduction.Attribute] += restored;
                healing -= restored;
                history.RemoveAt(history.Count - 1);
                if (restored > 0 && restored < reduction.Amount)
                    history.Add(reduction with { Amount = reduction.Amount - restored });
            }
            // Support attribute-only states without a recorded damage history.
            var higher = maximum.Strength >= maximum.Dexterity ? 0 : 1;
            foreach (var attribute in new[] { 1 - higher, higher, 2 })
            {
                var restored = Math.Min(
                    healing,
                    Math.Max(0, limits[attribute] - values[attribute])
                );
                values[attribute] += restored;
                healing -= restored;
            }
        }
        return new(values[0], values[1], values[2], history.ToArray());
    }
}
