using System.Text.Json.Serialization;

namespace MTU.Core;

public sealed record CombatantActionState(
    string? Action = null,
    int? TargetId = null,
    string Stance = "Standing",
    double? MovementDiameter = null,
    int? RangeColumn = null,
    int? RangeRow = null,
    string RangeColour = "#87cefa",
    IReadOnlyList<string>? MinorActions = null,
    string? MajorAction = null,
    int AimSelections = 0,
    int MoveSelections = 0,
    int CarriedAimBonus = 0,
    IReadOnlyList<string>? BeforeMajorActions = null,
    IReadOnlyList<string>? AfterMajorActions = null,
    int? TurnOriginColumn = null,
    int? TurnOriginRow = null,
    string TargetReaction = "None",
    int ReactionCount = 0,
    int? ReactionColumn = null,
    int? ReactionRow = null,
    string? PendingStance = null,
    string? TargetReactionStance = null,
    bool ActionsResolved = false,
    int? ResolvedAimBonus = null,
    bool BehindCover = false,
    bool Hiding = false,
    bool TargetTagged = false,
    bool? PendingBehindCover = null,
    bool? PendingHiding = null,
    bool Elevated = false,
    bool? PendingElevated = null,
    bool TargetHasBeenHit = false,
    AttackModifierSummary? ResolvedAttackModifiers = null,
    CombatCover? CoverProtection = null,
    CombatCover? PendingCoverProtection = null,
    bool HasPendingCoverProtection = false,
    AttackOutcome? AttackOutcome = null,
    bool Unconscious = false,
    int Speed = 6,
    bool AutomaticMove = false
)
{
    public IReadOnlyList<string> ColumnActions(int column) =>
        column == 3
            ? AfterMajorActions ?? []
            : BeforeMajorActions
                ?? (MinorActions ?? [])
                    .Concat(Enumerable.Repeat("Aim", AimSelections))
                    .Concat(Enumerable.Repeat("Move", MoveSelections))
                    .ToArray();

    private static int AimAfter(int bonus, IEnumerable<string> sequence)
    {
        foreach (var action in sequence)
            bonus = action == "Aim" ? Math.Min(6, bonus + 1) : 0;
        return bonus;
    }

    [JsonIgnore]
    public int AimBeforeMajor => AimAfter(CarriedAimBonus, ColumnActions(1));

    [JsonIgnore]
    public int RangedAimBonus => MajorAction == "Ranged Attack" ? AimBeforeMajor : 0;

    [JsonIgnore]
    public int AimBonus =>
        AimAfter(
            AimAfter(AimBeforeMajor, MajorAction is null ? [] : new[] { MajorAction }),
            ColumnActions(3)
        );

    [JsonIgnore]
    public int MinorCount => ColumnActions(1).Count + ColumnActions(3).Count;

    public bool IsSelected(string action) =>
        MajorAction == action
        || ColumnActions(1).Contains(action)
        || ColumnActions(3).Contains(action);

    public bool IsSelected(string action, int column) =>
        column == 2 ? MajorAction == action : ColumnActions(column).Contains(action);

    public string ButtonText(string action, int column = 1) =>
        action switch
        {
            "Aim" => $"Aim x{ColumnActions(column).Count(a => a == "Aim")}",
            "Move" when ColumnActions(column).Count(a => a == "Move") > 0 =>
                $"Move {ColumnActions(column).Count(a => a == "Move")}",
            _ => action,
        };

    [JsonIgnore]
    public bool NeedsTarget =>
        IsSelected("Aim")
        || IsSelected("Tag Target")
        || MajorAction is "Melee Attack" or "Ranged Attack";

    [JsonIgnore]
    public bool NeedsStance => IsSelected("Change Stance");

    [JsonIgnore]
    public bool NeedsReaction => MajorAction is "Melee Attack" or "Ranged Attack";

    [JsonIgnore]
    public bool IsDive => TargetReaction is "Dive for Cover" or "Dive for Prone";

    [JsonIgnore]
    public string SelectedStance => PendingStance ?? Stance;

    [JsonIgnore]
    public bool SelectedBehindCover => PendingBehindCover ?? BehindCover;

    [JsonIgnore]
    public bool SelectedHiding => PendingHiding ?? Hiding;

    [JsonIgnore]
    public bool HasPendingStatus =>
        PendingBehindCover.HasValue
        || PendingHiding.HasValue
        || PendingElevated.HasValue
        || HasPendingCoverProtection;

    [JsonIgnore]
    public CombatCover? SelectedCoverProtection =>
        HasPendingCoverProtection ? PendingCoverProtection : CoverProtection;

    [JsonIgnore]
    public int EffectiveCoverProtection =>
        SelectedHiding ? SelectedCoverProtection?.Protection ?? 0 : 0;

    [JsonIgnore]
    public bool SelectedElevated => PendingElevated ?? Elevated;

    [JsonIgnore]
    public string MinorSummary => string.Join(" → ", ColumnActions(1).Concat(ColumnActions(3)));

    [JsonIgnore]
    public string SequenceSummary =>
        string.Join(
            " → ",
            ColumnActions(1)
                .Concat(MajorAction is null ? [] : new[] { MajorAction })
                .Concat(ColumnActions(3))
        );
}

