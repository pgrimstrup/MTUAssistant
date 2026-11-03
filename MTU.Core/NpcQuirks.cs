namespace MTU.Core;

public static partial class NpcGenerator
{
    // Ordered by D66: 11-16, 21-26, through 61-66.
    public static IReadOnlyList<string> Quirks { get; } =
        Array.AsReadOnly<string>([
            "Loyal",
            "Distracted by other worries",
            "In debt to criminals",
            "Makes very bad jokes",
            "Will betray characters",
            "Aggressive",
            "Has secret allies",
            "Secret anagathic user",
            "Looking for something",
            "Helpful",
            "Forgetful",
            "Wants to hire the Travellers",
            "Has useful contacts",
            "Artistic",
            "Easily confused",
            "Unusually ugly",
            "Worried about current situation",
            "Shows pictures of their children",
            "Rumour-monger",
            "Unusually provincial",
            "Drunkard or drug addict",
            "Government informant",
            "Mistakes a Traveller for someone else",
            "Possesses unusually advanced technology",
            "Unusually handsome or beautiful",
            "Spying on the Travellers",
            "Possesses TAS membership",
            "Is secretly hostile towards the Travellers",
            "Wants to borrow money",
            "Is convinced the Travellers are dangerous",
            "Involved in political intrigue",
            "Has a dangerous secret",
            "Wants to get off planet as soon as possible",
            "Attracted to a Traveller",
            "From offworld",
            "Possesses telepathy or other unusual quality",
        ]);

    public static (string First, string Second) GenerateQuirks(Func<int>? d6 = null)
    {
        d6 ??= () => Random.Shared.Next(1, 7);
        int Index() => (Roll(d6) - 1) * 6 + Roll(d6) - 1;
        var first = Index();
        var second = Index();
        return (Quirks[first], first == second ? "" : Quirks[second]);
    }
}
