namespace MTU.Core;

public static class SkillRules
{
    private static (string Base, string? Speciality) Split(string name)
    {
        var text = name.Trim();
        var marker = text.IndexOf('(');
        return marker > 0 && text.EndsWith(')') && text[(marker + 1)..^1].Trim().Length > 0
            ? (text[..marker].Trim(), text[(marker + 1)..^1].Trim())
            : (text, null);
    }

    public static int MaximumRank(string name) =>
        Split(name).Base.Equals("Jack-of-All-Trades", StringComparison.OrdinalIgnoreCase) ? 3 : 5;

    /// <summary>Null means unskilled; zero means basic knowledge, never an unrelated speciality's rank.</summary>
    public static int? Rank(IEnumerable<CharacterSkill> skills, string requiredSkill)
    {
        var required = Split(requiredSkill);
        var family = skills
            .Select(s => (Skill: s, Name: Split(s.Name)))
            .Where(s => s.Name.Base.Equals(required.Base, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var exact = family
            .Where(s =>
                string.Equals(
                    s.Name.Speciality,
                    required.Speciality,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Select(s => (int?)Math.Clamp(s.Skill.Level, 0, MaximumRank(required.Base)))
            .Max();
        if (exact.HasValue)
            return
                required.Speciality is null
                && CharacterSkillCatalogue.Find(required.Base)?.Specialities.Count > 0
                ? 0
                : exact;
        if (family.Any(s => s.Name.Speciality is not null))
            return 0;
        if (family.Any(s => s.Name.Speciality is null))
            return 0;
        return null;
    }

    public static int Dm(IEnumerable<CharacterSkill> skills, string requiredSkill)
    {
        var entries = skills.ToArray();
        return Rank(entries, requiredSkill) ?? -3 + (Rank(entries, "Jack-of-All-Trades") ?? 0);
    }

    public static string Validate(string name, int rank)
    {
        var parsed = Split(name);
        var maximum = MaximumRank(name);
        if (rank < 0 || rank > maximum)
            throw new ArgumentException($"Skill rank must be between 0 and {maximum}.");
        var definition = CharacterSkillCatalogue.Find(parsed.Base);
        if (definition?.Specialities.Count > 0 && parsed.Speciality is null && rank > 0)
            throw new ArgumentException("Choose a specialization for a skill above rank 0.");
        if (parsed.Speciality is not null && rank == 0)
            return definition?.Name ?? parsed.Base;
        return parsed.Speciality is null
            ? definition?.Name ?? parsed.Base
            : $"{definition?.Name ?? parsed.Base} ({parsed.Speciality})";
    }
}