public sealed record CombatMovementRange(
    int Id,
    int Column,
    int Row,
    double Diameter,
    string Colour,
    bool IsVisible
);

public static class CombatActionChoices
{
    public static readonly string[] Minor =
    [
        "Aim",
        "Change Stance",
        "Draw/Holster",
        "Reload (fast)",
        "Move",
        "Other",
        "Tag Target",
    ];
    public static readonly string[] Major =
    [
        "Melee Attack",
        "Ranged Attack",
        "Leadership",
        "Reload (slow)",
        "Skill Check",
    ];

    public static bool UnavailableWhileHiding(string action) =>
        action is "Melee Attack" or "Ranged Attack" or "Aim" or "Tag Target";

    public static readonly string[] Stances = ["Standing", "Crouching", "Prone"];
}

public sealed partial class Encounter
{
    private readonly Dictionary<int, CombatantActionState> actions = [];

    private static CombatantActionState NormalizeActions(CombatantActionState state)
    {
        if (state.BeforeMajorActions is null)
        {
            if (state.MinorActions is null)
            {
                if (state.Action == "Aim")
                    state = state with { MinorActions = [], AimSelections = 1 };
                else if (state.Action == "Move")
                    state = state with
                    {
                        MinorActions = [],
                        MoveSelections = state.MovementDiameter switch
                        {
                            13.5 => 1,
                            25.5 => 2,
                            _ => 3,
                        },
                        MovementDiameter = state.MovementDiameter ?? 37.5,
                    };
                else if (CombatActionChoices.Minor.Contains(state.Action))
                    state = state with { MinorActions = [state.Action!] };
                else if (CombatActionChoices.Major.Contains(state.Action))
                    state = state with { MinorActions = [], MajorAction = state.Action };
                else
                    state = state with
                    {
                        MinorActions = [],
                        Action = null,
                        MovementDiameter = null,
                        RangeColumn = null,
                        RangeRow = null,
                    };
            }
            state = state with
            {
                BeforeMajorActions = state.ColumnActions(1).ToArray(),
                AfterMajorActions = state.AfterMajorActions ?? [],
            };
        }
        if (state.MajorAction is not null && state.MinorCount > 1)
        {
            var before = state.ColumnActions(1).Take(1).ToArray();
            var after = before.Length == 0 ? state.ColumnActions(3).Take(1).ToArray() : [];
            var single = before.Concat(after).ToArray();
            state = state with
            {
                BeforeMajorActions = before,
                AfterMajorActions = after,
                MinorActions = single.Where(a => a is not ("Aim" or "Move")).ToArray(),
                AimSelections = single.Count(a => a == "Aim"),
                MoveSelections = single.Count(a => a == "Move"),
            };
            if (!state.NeedsStance)
                state = state with { PendingStance = null };
        }
        return state with { MovementDiameter = MovementDiameterFor(state) };
    }

