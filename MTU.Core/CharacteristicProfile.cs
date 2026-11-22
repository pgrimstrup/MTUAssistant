namespace MTU.Core;

/// <summary>Splits a UPP into its six characteristic scores and joins them back.
/// The UPP stays the stored format: one eHex digit per characteristic, in the order
/// STR, DEX, END, INT, EDU, SOC, optionally followed by more digits such as PSI.</summary>
public static class CharacteristicProfile
{
    public const string EHex = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    public const int MaxScore = 33;
    public const char Unknown = '?';

    public static IReadOnlyList<string> Labels { get; } =
    ["STR", "DEX", "END", "INT", "EDU", "SOC"];

    /// <summary>A missing or unreadable digit gives a null score. Anything after the sixth
    /// digit is returned unchanged in Extra.</summary>
    public static (int?[] Scores, string Extra) Split(string? upp)
    {
        var profile = upp?.Trim().ToUpperInvariant() ?? "";
        var scores = new int?[Labels.Count];
        for (var i = 0; i < scores.Length && i < profile.Length; i++)
            scores[i] = EHex.IndexOf(profile[i]) is >= 0 and var score ? score : null;
        return (scores, profile.Length > scores.Length ? profile[scores.Length..] : "");
    }

    /// <summary>All scores unknown and no extra digits gives an empty UPP. Otherwise an
    /// unknown score is written as "?", which every reader already treats as not recorded.</summary>
    public static string Join(IReadOnlyList<int?> scores, string? extra = null)
    {
        if (scores.Count != Labels.Count)
            throw new ArgumentException("A profile has six characteristics.");
        for (var i = 0; i < scores.Count; i++)
            if (scores[i] is < 0 or > MaxScore)
                throw new ArgumentException($"{Labels[i]} must be between 0 and {MaxScore}.");
        var tail = extra?.Trim().ToUpperInvariant() ?? "";
        if (tail.Length == 0 && scores.All(score => score is null))
            return "";
        return string.Concat(scores.Select(score => score is int value ? EHex[value] : Unknown))
            + tail;
    }
}
