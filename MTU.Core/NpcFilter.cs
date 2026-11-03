namespace MTU.Core;

public static class NpcFilter
{
    public static bool Matches(Npc npc, string? query)
    {
        var text = query?.Trim();
        if (string.IsNullOrEmpty(text))
            return true;
        bool Contains(string value) => value.Contains(text, StringComparison.OrdinalIgnoreCase);
        return Contains(npc.Character.Name)
            || Contains(npc.Occupation)
            || Contains(npc.Character.Race)
            || Contains($"{(int)npc.SkillLevel + 1} — {npc.SkillLevel}")
            || Contains(NpcTypes.Label(npc.Type))
            || (npc.Character.Skills ?? []).Any(s => Contains($"{s.Name} {s.Level}"));
    }
}
