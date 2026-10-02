using Content.Shared._Starlight.Flock.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Mind;
using Content.Server.Objectives.Systems;

namespace Content.Server._Starlight.Flock.Objectives;

/// <summary>Flockmind must get the relay to fire.</summary>
[RegisterComponent]
public sealed partial class FlockRelayConditionComponent : Component;

/// <summary>Flockmind must convert a number of tiles (NumberObjective gives the target).</summary>
[RegisterComponent]
public sealed partial class FlockTilesConditionComponent : Component;

/// <summary>Flockmind must still be alive (flock not collapsed) at the end of the round.</summary>
[RegisterComponent]
public sealed partial class FlockSurviveConditionComponent : Component;

public sealed partial class FlockObjectivesSystem : EntitySystem
{
    [Dependency] private FlockSystem _flock = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private NumberObjectiveSystem _number = default!;

    private bool TryFlock(EntityUid mindId, MindComponent mind, out Entity<FlockComponent> flock)
    {
        flock = default;
        if (mind.OwnedEntity is { } owned && _flock.TryGetFlock(owned, out flock))
            return true;
        // flockmind may be possessing a drone; search all flocks for one whose flockmind holds this mind
        var q = EntityQueryEnumerator<FlockComponent>();
        while (q.MoveNext(out var uid, out var f))
        {
            if (f.Flockmind is { } fm && _mind.TryGetMind(fm, out var m, out _) && m == mindId)
            {
                flock = (uid, f);
                return true;
            }
        }
        return false;
    }

    [SubscribeLocalEvent]
    private void OnRelay(Entity<FlockRelayConditionComponent> ent, ref ObjectiveGetProgressEvent args)
        => args.Progress = TryFlock(args.MindId, args.Mind, out var f)
            && f.Comp.RelayFinished ? 1f : 0f;

    [SubscribeLocalEvent]
    private void OnTiles(Entity<FlockTilesConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var target = _number.GetTarget(ent);
        if (target <= 0 || !TryFlock(args.MindId, args.Mind, out var f))
        {
            args.Progress = 0f;
            return;
        }
        args.Progress = Math.Min(1f, (float) f.Comp.StatTilesConverted / target);
    }

    [SubscribeLocalEvent]
    private void OnSurvive(Entity<FlockSurviveConditionComponent> ent, ref ObjectiveGetProgressEvent args)
        => args.Progress = TryFlock(args.MindId, args.Mind, out var f)
            && f.Comp.Flockmind != null
            && !TerminatingOrDeleted(f.Comp.Flockmind) ? 1f : 0f;
}
