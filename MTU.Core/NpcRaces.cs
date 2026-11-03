namespace MTU.Core;

public static class NpcRaces
{
    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly<string>([
            "Human",
            "Aslan",
            "Vargr",
            "Droyne",
            "Bwap",
            "K'Kree",
            "Hiver",
            "Dolphin",
            "Gorilla",
            "Robot",
        ]);

    public static bool SupportsGeneration(string? race) =>
        new[] { "Human", "Aslan", "Vargr" }.Contains(
            race?.Trim(),
            StringComparer.OrdinalIgnoreCase
        );
}