    private static double? MovementDiameterFor(CombatantActionState state)
    {
        var moves = state.ColumnActions(1).Concat(state.ColumnActions(3)).Count(a => a == "Move");
        var distance = state.SelectedStance switch
        {
            "Crouching" => (double)state.Speed,
            "Prone" => state.Speed / 2.0,
            _ => state.Speed * 2.0,
        };
        return moves == 0 ? null : 1.5 + distance * Math.Min(3, moves);
    }

    public void ResetActions(int id, IReadOnlyList<SceneCombatant> entries)
    {
        RequireActor(id, entries);
        var state = ReleaseTargetReaction(ActionFor(id));
        actions[id] = new CombatantActionState(
            TargetId: state.TargetId,
            Stance: state.Stance,
            CarriedAimBonus: state.ResolvedAimBonus ?? state.CarriedAimBonus,
            TurnOriginColumn: state.TurnOriginColumn,
            TurnOriginRow: state.TurnOriginRow,
            ReactionCount: state.ReactionCount,
            BehindCover: state.BehindCover,
            Hiding: state.Hiding,
            TargetTagged: state.TargetTagged,
            Elevated: state.Elevated,
            Unconscious: state.Unconscious,
            Speed: state.Speed,
            CoverProtection: state.CoverProtection,
            TargetHasBeenHit: state.TargetHasBeenHit
        );
    }

    public CombatantActionState ActionFor(int id) =>
        NormalizeActions(actions.GetValueOrDefault(id) ?? new());

    private SceneCombatant RequireActor(int id, IReadOnlyList<SceneCombatant> entries)
    {
        var actor =
            entries.FirstOrDefault(e => e.Id == id)
            ?? throw new ArgumentException("Select a combatant in this scene.");
        if (!Active || IsDead(actor))
            throw new ArgumentException("Choose a living combatant during an encounter.");
        actions[id] = ActionFor(id) with { Speed = actor.Speed };
        return actor;
    }

    public bool CanChooseAction(int id, string action, bool smartTracker = false)
    {
        var state = ActionFor(id);
        if (state.ActionsResolved)
            return false;
        if (action == "Tag Target" && (!smartTracker || !state.TargetHasBeenHit))
            return false;
        if (state.SelectedHiding && CombatActionChoices.UnavailableWhileHiding(action))
            return false;
        if (CombatActionChoices.Major.Contains(action))
            return state.MinorCount < 2;
        return CombatActionChoices.Minor.Contains(action);
    }

