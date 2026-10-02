using Content.Server.Roles;
using System.Linq;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Flock.Components;
using Content.Shared._Starlight.Flock.Roles;
using Content.Shared.Actions;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Station.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;
using System.Numerics;

namespace Content.Server._Starlight.Flock.Rules;

/// <summary>
/// Drives the Flockmind antag: location, briefing, second chance, death, relay consequences and round end text.
/// </summary>
public sealed partial class FlockRuleSystem : GameRuleSystem<FlockRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private FlockSystem _flock = default!;
    [Dependency] private FlockmindSystem _flockmind = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private StationSystem _station = default!;

    protected override void Added(EntityUid uid, FlockRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        // Appear above the middle of the station: the flockmind is incorporeal and picks its own landing spot.
        var q = EntityQueryEnumerator<StationDataComponent>();
        while (q.MoveNext(out var station, out _))
        {
            var grid = _station.GetLargestGrid(station);
            if (grid == null || !TryComp<MapGridComponent>(grid, out var gc))
                continue;
            var center = gc.LocalAABB.Center;
            comp.Coords = new MapCoordinates(
                Vector2.Transform(center, _xform.GetWorldMatrix(grid.Value)),
                Transform(grid.Value).MapID);
            return;
        }
        ForceEndSelf(uid, gameRule);
    }

    [SubscribeLocalEvent]
    private static void OnSelectLocation(Entity<FlockRuleComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (ent.Comp.Coords is { } c)
            args.Coordinates.Add(c);
    }

    [SubscribeLocalEvent]
    private void OnAfterSelected(Entity<FlockRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (_flock.TryGetFlock(args.EntityUid, out var flock))
            ent.Comp.Flock = flock;
        _antag.SendBriefing(args.EntityUid, Loc.GetString("flock-role-briefing"), null, null);
    }

    [SubscribeLocalEvent]
    private void OnBriefing(Entity<FlockmindRoleComponent> ent, ref GetBriefingEvent args)
        => args.Append(Loc.GetString("flock-role-briefing"));

    // ---------------- flock collapse / second chance ----------------

    [SubscribeLocalEvent]
    private void OnCollapsed(ref FlockCollapsedEvent args)
    {
        if (!TryComp<FlockComponent>(args.Flock, out var flock) || flock.Flockmind is not { } mind)
            return;

        // goon: if the flock is wiped before it really got going, the flockmind gets one more attempt.
        if (!flock.SecondChanceUsed && !flock.EverReachedSecondChanceCompute && TryComp<FlockmindComponent>(mind, out var fm))
        {
            flock.SecondChanceUsed = true;
            flock.Started = false;
            fm.RiftPlaced = false;
            foreach (var a in fm.ActionEntities)
                _actions.RemoveAction(a);
            fm.ActionEntities.Clear();
            EntityUid? act = null;
            _actions.AddAction(mind, ref act, fm.RiftAction);
            fm.RiftActionEntity = act;
            Dirty(mind, fm);
            _popup.PopupEntity(Loc.GetString("flock-second-chance"), mind, mind, PopupType.LargeCaution);
            return;
        }
        KillFlock((args.Flock, flock), "flock-flockmind-died-collapse");
    }

    [SubscribeLocalEvent]
    private void OnRelayDestroyed(ref FlockRelayDestroyedEvent args)
    {
        if (TryComp<FlockComponent>(args.Flock, out var flock))
            KillFlock((args.Flock, flock), "flock-flockmind-died-relay");
    }

    [SubscribeLocalEvent]
    private void OnRelayFired(ref FlockRelayFiredEvent args)
    {
        var q = EntityQueryEnumerator<FlockRuleComponent>();
        while (q.MoveNext(out _, out var rule))
            rule.Won = true;
    }

    /// <summary>The flockmind dies and takes the flock with it.</summary>
    public void KillFlock(Entity<FlockComponent> flock, string _)
    {
        foreach (var t in flock.Comp.Traces.ToList())
            _flockmind.ReleaseTrace(t);

        if (flock.Comp.Flockmind is { } mind && Exists(mind))
        {
            if (_mind.TryGetMind(mind, out var mid, out var mc))
                _ghost.OnGhostAttempt(mid, false, forced: true, mind: mc);
            QueueDel(mind);
        }
        // drones and bits shut down on the spot
        foreach (var d in flock.Comp.Drones.Concat(flock.Comp.Bits).ToList())
        {
            if (Exists(d))
            {
                Spawn("FlockDroneDebris", Transform(d).Coordinates);
                QueueDel(d);
            }
        }
        flock.Comp.Flockmind = null;
        _flock.DestroyFlock(flock);
    }

    // ---------------- round end ----------------

    protected override void AppendRoundEndText(EntityUid uid, FlockRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);
        args.AddLine(Loc.GetString(component.Won ? "flock-roundend-won" : "flock-roundend-lost"));
        var q = EntityQueryEnumerator<FlockComponent>();
        while (q.MoveNext(out _, out var f))
        {
            args.AddLine(Loc.GetString("flock-roundend-stats",
                ("drones", f.StatDronesMade), ("bits", f.StatBitsMade), ("structures", f.StatStructuresMade),
                ("tiles", f.StatTilesConverted), ("resources", f.StatResourcesGained),
                ("caged", f.StatHumansCaged), ("lost", f.StatDronesLost)));
        }
    }
}
