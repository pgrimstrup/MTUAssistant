using System.Text.Json.Serialization;

namespace MTU.Core;

public sealed record EncounterSnapshot(
    bool Active,
    int Round,
    int? SelectedId,
    int EnemyTactics,
    Dictionary<int, int> Initiative,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        Dictionary<int, int>? HitPoints,
    Dictionary<int, int> Ambush,
    Dictionary<int, CombatHealth>? Attributes = null,
    Dictionary<int, CombatantActionState>? Actions = null
);

public sealed record PlayerCombatant(
    int Id,
    int CharacterId,
    string Name,
    bool IsNpc,
    bool HasTokenImage,
    bool IsVisible,
    int? Initiative,
    double HealthWidth,
    string HealthColour,
    bool IsDead,
    bool IsCurrent,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CurrentHp,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CurrentStrength,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CurrentDexterity,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CurrentEndurance,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? OriginalStrength,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? OriginalDexterity,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? OriginalEndurance
);

public sealed partial class Encounter
{
    public Dictionary<int, int> Initiative { get; } = [];
    private readonly Dictionary<int, CombatHealth> attributes = [];
    private readonly Dictionary<int, int> ambush = [];
    public bool Active { get; private set; }
    public int Round { get; private set; }
    private int? selectedId;
    public int? SelectedId
    {
        get => selectedId;
        set
        {
            if (selectedId != value)
                ClearMovement(selectedId);
            selectedId = value;
        }
    }
    public int EnemyTactics { get; private set; }

    public static int Stat(SceneCombatant e, int i) =>
        e.Upp.Length > i
            ? Math.Max(
                0,
                "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ".IndexOf(char.ToUpperInvariant(e.Upp[i]))
            )
            : 0;

    public static int Dm(int value) =>
        value switch
        {
            0 => -3,
            <= 2 => -2,
            <= 5 => -1,
            <= 8 => 0,
            <= 11 => 1,
            <= 14 => 2,
            _ => 3,
        };

    public static int MaximumHp(SceneCombatant e) => Stat(e, 0) + Stat(e, 1) + Stat(e, 2);

    public static CombatHealth MaximumHealth(SceneCombatant e) =>
        new(Stat(e, 0), Stat(e, 1), Stat(e, 2));

    public CombatHealth Health(SceneCombatant e) =>
        attributes.GetValueOrDefault(e.Id) ?? MaximumHealth(e);

    public int Hp(SceneCombatant e) => Health(e).Total;

    public bool IsDead(SceneCombatant e) => Hp(e) <= 0;

    public void SetHp(int id, int hp, IReadOnlyList<SceneCombatant> entries)
    {
        var entry =
            entries.FirstOrDefault(e => e.Id == id)
            ?? throw new ArgumentException("Choose a combatant in this scene.");
        attributes[id] = Health(entry).WithTotal(hp, MaximumHealth(entry));
        if (!Active)
            return;
        if (SelectedId is null)
            SelectedId = Order(entries).FirstOrDefault(e => !IsDead(e))?.Id;
        else if (
            entries.FirstOrDefault(e => e.Id == SelectedId) is { } selected
            && IsDead(selected)
        )
            Next(entries);
        CaptureSelectedOrigin(entries);
    }

    public string HealthColour(SceneCombatant e) =>
        Hp(e) > Stat(e, 0) + Stat(e, 1) ? "green"
        : Hp(e) > (Stat(e, 0) + Stat(e, 1)) / 2.0 ? "orange"
        : "red";

    public double HealthWidth(SceneCombatant e) =>
        Hp(e) <= 0 ? 100 : Math.Clamp(100.0 * Hp(e) / Math.Max(1, MaximumHp(e)), 0, 100);

