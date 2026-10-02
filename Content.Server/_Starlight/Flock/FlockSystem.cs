using Content.Shared._Starlight.Flock;
using Content.Shared._Starlight.Flock.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Flock;

/// <summary>Raised (broadcast) when a flock has no drones, eggs or rifts left.</summary>
[ByRefEvent]
public record struct FlockCollapsedEvent(EntityUid Flock);

/// <summary>
/// Owns all flock-wide bookkeeping: membership, compute, egg cost, unlocks, enemies, panel data.
/// This is the port of goon's /datum/flock.
/// </summary>
public sealed partial class FlockSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private TimeSpan _nextUpdate;
    private static readonly TimeSpan _updateDelay = TimeSpan.FromSeconds(2);

    // ---------------- creation / membership ----------------

    public Entity<FlockComponent> CreateFlock(EntityUid flockmind)
    {
        var uid = Spawn("FlockRoot", MapCoordinates.Nullspace);
        var comp = EnsureComp<FlockComponent>(uid);
        comp.Flockmind = flockmind;
        comp.Start = _timing.CurTime;
        _meta.SetEntityName(uid, "Flock");
        AddMember((uid, comp), flockmind);
        return (uid, comp);
    }

    public bool TryGetFlock(EntityUid member, out Entity<FlockComponent> flock)
    {
        flock = default;
        if (!TryComp<FlockMemberComponent>(member, out var m) || m.Flock is not { } f || !TryComp<FlockComponent>(f, out var fc))
            return false;
        flock = (f, fc);
        return true;
    }

    public bool IsSameFlock(EntityUid a, EntityUid b)
        => TryGetFlock(a, out var fa) && TryGetFlock(b, out var fb) && fa.Owner == fb.Owner;

    public void AddMember(Entity<FlockComponent> flock, EntityUid ent)
    {
        var m = EnsureComp<FlockMemberComponent>(ent);
        m.Flock = flock;
        Dirty(ent, m);
        if (HasComp<FlockDroneComponent>(ent)) flock.Comp.Drones.Add(ent);
        if (HasComp<FlockBitComponent>(ent)) flock.Comp.Bits.Add(ent);
        if (HasComp<FlocktraceComponent>(ent)) flock.Comp.Traces.Add(ent);
        if (HasComp<FlockStructureComponent>(ent)) flock.Comp.Structures.Add(ent);
        if (HasComp<FlockTealprintComponent>(ent)) flock.Comp.Tealprints.Add(ent);
        UpdateEggCost(flock);
    }

    [SubscribeLocalEvent]
    private void OnMemberShutdown(Entity<FlockMemberComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Flock is not { } f || !TryComp<FlockComponent>(f, out var fc))
            return;
        fc.Drones.Remove(ent);
        fc.Bits.Remove(ent);
        fc.Traces.Remove(ent);
        fc.Structures.Remove(ent);
        fc.Tealprints.Remove(ent);
        fc.Enemies.Remove(ent);
        fc.Ignored.Remove(ent);
        fc.DeconstructMarks.Remove(ent);
    }

    public void DestroyFlock(Entity<FlockComponent> flock)
    {
        foreach (var e in new List<EntityUid>(flock.Comp.Enemies))
            RemComp<FlockEnemyComponent>(e);
        foreach (var e in new List<EntityUid>(flock.Comp.Ignored))
            RemComp<FlockIgnoredComponent>(e);
        QueueDel(flock);
    }

    // ---------------- numbers ----------------

    public int GetComplexDroneCount(FlockComponent flock)
    {
        var n = 0;
        foreach (var d in flock.Drones)
            if (TryComp<FlockDroneComponent>(d, out var dc) && !dc.Dead)
                n++;
        return n;
    }

    public void UpdateEggCost(Entity<FlockComponent> flock)
    {
        var eggs = 0;
        foreach (var s in flock.Comp.Structures)
            if (HasComp<FlockEggComponent>(s)) eggs++;
        var raw = FlockConsts.LayEggCost + MathF.Pow(GetComplexDroneCount(flock.Comp) + eggs, 1.4f);
        flock.Comp.CurrentEggCost = (int) (MathF.Round(raw / 10f) * 10f);
    }

    public int AvailableCompute(FlockComponent f) => f.TotalCompute - f.UsedCompute;

    public bool CanAffordCompute(FlockComponent f, int cost) => AvailableCompute(f) >= cost;

    public bool IsUnlocked(FlockComponent f, string structureId) => f.UnlockedStructures.Contains(structureId);

    public bool RelayUnlocked(FlockComponent f) => f.TotalCompute >= FlockConsts.RelayComputeCost && !f.RelayInProgress && !f.RelayFinished;

    // ---------------- enemies / ignore ----------------

    public bool ToggleEnemy(Entity<FlockComponent> flock, EntityUid target)
    {
        if (flock.Comp.Enemies.Remove(target))
        {
            RemComp<FlockEnemyComponent>(target);
            return false;
        }
        flock.Comp.Ignored.Remove(target);
        RemComp<FlockIgnoredComponent>(target);
        flock.Comp.Enemies.Add(target);
        EnsureComp<FlockEnemyComponent>(target).Flock = flock;
        return true;
    }

    public bool ToggleIgnore(Entity<FlockComponent> flock, EntityUid target)
    {
        if (flock.Comp.Ignored.Remove(target))
        {
            RemComp<FlockIgnoredComponent>(target);
            return false;
        }
        flock.Comp.Enemies.Remove(target);
        RemComp<FlockEnemyComponent>(target);
        flock.Comp.Ignored.Add(target);
        EnsureComp<FlockIgnoredComponent>(target).Flock = flock;
        return true;
    }

    public bool IsEnemyOf(EntityUid flockMember, EntityUid target)
    {
        if (!TryGetFlock(flockMember, out var f))
            return false;
        return f.Comp.Enemies.Contains(target);
    }

    /// <summary>Anyone who attacked a flock thing becomes an enemy automatically.</summary>
    public void AutoEnemy(EntityUid flockMember, EntityUid attacker)
    {
        if (!TryGetFlock(flockMember, out var f) || HasComp<FlockMemberComponent>(attacker) || f.Comp.Ignored.Contains(attacker))
            return;
        if (!HasComp<MobStateComponent>(attacker))
            return;
        if (!f.Comp.Enemies.Contains(attacker))
        {
            f.Comp.Enemies.Add(attacker);
            EnsureComp<FlockEnemyComponent>(attacker).Flock = f;
        }
    }

    // ---------------- tiles ----------------

    public void AddFlockTile(Entity<FlockComponent> flock, EntityUid grid, Vector2i idx)
    {
        if (flock.Comp.FlockTiles.Add((grid, idx)))
            flock.Comp.StatTilesConverted++;
        flock.Comp.PriorityTiles.Remove((grid, idx));
    }

    public void RemoveFlockTile(Entity<FlockComponent> flock, EntityUid grid, Vector2i idx)
        => flock.Comp.FlockTiles.Remove((grid, idx));

    // ---------------- ticking ----------------

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + _updateDelay;

        var q = EntityQueryEnumerator<FlockComponent>();
        while (q.MoveNext(out var uid, out var flock))
        {
            Recompute((uid, flock));
            CheckCollapse((uid, flock));
        }
    }

    private void Recompute(Entity<FlockComponent> flock)
    {
        var c = flock.Comp;
        var total = 0;
        foreach (var d in c.Drones)
        {
            if (!TryComp<FlockDroneComponent>(d, out var dc) || dc.Dead)
                continue;
            total += dc.Hibernating ? FlockConsts.DroneComputeHibernate : FlockConsts.DroneCompute;
        }
        foreach (var s in c.Structures)
        {
            if (TryComp<FlockStructureComponent>(s, out var sc) && sc.State == FlockStructureState.Online && sc.Compute > 0)
                total += sc.Compute;
        }
        c.TotalCompute = total;

        var used = c.Traces.Count * FlockConsts.FlocktraceComputeCost;
        // Consumers get switched on greedily in a stable order.
        var consumers = new List<(EntityUid Uid, FlockStructureComponent Comp)>();
        foreach (var s in c.Structures)
            if (TryComp<FlockStructureComponent>(s, out var sc) && sc.OnlineComputeCost > 0 && sc.State != FlockStructureState.Building)
                consumers.Add((s, sc));
        consumers.Sort((a, b) => a.Uid.Id.CompareTo(b.Uid.Id));
        foreach (var (uid, sc) in consumers)
        {
            var ok = used + sc.OnlineComputeCost <= total;
            if (ok) used += sc.OnlineComputeCost;
            if (sc.Powered != ok)
            {
                sc.Powered = ok;
                sc.State = ok ? FlockStructureState.Online : FlockStructureState.Offline;
                Dirty(uid, sc);
            }
        }
        c.UsedCompute = used;

        if (total >= FlockConsts.SecondChanceComputeThreshold)
            c.EverReachedSecondChanceCompute = true;

        // clean up stale tiles (explosions etc)
        c.FlockTiles.RemoveWhere(t => TerminatingOrDeleted(t.Grid));
        UpdateEggCost(flock);
    }

    private void CheckCollapse(Entity<FlockComponent> flock)
    {
        if (!flock.Comp.Started)
            return;
        if (GetComplexDroneCount(flock.Comp) > 0)
            return;
        foreach (var s in flock.Comp.Structures)
            if (HasComp<FlockEggComponent>(s) || HasComp<FlockRiftComponent>(s))
                return;
        var ev = new FlockCollapsedEvent(flock);
        RaiseLocalEvent(ref ev);
    }

    // ---------------- panel ----------------

    public FlockPanelState BuildPanelState(Entity<FlockComponent> flock)
    {
        var c = flock.Comp;
        var st = new FlockPanelState
        {
            TotalCompute = c.TotalCompute,
            UsedCompute = c.UsedCompute,
            FlockTiles = c.FlockTiles.Count,
            RelayCompute = FlockConsts.RelayComputeCost,
            RelayTiles = FlockConsts.RelayTileRequirement,
            RelayUnlocked = RelayUnlocked(c),
            RelayBuilt = c.RelayInProgress || c.RelayFinished,
            EggCost = c.CurrentEggCost,
        };
        foreach (var d in c.Drones)
        {
            if (!TryComp<FlockDroneComponent>(d, out var dc))
                continue;
            var e = new FlockPanelEntry
            {
                Entity = GetNetEntity(d),
                Name = Name(d),
                Resources = dc.Resources,
                Health = GetHealthFraction(d),
                Extra = dc.Dead ? "dead" : dc.Hibernating ? "hibernating" : (TryComp<MindContainerComponent>(d, out var mc) && mc.HasMind) ? "controlled" : "ai",
            };
            st.Drones.Add(e);
        }
        foreach (var t in c.Traces)
            st.Traces.Add(new FlockPanelEntry { Entity = GetNetEntity(t), Name = Name(t) });
        foreach (var s in c.Structures)
        {
            if (!TryComp<FlockStructureComponent>(s, out var sc))
                continue;
            st.Structures.Add(new FlockPanelEntry
            {
                Entity = GetNetEntity(s),
                Name = sc.FlockId,
                Extra = sc.State.ToString(),
                Health = GetHealthFraction(s),
            });
        }
        foreach (var en in c.Enemies)
            st.Enemies.Add(new FlockPanelEntry { Entity = GetNetEntity(en), Name = Name(en) });
        return st;
    }

    private float GetHealthFraction(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var dmg))
            return 1f;
        var max = 100f;
        if (TryComp<MobThresholdsComponent>(uid, out var th))
        {
            foreach (var (value, state) in th.Thresholds)
                if (state == Shared.Mobs.MobState.Dead) { max = (float) value; break; }
        }
        return Math.Clamp(1f - ((float) _damageable.GetTotalDamage((uid, dmg)) / max), 0f, 1f);
    }
}
