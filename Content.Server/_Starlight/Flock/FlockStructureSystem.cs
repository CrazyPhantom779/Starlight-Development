using Content.Server.Lightning;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators.Specific;
using Content.Shared._Starlight.Flock;
using Content.Shared._Starlight.Flock.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using System.Numerics;

namespace Content.Server._Starlight.Flock;

/// <summary>Eggs, rift, caches, collectors, compute nodes, defensive structures, tealprints and flockbits.</summary>
public sealed partial class FlockStructureSystem : EntitySystem
{
    [Dependency] private FlockSystem _flock = default!;
    [Dependency] private FlockConversionSystem _conv = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private LightningSystem _lightning = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IRobustRandom _random = default!;

    private static readonly SoundSpecifier _eggSound = new SoundPathSpecifier("/Audio/_Starlight/Flock/flockdrone_build_complete.ogg");

    [SubscribeLocalEvent]
    private void OnStructureShutdown(Entity<FlockStructureComponent> ent, ref ComponentShutdown args)
    {
        if (_flock.TryGetFlock(ent, out var f))
            _flock.UpdateEggCost(f);
    }

    [SubscribeLocalEvent]
    private void OnStructureDamaged(Entity<FlockStructureComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased && args.Origin is { } o)
            _flock.AutoEnemy(ent, o);
    }

    // ---------------- egg ----------------

    [SubscribeLocalEvent]
    private void OnEggInit(Entity<FlockEggComponent> ent, ref MapInitEvent args)
        => ent.Comp.HatchAt = _timing.CurTime + ent.Comp.HatchTime;

    [SubscribeLocalEvent]
    private void OnRiftInit(Entity<FlockRiftComponent> ent, ref MapInitEvent args)
        => ent.Comp.OpenAt = _timing.CurTime + ent.Comp.EntryTime;

    // ---------------- tealprint ----------------

    [SubscribeLocalEvent]
    private void OnTealprintComplete(Entity<FlockTealprintComponent> ent, ref FlockTealprintCompleteEvent args)
    {
        if (!_flock.TryGetFlock(ent, out var flock))
            return;
        var coords = Transform(ent).Coordinates;
        var s = Spawn(ent.Comp.Structure, coords);
        _flock.AddMember(flock, s);
        flock.Comp.StatStructuresMade++;
        _audio.PlayPvs(_eggSound, s);
        QueueDel(ent);
    }

    // ---------------- flockbit ----------------

    [SubscribeLocalEvent]
    private void OnBitHtn(Entity<FlockBitComponent> ent, ref HTNRaisedEvent args)
    {
        if (args.Args is not FlockDroneAiActEvent act || act.Act != FlockAiAct.Convert)
            return;
        if (_timing.CurTime < ent.Comp.NextConvert || !_flock.TryGetFlock(ent, out var flock))
            return;
        ent.Comp.NextConvert = _timing.CurTime + (ent.Comp.ConvertDelay / ent.Comp.SapperBoost);
        _conv.ConvertArea(flock, Transform(ent).Coordinates, 1.6f, 1);
    }

    // ---------------- tick ----------------

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var eggs = EntityQueryEnumerator<FlockEggComponent>();
        while (eggs.MoveNext(out var uid, out var egg))
        {
            if (now < egg.HatchAt)
                continue;
            Hatch((uid, egg));
        }

        var rifts = EntityQueryEnumerator<FlockRiftComponent>();
        while (rifts.MoveNext(out var uid, out var rift))
        {
            if (now < rift.OpenAt)
                continue;
            OpenRift((uid, rift));
        }

        var collectors = EntityQueryEnumerator<FlockCollectorComponent, FlockStructureComponent>();
        while (collectors.MoveNext(out var uid, out var col, out var sc))
        {
            if (now < col.NextCycle)
                continue;
            col.NextCycle = now + col.CycleTime;
            UpdateCollector((uid, col, sc));
        }

        var sentinels = EntityQueryEnumerator<FlockSentinelComponent, FlockStructureComponent>();
        while (sentinels.MoveNext(out var uid, out var sen, out var sc))
        {
            if (!sc.Powered || sc.State != FlockStructureState.Online)
                continue;
            UpdateSentinel((uid, sen), frameTime);
        }

        var interceptors = EntityQueryEnumerator<FlockInterceptorComponent, FlockStructureComponent>();
        while (interceptors.MoveNext(out var uid, out var inter, out var sc))
        {
            if (now < inter.NextShot || sc.State != FlockStructureState.Online)
                continue;
            UpdateInterceptor((uid, inter));
        }

        var sappers = EntityQueryEnumerator<FlockSapperComponent, FlockStructureComponent>();
        while (sappers.MoveNext(out var uid, out var sap, out var sc))
        {
            if (sc.State != FlockStructureState.Online)
                continue;
            UpdateSapper((uid, sap), frameTime);
        }

        var turrets = EntityQueryEnumerator<FlockGnesisTurretComponent, FlockStructureComponent>();
        while (turrets.MoveNext(out var uid, out var tur, out var sc))
        {
            if (sc.State != FlockStructureState.Online)
                continue;
            UpdateTurret((uid, tur), frameTime);
        }
    }

    private void Hatch(Entity<FlockEggComponent> egg)
    {
        var coords = Transform(egg).Coordinates;
        _flock.TryGetFlock(egg, out var flock);
        for (var i = 0; i < egg.Comp.Count; i++)
        {
            var m = Spawn(egg.Comp.Hatch, coords);
            if (flock.Comp != null)
            {
                _flock.AddMember(flock, m);
                if (HasComp<FlockDroneComponent>(m)) flock.Comp.StatDronesMade++;
                if (HasComp<FlockBitComponent>(m)) flock.Comp.StatBitsMade++;
            }
        }
        _audio.PlayPvs(_eggSound, coords);
        QueueDel(egg);
    }

    private void OpenRift(Entity<FlockRiftComponent> rift)
    {
        var coords = Transform(rift).Coordinates;
        if (!_flock.TryGetFlock(rift, out var flock))
        {
            QueueDel(rift);
            return;
        }
        for (var i = 0; i < rift.Comp.StartingDrones; i++)
        {
            var d = Spawn("MobFlockDrone", coords.Offset(_random.NextVector2(1.2f)));
            _flock.AddMember(flock, d);
            flock.Comp.StatDronesMade++;
        }
        for (var i = 0; i < rift.Comp.StartingCaches; i++)
        {
            var c = Spawn(rift.Comp.Cache, coords.Offset(_random.NextVector2(2.2f)));
            if (TryComp<FlockCacheComponent>(c, out var cc))
                cc.Resources = rift.Comp.CacheResources;
        }
        // flock tile under the rift, to give drones somewhere to charge up
        if (_conv.TryGetTile(coords, out var g, out var gc, out var idx))
            _conv.TryConvertTile(flock, g, gc, idx);
        flock.Comp.Started = true;
        _audio.PlayPvs(_eggSound, coords);
        QueueDel(rift);
    }

    // ---------------- collector ----------------

    private void UpdateCollector(Entity<FlockCollectorComponent, FlockStructureComponent> ent)
    {
        if (!_flock.TryGetFlock(ent, out var flock))
            return;
        var xform = Transform(ent);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gc))
            return;
        var origin = _map.TileIndicesFor(grid, gc, xform.Coordinates);
        // flood fill over flock tiles, up to MaxRange away (goon: calcconnected)
        var seen = new HashSet<Vector2i> { origin };
        var frontier = new Queue<(Vector2i, int)>();
        frontier.Enqueue((origin, 0));
        var count = 0;
        while (frontier.Count > 0)
        {
            var (p, d) = frontier.Dequeue();
            if (_conv.IsFlockTile(grid, gc, p))
                count++;
            if (d >= ent.Comp1.MaxRange)
                continue;
            foreach (var n in new[] { p + new Vector2i(1, 0), p + new Vector2i(-1, 0), p + new Vector2i(0, 1), p + new Vector2i(0, -1) })
                if (seen.Add(n) && _conv.IsFlockTile(grid, gc, n))
                    frontier.Enqueue((n, d + 1));
        }
        ent.Comp1.ConnectedTiles = count;
        ent.Comp2.Compute = count * ent.Comp1.ComputePerTile;
        Dirty(ent.Owner, ent.Comp2);
    }

    // ---------------- sentinel ----------------

    private void UpdateSentinel(Entity<FlockSentinelComponent> ent, float dt)
    {
        var sentinel = ent.Comp;
        sentinel.Charge = Math.Min(sentinel.MaxCharge, sentinel.Charge + (sentinel.ChargePerSecond * dt));
        if (sentinel.Charge < sentinel.MaxCharge || _timing.CurTime < sentinel.NextZap)
            return;
        var targets = FindEnemies(ent, sentinel.Range);
        if (targets.Count == 0)
            return;
        sentinel.Charge = 0;
        sentinel.NextZap = _timing.CurTime + TimeSpan.FromSeconds(2);
        var n = 0;
        foreach (var t in targets)
        {
            if (n++ >= sentinel.ChainTargets)
                break;
            _lightning.ShootLightning(ent, t, "FlockLightning");
        }
    }

    private List<EntityUid> FindEnemies(EntityUid structure, float range)
    {
        var list = new List<EntityUid>();
        if (!_flock.TryGetFlock(structure, out var flock))
            return list;
        foreach (var m in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(structure).Coordinates, range))
        {
            if (_mobState.IsDead(m) || !flock.Comp.Enemies.Contains(m))
                continue;
            list.Add(m);
        }
        return list;
    }

    // ---------------- interceptor ----------------

    private void UpdateInterceptor(Entity<FlockInterceptorComponent> ent)
    {
        foreach (var p in _lookup.GetEntitiesInRange<ProjectileComponent>(Transform(ent).Coordinates, ent.Comp.Radius))
        {
            if (HasComp<FlockMemberComponent>(p.Comp.Shooter ?? EntityUid.Invalid))
                continue;
            ent.Comp.NextShot = _timing.CurTime + ent.Comp.Cooldown;
            Spawn("FlockInterceptEffect", Transform(p).Coordinates);
            QueueDel(p);
            return;
        }
    }

    // ---------------- sapper ----------------
    // NOTE: Creative interpretation - goon's sapper behaviour is only partly documented, see PORT_NOTES.md.

    private float _sapperAccum;
    private void UpdateSapper(Entity<FlockSapperComponent> ent, float dt)
    {
        _sapperAccum += dt;
        if (_sapperAccum < 1f)
            return;
        _sapperAccum = 0f;
        var coords = Transform(ent).Coordinates;
        switch (ent.Comp.Mode)
        {
            case FlockSapperMode.Bits:
                foreach (var b in _lookup.GetEntitiesInRange<FlockBitComponent>(coords, ent.Comp.Range))
                    b.Comp.SapperBoost = 2f;
                break;
            case FlockSapperMode.Drones:
                foreach (var d in _lookup.GetEntitiesInRange<FlockDroneComponent>(coords, ent.Comp.Range))
                    HealOne(d, 2);
                break;
            case FlockSapperMode.Structures:
                foreach (var s in _lookup.GetEntitiesInRange<FlockStructureComponent>(coords, ent.Comp.Range))
                    HealOne(s, 2);
                break;
        }
    }

    private void HealOne(EntityUid e, int amount)
    {
        if (!TryComp<DamageableComponent>(e, out var dmg) || _damageable.GetTotalDamage((e, dmg)) <= 0)
            return;
        var heal = new DamageSpecifier();
        foreach (var (type, val) in _damageable.GetAllDamage((e, dmg)).DamageDict)
            if (val > 0)
                heal.DamageDict[type] = -FixedPoint2.Min(val, amount);
        _damageable.TryChangeDamage(e, heal, true, false);
    }

    // ---------------- gnesis turret ----------------

    private void UpdateTurret(Entity<FlockGnesisTurretComponent> ent, float dt)
    {
        var t = ent.Comp;
        t.Fluid = Math.Min(t.FluidMax, t.Fluid + (t.FluidPerSecond * dt));
        if (t.Fluid < t.FluidPerShot || _timing.CurTime < t.NextShot)
            return;
        var targets = FindEnemies(ent, t.Range);
        if (targets.Count == 0)
            return;
        var target = targets[0];
        var from = _xform.GetMapCoordinates(ent);
        var to = _xform.GetMapCoordinates(target);
        var dir = to.Position - from.Position;
        if (dir.LengthSquared() < 0.01f)
            return;
        t.Fluid -= t.FluidPerShot;
        t.NextShot = _timing.CurTime + t.Cooldown;
        var proj = Spawn("BulletFlockGnesis", Transform(ent).Coordinates);
        _gun.ShootProjectile(proj, Vector2.Normalize(dir), Vector2.Zero, ent, ent, 25f);
    }
}