    public IEnumerable<SceneCombatant> Order(IEnumerable<SceneCombatant> entries) =>
        entries
            .OrderByDescending(e => Initiative.GetValueOrDefault(e.Id))
            .ThenByDescending(e => Stat(e, 1))
            .ThenByDescending(e => Stat(e, 3))
            .ThenBy(e => e.IsNpc)
            .ThenBy(e => e.Id);

    public static int? TravellerTacticsLevel(SceneCombatant entry) =>
        entry.MilitaryTactics is int military ? Math.Clamp(military, 0, 5)
        : entry.Tactics.HasValue ? 0
        : null;

    public static (IReadOnlyDictionary<int, int> Initiative, int Tactics) RollTravellers(
        IEnumerable<SceneCombatant> entries,
        bool aware,
        Func<int>? dice = null
    )
    {
        dice ??= () => Random.Shared.Next(1, 7) + Random.Shared.Next(1, 7);
        var travellers = entries.Where(e => !e.IsNpc).ToArray();
        var rolls = travellers.ToDictionary(
            e => e.Id,
            e => dice() + Dm(Math.Max(Stat(e, 1), Stat(e, 3)))
        );
        var leader = travellers
            .Where(e => TravellerTacticsLevel(e).HasValue)
            .OrderByDescending(e =>
                TravellerTacticsLevel(e)!.Value + Dm(Math.Max(Stat(e, 3), Stat(e, 4)))
            )
            .FirstOrDefault();
        var tactics =
            aware && leader is not null
                ? dice()
                    + TravellerTacticsLevel(leader)!.Value
                    + Dm(Math.Max(Stat(leader, 3), Stat(leader, 4)))
                    - 8
                : 0;
        return (rolls, tactics);
    }

    public void Begin(
        IReadOnlyList<SceneCombatant> entries,
        bool enemyAware,
        bool travellersAware,
        int tactics,
        IReadOnlyDictionary<int, int> rolls,
        Func<int>? dice = null
    )
    {
        dice ??= () => Random.Shared.Next(1, 7) + Random.Shared.Next(1, 7);
        Initiative.Clear();
        ambush.Clear();
        ResetTurnActions();
        var leader = entries
            .Where(e => e.IsNpc && TravellerTacticsLevel(e).HasValue)
            .OrderByDescending(e => TravellerTacticsLevel(e) + Dm(Math.Max(Stat(e, 3), Stat(e, 4))))
            .FirstOrDefault();
        EnemyTactics =
            enemyAware && leader is not null
                ? dice()
                    + TravellerTacticsLevel(leader)!.Value
                    + Dm(Math.Max(Stat(leader, 3), Stat(leader, 4)))
                    - 8
                : 0;
        foreach (var e in entries)
        {
            var aware = e.IsNpc ? enemyAware : travellersAware;
            ambush[e.Id] =
                enemyAware == travellersAware ? 0
                : aware ? 6
                : -6;
            var roll = e.IsNpc
                ? dice() + Dm(Math.Max(Stat(e, 1), Stat(e, 3)))
                : rolls.GetValueOrDefault(e.Id);
            Initiative[e.Id] =
                roll
                + ambush[e.Id]
                + (
                    e.IsNpc ? EnemyTactics
                    : travellersAware ? tactics
                    : 0
                );
        }
        Active = true;
        Round = 1;
        SelectedId = Order(entries).FirstOrDefault(e => !IsDead(e))?.Id;
        foreach (var entry in entries)
            EnsureTurnOrigin(entry.Id, entries, true);
    }

    public void Next(IReadOnlyList<SceneCombatant> entries)
    {
        ClearMovement(SelectedId);
        if (SelectedId is int completed)
            actions[completed] = ActionFor(completed) with { ReactionCount = 0 };
        var order = Order(entries).ToList();
        if (!order.Any(e => !IsDead(e)))
        {
            SelectedId = null;
            return;
        }
        if (SelectedId is null)
        {
            SelectedId = order.First(e => !IsDead(e)).Id;
            CaptureSelectedOrigin(entries, true);
            return;
        }
        var index = order.FindIndex(e => e.Id == SelectedId);
        var next = order.Skip(index + 1).FirstOrDefault(e => !IsDead(e));
        if (next is not null)
        {
            SelectedId = next.Id;
            CaptureSelectedOrigin(entries, true);
            return;
        }
        if (Round == 1)
            foreach (var id in Initiative.Keys)
                Initiative[id] -= ambush.GetValueOrDefault(id);
        Round++;
        SelectedId = Order(entries).First(e => !IsDead(e)).Id;
        CaptureSelectedOrigin(entries, true);
    }

