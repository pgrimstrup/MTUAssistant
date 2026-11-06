using Microsoft.JSInterop;
using MTU.Core;

namespace MTUAssistant.Components.Pages;

public partial class Battlemap
{
    private (int Actor, int Round, int Count)? mapTargetTurn;
    private int nextMapTargetSlot;

    private sealed record MapTargetContext(int ActorId, int Round, int Slot, int[] TargetIds);

    private MapTargetContext? MapTargetingContext()
    {
        if (
            !encounter.Active
            || SelectedActor is not { } actor
            || !actor.OnMap
            || encounter.IsDead(actor)
        )
            return null;
        var state = encounter.ActionFor(actor.Id);
        if (
            state.ActionsResolved
            || !(state.IsSelected("Aim") || state.MajorAction is "Ranged Attack" or "Melee Attack")
        )
            return null;
        var slots = state.AttackTargets;
        var turn = (actor.Id, encounter.Round, slots.Count);
        if (mapTargetTurn != turn)
        {
            mapTargetTurn = turn;
            nextMapTargetSlot = 0;
        }
        var slot = Enumerable
            .Range(0, slots.Count)
            .FirstOrDefault(i => slots[i].TargetId is null, -1);
        if (slot < 0)
            slot = nextMapTargetSlot % slots.Count;
        var targets = entries
            .Where(e =>
                e.OnMap
                && e.Id != actor.Id
                && (
                    !state.FullAuto
                    || !slots.Where((_, i) => i != slot).Any(t => t.TargetId == e.Id)
                )
                && (
                    slot == 0
                    || Encounter.WithinAutoTargetRange(
                        entries.FirstOrDefault(e => e.Id == state.TargetId),
                        e
                    )
                )
            )
            .Select(e => e.Id)
            .ToArray();
        return new(actor.Id, encounter.Round, slot, targets);
    }

    [JSInvokable]
    public Task SelectMapTarget(int actorId, int round, int slot, int targetId) =>
        InvokeAsync(async () =>
        {
            if (
                disposed
                || busy
                || MapTargetingContext() is not { } context
                || context.ActorId != actorId
                || context.Round != round
                || context.Slot != slot
                || !context.TargetIds.Contains(targetId)
            )
                return;
            await ChangeEncounter(() =>
            {
                encounter.SetAttackTarget(actorId, slot, targetId, entries);
                nextMapTargetSlot = (slot + 1) % encounter.ActionFor(actorId).AttackTargets.Count;
            });
            if (!disposed)
                StateHasChanged();
        });
}