    public void ChooseAction(
        int id,
        string action,
        IReadOnlyList<SceneCombatant> entries,
        int column = 1,
        bool smartTracker = false
    )
    {
        var actor = RequireActor(id, entries);
        RequireUnresolved(id);
        EnsureTurnOrigin(id, entries);
        if (!CombatActionChoices.Minor.Concat(CombatActionChoices.Major).Contains(action))
            throw new ArgumentException("Choose an available action.");
        if (action == "Tag Target" && (!smartTracker || !ActionFor(id).TargetHasBeenHit))
            throw new ArgumentException(
                "Tag Target requires Smart Tracker and a target that has been hit."
            );
        if (ActionFor(id).SelectedHiding && CombatActionChoices.UnavailableWhileHiding(action))
            throw new ArgumentException(
                "Stop Hiding before attacking, aiming or tagging a target."
            );
        if (!CanChooseAction(id, action, smartTracker))
            throw new ArgumentException(
                "Major Actions are unavailable while two or more Minor Actions are selected."
            );
        var state = ActionFor(id);
        if (CombatActionChoices.Major.Contains(action))
            state = state with { MajorAction = state.MajorAction == action ? null : action };
        else
        {
            if (column is not (1 or 3))
                throw new ArgumentException("Choose a Minor Action in column 1 or 3.");
            var selected = state.ColumnActions(column).ToList();
            var count = selected.Count(a => a == action);
            var index = selected.IndexOf(action);
            if (index < 0)
                index = selected.Count;
            selected.RemoveAll(a => a == action);
            var next = state.MajorAction is not null
                ? count == 0
                    ? 1
                    : 0
                : action is "Aim" or "Move"
                    ? (count + 1) % 4
                    : count == 0
                        ? 1
                        : 0;
            if (state.MajorAction is not null)
            {
                selected.Clear();
                index = 0;
                state = state with { BeforeMajorActions = [], AfterMajorActions = [] };
            }
            selected.InsertRange(Math.Min(index, selected.Count), Enumerable.Repeat(action, next));
            state =
                column == 1
                    ? state with
                    {
                        BeforeMajorActions = selected.ToArray(),
                    }
                    : state with
                    {
                        AfterMajorActions = selected.ToArray(),
                    };
        }
        state = state with { AutomaticMove = false };
        var sequence = state.ColumnActions(1).Concat(state.ColumnActions(3)).ToArray();
        var moves = sequence.Count(a => a == "Move");
        state = state with
        {
            MinorActions = sequence.Where(a => a is not ("Aim" or "Move")).ToArray(),
            AimSelections = sequence.Count(a => a == "Aim"),
            MoveSelections = moves,
            MovementDiameter = MovementDiameterFor(state),
            RangeColumn =
                moves == 0 ? null : state.TurnOriginColumn ?? state.RangeColumn ?? actor.Column,
            RangeRow = moves == 0 ? null : state.TurnOriginRow ?? state.RangeRow ?? actor.Row,
            Action = action,
        };
        if (!state.NeedsReaction)
            state = ReleaseTargetReaction(state);
        if (!state.NeedsStance)
            state = state with
            {
                PendingStance = null,
                MovementDiameter = MovementDiameterFor(state with { PendingStance = null }),
            };
        actions[id] = state;
    }

    public void SetTarget(int id, int? targetId, IReadOnlyList<SceneCombatant> entries)
    {
        var actor = RequireActor(id, entries);
        RequireUnresolved(id);
        EnsureTurnOrigin(id, entries);
        if (targetId is int target && !entries.Any(e => e.Id == target && e.IsNpc != actor.IsNpc))
            throw new ArgumentException(
                "Choose a combatant from the opposing side, or clear the target."
            );
        var state = ActionFor(id);
        if (state.TargetId != targetId)
            state = ReleaseTargetReaction(state);
        actions[id] = state with
        {
            TargetId = targetId,
            TargetTagged = state.TargetId == targetId && state.TargetTagged,
            TargetHasBeenHit = state.TargetId == targetId && state.TargetHasBeenHit,
        };
    }

    public void SetStance(int id, string stance, IReadOnlyList<SceneCombatant> entries)
    {
        RequireActor(id, entries);
        RequireUnresolved(id);
        if (!CombatActionChoices.Stances.Contains(stance))
            throw new ArgumentException("Choose Standing, Crouching or Prone.");
        var state = ActionFor(id) with { PendingStance = stance };
        actions[id] = state with { MovementDiameter = MovementDiameterFor(state) };
    }

    public bool EnsureTurnOrigin(int id, IReadOnlyList<SceneCombatant> entries, bool reset = false)
    {
        if (!Active || entries.FirstOrDefault(e => e.Id == id) is not { OnMap: true } actor)
            return false;
        var state = ActionFor(id) with { Speed = actor.Speed };
        actions[id] = state;
        if (!reset && state.TurnOriginColumn.HasValue && state.TurnOriginRow.HasValue)
            return false;
        actions[id] = state with
        {
            TurnOriginColumn = reset ? actor.Column : state.RangeColumn ?? actor.Column,
            TurnOriginRow = reset ? actor.Row : state.RangeRow ?? actor.Row,
        };
        return true;
    }

