using Content.Shared.Mind.Components;
using System.Numerics;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators.Specific;
using Content.Server.NPC.Systems;
using Content.Shared._Starlight.Flock;
using Content.Shared._Starlight.Flock.Components;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Flock;

/// <summary>Everything a flockdrone does: spray modes, incapacitor, eggs, cages, AI actions, death.</summary>
public sealed partial class FlockDroneSystem : EntitySystem
{
    [Dependency] private FlockSystem _flock = default!;
    [Dependency] private FlockConversionSystem _conv = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    private const float ReachRange = 2.6f;
    private const float FloorRunMult = 1.7f;
    private static readonly SoundSpecifier _convertSound = new SoundPathSpecifier("/Audio/_Starlight/Flock/flockdrone_build.ogg");
    private static readonly SoundSpecifier _shootSound = new SoundPathSpecifier("/Audio/_Starlight/Flock/flockdrone_beep2.ogg");

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<FlockDroneComponent> ent, ref MapInitEvent args)
    {
        foreach (var proto in ent.Comp.Actions)
        {
            EntityUid? act = null;
            _actions.AddAction(ent, ref act, proto);
            if (act != null)
                ent.Comp.ActionEntities.Add(act.Value);
        }
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<FlockDroneComponent> ent, ref ComponentShutdown args)
    {
        foreach (var a in ent.Comp.ActionEntities)
            _actions.RemoveAction(a);
    }

    [SubscribeLocalEvent]
    private static void OnRefreshSpeed(Entity<FlockDroneComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.FloorRunning)
            args.ModifySpeed(FloorRunMult, FloorRunMult);
    }

    // ---------------- helpers ----------------

    public bool IsControlled(EntityUid uid) => TryComp<MindContainerComponent>(uid, out var m) && m.HasMind;

    private bool InReach(EntityUid user, EntityCoordinates target)
    {
        var a = _xform.GetMapCoordinates(user);
        var b = _xform.ToMapCoordinates(target);
        return a.MapId == b.MapId && (a.Position - b.Position).Length() <= ReachRange;
    }

    private bool Spend(Entity<FlockDroneComponent> ent, int amount, EntityUid user)
    {
        if (ent.Comp.Resources < amount)
        {
            _popup.PopupEntity(Loc.GetString("flock-drone-not-enough-resources", ("need", amount), ("have", ent.Comp.Resources)), user, user);
            return false;
        }
        ent.Comp.Resources -= amount;
        Dirty(ent);
        return true;
    }

    public void AddResources(Entity<FlockDroneComponent> ent, int amount)
    {
        ent.Comp.Resources += amount;
        if (_flock.TryGetFlock(ent, out var f))
            f.Comp.StatResourcesGained += amount;
        Dirty(ent);
    }

    // ---------------- spray ----------------

    [SubscribeLocalEvent]
    private void OnSprayMode(Entity<FlockDroneComponent> ent, ref FlockSprayModeEvent args)
    {
        ent.Comp.SprayMode = ent.Comp.SprayMode switch
        {
            FlockSprayMode.Convert => FlockSprayMode.Repair,
            FlockSprayMode.Repair => FlockSprayMode.Barricade,
            FlockSprayMode.Barricade => FlockSprayMode.Scrap,
            _ => FlockSprayMode.Convert,
        };
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("flock-spray-mode-" + ent.Comp.SprayMode.ToString().ToLowerInvariant()), ent, ent);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnSpray(Entity<FlockDroneComponent> ent, ref FlockSprayEvent args)
    {
        if (args.Handled || ent.Comp.Dead)
            return;
        if (!_flock.TryGetFlock(ent, out var flock))
            return;
        if (!InReach(ent, args.Target))
        {
            _popup.PopupEntity(Loc.GetString("flock-drone-too-far"), ent, ent);
            return;
        }
        args.Handled = DoSpray(ent, flock, args.Target, args.Entity);
    }

    private bool DoSpray(Entity<FlockDroneComponent> ent, Entity<FlockComponent> flock, EntityCoordinates at, EntityUid? target) =>
        // Depositing into a tealprint works regardless of mode.
        target is { } tp && TryComp<FlockTealprintComponent>(tp, out var print)
            ? Deposit(ent, tp, print)
            : ent.Comp.SprayMode switch
            {
                FlockSprayMode.Convert => SprayConvert(ent, flock, at, target),
                FlockSprayMode.Repair => SprayRepair(ent, target),
                FlockSprayMode.Barricade => SprayBarricade(ent, flock, at),
                FlockSprayMode.Scrap => SprayScrap(ent, flock, target),
                _ => false,
            };

    private bool SprayConvert(Entity<FlockDroneComponent> ent, Entity<FlockComponent> flock, EntityCoordinates at, EntityUid? target)
    {
        if (target is { } t && _conv.FindConversion(t) != null)
        {
            if (!Spend(ent, FlockConsts.ConvertCost, ent))
                return false;
            _conv.TryConvertEntity(flock, t);
            _audio.PlayPvs(_convertSound, ent);
            return true;
        }
        if (_conv.TryGetTile(at, out var grid, out var gc, out var idx) && _conv.CanConvertTile(grid, gc, idx))
        {
            if (!Spend(ent, FlockConsts.ConvertCost, ent))
                return false;
            _conv.TryConvertTile(flock, grid, gc, idx);
            _audio.PlayPvs(_convertSound, ent);
            return true;
        }
        _popup.PopupEntity(Loc.GetString("flock-drone-nothing-to-convert"), ent, ent);
        return false;
    }

    private bool SprayRepair(Entity<FlockDroneComponent> ent, EntityUid? target)
    {
        if (target is not { } t || !HasComp<FlockMemberComponent>(t) || !TryComp<DamageableComponent>(t, out var dmg) || _damageable.GetTotalDamage((t, dmg)) <= 0)
        {
            _popup.PopupEntity(Loc.GetString("flock-drone-nothing-to-repair"), ent, ent);
            return false;
        }
        if (TryComp<FlockDroneComponent>(t, out var other) && other.Dead)
            return false;
        if (!Spend(ent, FlockConsts.RepairCost, ent))
            return false;
        var heal = new DamageSpecifier();
        foreach (var (type, val) in _damageable.GetAllDamage((t, dmg)).DamageDict)
            if (val > 0)
                heal.DamageDict[type] = -FixedPoint2.Min(val, 15);
        _damageable.TryChangeDamage(t, heal, true, false);
        _audio.PlayPvs(_convertSound, t);
        return true;
    }

    private bool SprayBarricade(Entity<FlockDroneComponent> ent, Entity<FlockComponent> flock, EntityCoordinates at)
    {
        if (!_conv.TryGetTile(at, out var _, out var __, out var ___))
            return false;
        if (!Spend(ent, FlockConsts.BarricadeCost, ent))
            return false;
        var b = Spawn("FlockBarricade", _xform.GetMoverCoordinates(at));
        _flock.AddMember(flock, b);
        _audio.PlayPvs(_convertSound, b);
        return true;
    }

    private bool SprayScrap(Entity<FlockDroneComponent> ent, Entity<FlockComponent> _, EntityUid? target)
    {
        if (target is not { } t || t == ent.Owner)
            return false;
        // Flock structures / converted things: deconstruct + refund
        if (TryComp<FlockTealprintComponent>(t, out var print))
        {
            AddResources(ent, print.Deposited);
            QueueDel(t);
            return true;
        }
        if (TryComp<FlockCacheComponent>(t, out var cache))
        {
            AddResources(ent, cache.Resources);
            QueueDel(t);
            _audio.PlayPvs(_convertSound, ent);
            return true;
        }
        if (HasComp<FlockMemberComponent>(t) && !HasComp<FlockDroneComponent>(t) && !HasComp<FlockDeconImmuneComponent>(t))
        {
            var value = TryComp<FlockStructureComponent>(t, out var s) ? s.ResourceCost / 2 : 10;
            if (TryComp<FlockConvertedComponent>(t, out var conv)) value = conv.ScrapValue;
            AddResources(ent, value);
            QueueDel(t);
            _audio.PlayPvs(_convertSound, ent);
            return true;
        }
        // Dead drone remains / cores
        if (TryComp<FlockDroneComponent>(t, out var d) && d.Dead)
        {
            AddResources(ent, d.Resources + 20);
            QueueDel(t);
            return true;
        }
        // Loose items get absorbed
        return HasComp<ItemComponent>(t) && !HasComp<MobStateComponent>(t) && Absorb(ent, t);
    }

    private bool Absorb(Entity<FlockDroneComponent> ent, EntityUid item)
    {
        AddResources(ent, 10);
        QueueDel(item);
        _audio.PlayPvs(_convertSound, ent);
        return true;
    }

    private bool Deposit(Entity<FlockDroneComponent> ent, EntityUid printUid, FlockTealprintComponent print)
    {
        var need = print.Required - print.Deposited;
        var give = Math.Min(need, ent.Comp.Resources);
        if (give <= 0)
        {
            _popup.PopupEntity(Loc.GetString("flock-drone-not-enough-resources", ("need", need), ("have", ent.Comp.Resources)), ent, ent);
            return false;
        }
        ent.Comp.Resources -= give;
        print.Deposited += give;
        Dirty(ent);
        Dirty(printUid, print);
        _audio.PlayPvs(_convertSound, printUid);
        if (print.Deposited >= print.Required)
            RaiseLocalEvent(printUid, new FlockTealprintCompleteEvent());
        return true;
    }

    // ---------------- incapacitor ----------------

    [SubscribeLocalEvent]
    private void OnIncapacitor(Entity<FlockDroneComponent> ent, ref FlockIncapacitorEvent args)
    {
        if (args.Handled || ent.Comp.Dead)
            return;
        args.Handled = TryShoot(ent, args.Target);
    }

    public bool TryShoot(Entity<FlockDroneComponent> ent, EntityCoordinates target)
    {
        if (_timing.CurTime < ent.Comp.NextShot)
            return false;
        if (ent.Comp.Charge < ent.Comp.IncapacitorCost)
        {
            _popup.PopupEntity(Loc.GetString("flock-drone-no-charge"), ent, ent);
            return false;
        }
        var from = _xform.GetMapCoordinates(ent);
        var to = _xform.ToMapCoordinates(target);
        if (from.MapId != to.MapId)
            return false;
        var dir = to.Position - from.Position;
        if (dir.LengthSquared() < 0.01f)
            return false;
        ent.Comp.Charge -= ent.Comp.IncapacitorCost;
        ent.Comp.NextShot = _timing.CurTime + ent.Comp.IncapacitorCooldown;
        Dirty(ent);
        var proj = Spawn("BulletFlockStun", _xform.GetMoverCoordinates(ent));
        _gun.ShootProjectile(proj, Vector2.Normalize(dir), Vector2.Zero, ent, ent, 25f);
        _audio.PlayPvs(_shootSound, ent);
        return true;
    }

    // ---------------- cage ----------------

    [SubscribeLocalEvent]
    private void OnCage(Entity<FlockDroneComponent> ent, ref FlockCageEvent args)
    {
        if (args.Handled || ent.Comp.Dead)
            return;
        if (args.Entity is not { } target)
            return;
        args.Handled = TryCage(ent, target);
    }

    public bool TryCage(Entity<FlockDroneComponent> ent, EntityUid target)
    {
        if (!_flock.TryGetFlock(ent, out var flock))
            return false;
        if (!TryComp<MobStateComponent>(target, out _) || HasComp<FlockMemberComponent>(target))
            return false;
        if (!InReach(ent, Transform(target).Coordinates))
        {
            _popup.PopupEntity(Loc.GetString("flock-drone-too-far"), ent, ent);
            return false;
        }
        var down = _mobState.IsIncapacitated(target) || HasComp<KnockedDownComponent>(target) || HasComp<StunnedComponent>(target);
        if (!down)
        {
            _popup.PopupEntity(Loc.GetString("flock-cage-not-downed"), ent, ent);
            return false;
        }
        var cage = Spawn("FlockCage", Transform(target).Coordinates);
        _flock.AddMember(flock, cage);
        if (!_storage.Insert(target, cage))
        {
            QueueDel(cage);
            return false;
        }
        if (TryComp<FlockCageComponent>(cage, out var cc))
            cc.Occupant = target;
        flock.Comp.StatHumansCaged++;
        _popup.PopupEntity(Loc.GetString("flock-cage-trapped", ("target", target)), target, PopupType.LargeCaution);
        return true;
    }

    // ---------------- eggs ----------------

    [SubscribeLocalEvent]
    private void OnLayEgg(Entity<FlockDroneComponent> ent, ref FlockLayEggEvent args)
    {
        if (args.Handled || ent.Comp.Dead)
            return;
        args.Handled = TryLayEgg(ent);
    }

    public bool TryLayEgg(Entity<FlockDroneComponent> ent)
    {
        if (!_flock.TryGetFlock(ent, out var flock))
            return false;
        if (flock.Comp.Drones.Count >= FlockConsts.DroneLimit)
        {
            _popup.PopupEntity(Loc.GetString("flock-drone-limit"), ent, ent);
            return false;
        }
        if (!Spend(ent, flock.Comp.CurrentEggCost, ent))
            return false;
        var egg = Spawn("FlockEgg", Transform(ent).Coordinates);
        _flock.AddMember(flock, egg);
        flock.Comp.StatStructuresMade++;
        _audio.PlayPvs(_convertSound, egg);
        return true;
    }

    // ---------------- floor run ----------------

    [SubscribeLocalEvent]
    private void OnFloorRun(Entity<FlockDroneComponent> ent, ref FlockFloorRunEvent args)
    {
        ent.Comp.FloorRunning = !ent.Comp.FloorRunning;
        Dirty(ent);
        _speed.RefreshMovementSpeedModifiers(ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.FloorRunning ? "flock-floorrun-on" : "flock-floorrun-off"), ent, ent);
        args.Handled = true;
    }

    // ---------------- damage / death ----------------

    [SubscribeLocalEvent]
    private void OnDamaged(Entity<FlockDroneComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } origin)
            return;
        _flock.AutoEnemy(ent, origin);
        // wake from hibernation when hurt
        if (ent.Comp.Hibernating)
            Wake(ent);
    }

    [SubscribeLocalEvent]
    private void OnMobState(Entity<FlockDroneComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.Dead)
            return;
        ent.Comp.Dead = true;
        Dirty(ent);

        // Kick any controlling flockmind/trace back out through the shunt system.
        if (TryComp<StationAIShuntComponent>(ent, out var shunt) && shunt.Return != null)
            RaiseLocalEvent(ent, new AIUnShuntActionEvent { Performer = ent });

        if (_flock.TryGetFlock(ent, out var flock))
            flock.Comp.StatDronesLost++;

        var debris = Spawn("FlockDroneDebris", Transform(ent).Coordinates);
        if (TryComp<FlockCacheComponent>(debris, out var cache))
            cache.Resources = ent.Comp.Resources + 20;
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Starlight/Flock/flockmind_deathcry.ogg"), debris);
        QueueDel(ent);
    }

    // ---------------- hibernation ----------------

    public void Hibernate(Entity<FlockDroneComponent> ent)
    {
        if (ent.Comp.Hibernating)
            return;
        ent.Comp.Hibernating = true;
        ent.Comp.WanderCount = 0;
        Dirty(ent);
        _appearance.SetData(ent, FlockVisuals.Hibernating, true);
        if (!IsControlled(ent))
            _npc.SleepNPC(ent);
    }

    public void Wake(Entity<FlockDroneComponent> ent)
    {
        if (!ent.Comp.Hibernating)
            return;
        ent.Comp.Hibernating = false;
        ent.Comp.WanderCount = 0;
        Dirty(ent);
        _appearance.SetData(ent, FlockVisuals.Hibernating, false);
        if (!IsControlled(ent))
            _npc.WakeNPC(ent);
    }

    // ---------------- AI actions (raised by HTN) ----------------

    [SubscribeLocalEvent]
    private void OnHtn(Entity<FlockDroneComponent> ent, ref HTNRaisedEvent args)
    {
        if (args.Args is not FlockDroneAiActEvent act || ent.Comp.Dead)
            return;
        if (!_flock.TryGetFlock(ent, out var flock))
            return;
        var target = args.Target;
        switch (act.Act)
        {
            case FlockAiAct.Shoot:
                if (target != null)
                    TryShoot(ent, Transform(target.Value).Coordinates);
                ent.Comp.WanderCount = 0;
                break;
            case FlockAiAct.Cage:
                if (target != null)
                    TryCage(ent, target.Value);
                ent.Comp.WanderCount = 0;
                break;
            case FlockAiAct.LayEgg:
                TryLayEgg(ent);
                ent.Comp.WanderCount = 0;
                break;
            case FlockAiAct.Repair:
                if (target != null)
                {
                    var prev = ent.Comp.SprayMode;
                    ent.Comp.SprayMode = FlockSprayMode.Repair;
                    SprayRepair(ent, target);
                    ent.Comp.SprayMode = prev;
                }
                ent.Comp.WanderCount = 0;
                break;
            case FlockAiAct.Convert:
                AiConvertNearby(ent, flock);
                ent.Comp.WanderCount = 0;
                break;
            case FlockAiAct.Harvest:
                if (target is { } t)
                {
                    if (TryComp<FlockCacheComponent>(t, out var c)) { AddResources(ent, c.Resources); QueueDel(t); }
                    else if (flock.Comp.DeconstructMarks.Contains(t)) SprayScrap(ent, flock, t);
                    else if (HasComp<ItemComponent>(t) && !HasComp<FlockMemberComponent>(t)) Absorb(ent, t);
                }
                ent.Comp.WanderCount = 0;
                break;
            case FlockAiAct.Hibernate:
                Hibernate(ent);
                break;
            case FlockAiAct.Wander:
                ent.Comp.WanderCount++;
                if (ent.Comp.WanderCount >= FlockConsts.DroneWanderPauseCount)
                    Hibernate(ent);
                break;
        }
    }

    private void AiConvertNearby(Entity<FlockDroneComponent> ent, Entity<FlockComponent> flock)
    {
        var coords = Transform(ent).Coordinates;
        if (ent.Comp.Resources < FlockConsts.ConvertCost)
            return;
        ent.Comp.Resources -= FlockConsts.ConvertCost;
        if (_conv.ConvertArea(flock, coords, 1.6f, 1) == 0)
            ent.Comp.Resources += FlockConsts.ConvertCost; // nothing happened: refund
        else
            _audio.PlayPvs(_convertSound, ent);
        Dirty(ent);
    }

    // ---------------- tick ----------------

    public override void Update(float frameTime)
    {
        var q = EntityQueryEnumerator<FlockDroneComponent>();
        while (q.MoveNext(out var uid, out var d))
        {
            if (d.Dead)
                continue;

            // Hibernating AI drones wake up when something needs doing.
            if (d.Hibernating && !IsControlled(uid) && _timing.CurTime >= d.NextAiThink)
            {
                d.NextAiThink = _timing.CurTime + TimeSpan.FromSeconds(3);
                if (_flock.TryGetFlock(uid, out var fl) && (fl.Comp.PriorityTiles.Count > 0 || EnemyNearby(uid, fl)))
                    Wake((uid, d));
            }

            var onFlock = false;
            var xform = Transform(uid);
            if (xform.GridUid is { } g && TryComp<Robust.Shared.Map.Components.MapGridComponent>(g, out var gc))
            {
                var idx = _map.TileIndicesFor(g, gc, xform.Coordinates);
                onFlock = _conv.IsFlockTile(g, gc, idx);
            }

            var regen = d.ChargePerSecond * (onFlock ? 2f : 1f) * frameTime;
            if (d.Charge < d.MaxCharge)
            {
                d.Charge = Math.Min(d.MaxCharge, d.Charge + regen);
                Dirty(uid, d);
            }

            // floor running only works on flock tiles
            if (d.FloorRunning && !onFlock)
            {
                d.FloorRunning = false;
                Dirty(uid, d);
                _speed.RefreshMovementSpeedModifiers(uid);
            }
        }
    }

    private bool EnemyNearby(EntityUid uid, Entity<FlockComponent> flock)
    {
        foreach (var e in flock.Comp.Enemies)
        {
            if (!Exists(e)) continue;
            var a = _xform.GetMapCoordinates(uid);
            var b = _xform.GetMapCoordinates(e);
            if (a.MapId == b.MapId && (a.Position - b.Position).Length() < 10f)
                return true;
        }
        return false;
    }

    private SharedMapSystem _map => field ??= EntityManager.System<SharedMapSystem>();
}

/// <summary>Raised on a tealprint when its resource requirement is met.</summary>
public sealed class FlockTealprintCompleteEvent : EntityEventArgs;