    public IReadOnlyList<PlayerCombatant> PlayerTracker(IReadOnlyList<SceneCombatant> entries)
    {
        IEnumerable<SceneCombatant> ordered = Active ? Order(entries) : entries;
        return ordered
            .Where(e => e.IsVisible)
            .Select(e => new PlayerCombatant(
                e.Id,
                e.CharacterId,
                e.Name,
                e.IsNpc,
                e.HasTokenImage,
                true,
                Active ? Initiative.GetValueOrDefault(e.Id) : null,
                HealthWidth(e),
                HealthColour(e),
                IsDead(e),
                SelectedId == e.Id && !IsDead(e),
                e.IsNpc ? null : Hp(e),
                e.IsNpc ? null : Health(e).Strength,
                e.IsNpc ? null : Health(e).Dexterity,
                e.IsNpc ? null : Health(e).Endurance,
                e.IsNpc ? null : Stat(e, 0),
                e.IsNpc ? null : Stat(e, 1),
                e.IsNpc ? null : Stat(e, 2)
            ))
            .ToArray();
    }

    public EncounterSnapshot Snapshot() =>
        new(
            Active,
            Round,
            SelectedId,
            EnemyTactics,
            new(Initiative),
            null,
            new(ambush),
            new(attributes),
            new(actions)
        );

    public void Restore(EncounterSnapshot state, IReadOnlyList<SceneCombatant> entries)
    {
        var ids = entries.Select(e => e.Id).ToHashSet();
        actions.Clear();
        foreach (var pair in state.Actions ?? [])
            if (ids.Contains(pair.Key))
                actions[pair.Key] = NormalizeActions(pair.Value) with
                {
                    TargetId =
                        pair.Value.TargetId is int target && ids.Contains(target) ? target : null,
                    TargetTagged =
                        pair.Value.TargetTagged
                        && pair.Value.TargetId is int taggedTarget
                        && ids.Contains(taggedTarget),
                };
        Initiative.Clear();
        attributes.Clear();
        ambush.Clear();
        foreach (var pair in state.Initiative.Where(p => ids.Contains(p.Key)))
            Initiative[pair.Key] = pair.Value;
        foreach (var entry in entries)
        {
            if (state.Attributes?.TryGetValue(entry.Id, out var health) == true)
                attributes[entry.Id] = health;
            else if (state.HitPoints?.TryGetValue(entry.Id, out var hp) == true)
                attributes[entry.Id] = MaximumHealth(entry).WithTotal(hp, MaximumHealth(entry));
        }
        foreach (var pair in state.Ambush.Where(p => ids.Contains(p.Key)))
            ambush[pair.Key] = pair.Value;
        Active = state.Active;
        Round = state.Active ? Math.Max(1, state.Round) : 0;
        EnemyTactics = state.EnemyTactics;
        selectedId =
            state.SelectedId is int id && entries.Any(e => e.Id == id && (!Active || !IsDead(e)))
                ? id
            : Active ? Order(entries).FirstOrDefault(e => !IsDead(e))?.Id
            : null;
        if (Active)
            foreach (var entry in entries)
                EnsureTurnOrigin(entry.Id, entries);
    }

    public void Exit()
    {
        ResetTurnActions();
        Active = false;
        Round = 0;
        SelectedId = null;
        Initiative.Clear();
        ambush.Clear();
    }
}
