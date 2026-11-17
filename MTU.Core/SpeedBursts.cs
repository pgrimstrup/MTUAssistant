namespace MTU.Core;

public sealed partial class Encounter
{
    public static bool IsAslan(SceneCombatant entry) =>
        !entry.IsAnimal && entry.Race.Trim().Equals("Aslan", StringComparison.OrdinalIgnoreCase);

    // RULES-ASSUMPTION: Aslan speed bursts per encounter are 3 + END DM, using the END
    // in the UPP (not END reduced by damage), with a minimum of zero. Everyone else has none.
    public static int SpeedBurstLimitFor(SceneCombatant entry) =>
        IsAslan(entry) ? Math.Max(0, 3 + Dm(Stat(entry, 2))) : 0;

    private CombatantActionState WithSpeed(CombatantActionState state, SceneCombatant actor) =>
        state with
        {
            Speed = SpeedFor(actor),
            SpeedBurstLimit = SpeedBurstLimitFor(actor),
        };

    public void SetSpeedBurst(int id, bool speedBurst, IReadOnlyList<SceneCombatant> entries)
    {
        RequireActor(id, entries);
        RequireUnresolved(id);
        var state = ActionFor(id);
        if (speedBurst && state.SpeedBurstsRemaining == 0)
            throw new ArgumentException("No speed bursts remain in this encounter.");
        state = state with { SpeedBurst = speedBurst };
        actions[id] = state with { MovementDiameter = MovementDiameterFor(state) };
    }

    // Fewest Move actions (1–3) whose reach covers the distance; the first
    // burstMoves Moves cover double the per-Move distance.
    public static int AutomaticMoveCount(double distance, double speed, int burstMoves)
    {
        var needed = Math.Max(0, distance - .8) / speed - 1e-9;
        for (var moves = 1; moves < 3; moves++)
            if (moves + Math.Min(moves, Math.Max(0, burstMoves)) >= needed)
                return moves;
        return 3;
    }
}