    public void SelectCombatant(int id, IReadOnlyList<SceneCombatant> entries)
    {
        if (!entries.Any(e => e.Id == id))
            throw new ArgumentException("Choose a combatant in this scene.");
        if (SelectedId == id)
            return;
        SelectedId = id;
        EnsureTurnOrigin(id, entries, true);
    }

    private void CaptureSelectedOrigin(IReadOnlyList<SceneCombatant> entries, bool reset = false)
    {
        if (SelectedId is int id)
            EnsureTurnOrigin(id, entries, reset);
    }

    public bool AnchorMovement(int id, int column, int row)
    {
        if (!Active)
            return false;
        var state = ActionFor(id);
        var updated = state with
        {
            TurnOriginColumn = state.TurnOriginColumn ?? column,
            TurnOriginRow = state.TurnOriginRow ?? row,
        };
        if (
            updated.MovementDiameter.HasValue
            && !updated.RangeColumn.HasValue
            && !updated.RangeRow.HasValue
        )
            updated = updated with
            {
                RangeColumn = updated.TurnOriginColumn,
                RangeRow = updated.TurnOriginRow,
            };
        if (updated == state)
            return false;
        actions[id] = updated;
        return true;
    }

    private static CombatantActionState EndTurn(CombatantActionState state, bool carryAim) =>
        state with
        {
            Action = null,
            MinorActions = [],
            BeforeMajorActions = [],
            AfterMajorActions = [],
            MajorAction = null,
            AimSelections = 0,
            MoveSelections = 0,
            CarriedAimBonus = carryAim ? state.ResolvedAimBonus ?? state.CarriedAimBonus : 0,
            MovementDiameter = null,
            RangeColumn = null,
            RangeRow = null,
            TurnOriginColumn = null,
            TurnOriginRow = null,
            TargetReaction = "None",
            ReactionColumn = null,
            ReactionRow = null,
            PendingStance = null,
            TargetReactionStance = null,
            ActionsResolved = false,
            ResolvedAimBonus = null,
            PendingBehindCover = null,
            PendingHiding = null,
            PendingElevated = null,
            PendingCoverProtection = null,
            HasPendingCoverProtection = false,
            ResolvedAttackModifiers = null,
            AttackOutcome = null,
            AutomaticMove = false,
        };

    private void ClearMovement(int? id)
    {
        if (id is int actor && actions.ContainsKey(actor))
            actions[actor] = EndTurn(ActionFor(actor), true);
    }

    private void ResetTurnActions()
    {
        foreach (var id in actions.Keys)
            actions[id] = EndTurn(ActionFor(id), false) with { ReactionCount = 0 };
    }

    public IReadOnlyList<CombatMovementRange> MovementRanges(IReadOnlyList<SceneCombatant> entries)
    {
        foreach (var entry in entries)
            if (actions.TryGetValue(entry.Id, out var state) && state.Speed != entry.Speed)
                actions[entry.Id] = NormalizeActions(state with { Speed = entry.Speed });
        return !Active
            ? []
            : entries
                .Where(e =>
                    e.OnMap
                    && ActionFor(e.Id).MovementDiameter.HasValue
                    && ActionFor(e.Id).RangeColumn.HasValue
                    && ActionFor(e.Id).RangeRow.HasValue
                )
                .Select(e =>
                {
                    var state = ActionFor(e.Id);
                    return new CombatMovementRange(
                        e.Id,
                        state.RangeColumn!.Value,
                        state.RangeRow!.Value,
                        state.MovementDiameter!.Value,
                        state.RangeColour,
                        e.IsVisible
                    );
                })
                .Concat(TargetReactionRanges(entries))
                .ToArray();
    }
}
