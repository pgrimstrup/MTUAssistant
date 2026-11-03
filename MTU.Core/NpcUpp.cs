namespace MTU.Core;

public static partial class NpcGenerator
{
    public static string GenerateUpp(
        NpcType type,
        string race,
        NpcSkillLevel level,
        Func<int>? d6 = null
    )
    {
        if (!NpcRaces.SupportsGeneration(race))
            throw new ArgumentException("This race requires manual entry.");
        if (!Enum.IsDefined(type))
            throw new ArgumentException("Select a valid NPC type.");
        _ = SkillRollCount(level);
        d6 ??= () => Random.Shared.Next(1, 7);
        var values = Enumerable.Range(0, 6).Select(_ => Roll(d6) + Roll(d6)).ToArray();
        if (type != NpcType.AllyEnemy)
        {
            Array.Sort(values);
            var lower = values[..3];
            var upper = values[3..];
            Shuffle(lower);
            Shuffle(upper);
            values = type == NpcType.PatronCivilian ? [.. lower, .. upper] : [.. upper, .. lower];
        }
        switch (race.Trim().ToUpperInvariant())
        {
            case "ASLAN":
                values[0] += 2;
                values[1] -= 2;
                break;
            case "VARGR":
                values[0] -= 2;
                values[1]++;
                values[2]--;
                break;
        }
        for (var i = 0; i < values.Length; i++)
            values[i] = Math.Clamp(values[i], 3, 12);
        var numericLevel = (int)level + 1;
        for (var i = 0; i < Math.Min(numericLevel, 8) / 2; i++)
            Adjust(1);
        for (var i = 8; i < numericLevel; i++)
            Adjust(-1);
        return new string(values.Select(value => "0123456789ABC"[value]).ToArray());

        void Shuffle(int[] group)
        {
            // Group sizes are three and two, both divisors of six: no modulo bias.
            for (var i = group.Length - 1; i > 0; i--)
            {
                var index = (Roll(d6) - 1) % (i + 1);
                (group[i], group[index]) = (group[index], group[i]);
            }
        }
        void Adjust(int delta)
        {
            var index = Roll(d6) - 1;
            values[index] = Math.Clamp(values[index] + delta, 3, 12);
        }
    }
}
