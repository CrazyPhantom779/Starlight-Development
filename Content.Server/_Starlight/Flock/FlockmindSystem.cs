using Content.Server.Chat.Managers;
using Content.Server.Ghost;
using Content.Shared._Starlight.Flock;
using Content.Shared._Starlight.Flock.Components;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared.Stunnable;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._Starlight.Flock;

/// <summary>Abilities, panel and drone possession for the flockmind and its flocktraces.</summary>
public sealed partial class FlockmindSystem : EntitySystem
{
    [Dependency] private FlockSystem _flock = default!;
    [Dependency] private FlockConversionSystem _conv = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IChatManager _chatMan = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _map = default!;

    private readonly Dictionary<(EntityUid, Vector2i), EntityUid> _priorityMarkers = [];
    private static readonly EntProtoId _panelAction = "ActionFlockPanel";
    private TimeSpan _nextPanel;
    private readonly HashSet<EntityUid> _menuOpen = [];
    private static readonly SoundSpecifier _pingSound = new SoundPathSpecifier("/Audio/_Starlight/Flock/ping.ogg");

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<FlockMemberComponent>(FlockUiKey.Panel, subs =>
        {
            subs.Event<FlockPanelJumpMessage>(OnJump);
            subs.Event<FlockPanelReleaseTraceMessage>(OnReleaseTrace);
            subs.Event<FlockPanelControlMessage>(OnControl);
            subs.Event<FlockTealprintChoiceMessage>(OnTealprintChoice);
            subs.Event<BoundUIClosedEvent>((uid, _, _) => _menuOpen.Remove(uid));
        });
    }

    // ---------------- setup ----------------

    [SubscribeLocalEvent]
    private void OnMindMapInit(Entity<FlockmindComponent> ent, ref MapInitEvent args)
    {
        var flock = _flock.CreateFlock(ent);
        EntityUid? act = null;
        _actions.AddAction(ent, ref act, ent.Comp.RiftAction);
        ent.Comp.RiftActionEntity = act;
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnTraceMapInit(Entity<FlocktraceComponent> ent, ref MapInitEvent args)
    {
        foreach (var proto in ent.Comp.Actions)
        {
            EntityUid? act = null;
            _actions.AddAction(ent, ref act, proto);
            if (act != null)
                ent.Comp.ActionEntities.Add(act.Value);
        }
        // Panel is available to traces as well.
        EntityUid? panel = null;
        _actions.AddAction(ent, ref panel, _panelAction);
        if (panel != null)
            ent.Comp.ActionEntities.Add(panel.Value);
    }

    private bool TryGetPerformerFlock(EntityUid performer, out Entity<FlockComponent> flock)
        => _flock.TryGetFlock(performer, out flock);

    // ---------------- rift ----------------

    [SubscribeLocalEvent]
    private void OnSpawnRift(Entity<FlockmindComponent> ent, ref FlockSpawnRiftEvent args)
    {
        if (args.Handled || ent.Comp.RiftPlaced)
            return;
        if (!_flock.TryGetFlock(ent, out var flock))
            return;
        if (!_conv.TryGetTile(args.Target, out var grid, out var gc, out var idx) ||
            _conv.IsFlockTile(grid, gc, idx) || _map.GetTileRef(grid, gc, idx).Tile.IsEmpty)
        {
            _popup.PopupEntity(Loc.GetString("flock-rift-bad-location"), ent, ent);
            return;
        }
        // Must be a solid, unobstructed tile.
        var coords = _map.GridTileToLocal(grid, gc, idx);
        foreach (var e in _map.GetAnchoredEntities(grid, gc, idx))
        {
            if (TryComp<FixturesComponent>(e, out _) && !HasComp<FlockMemberComponent>(e))
            {
                if (TryComp<PhysicsComponent>(e, out var p) && p.Hard)
                {
                    _popup.PopupEntity(Loc.GetString("flock-rift-bad-location"), ent, ent);
                    return;
                }
            }
        }

        var rift = Spawn("FlockRift", coords);
        _flock.AddMember(flock, rift);
        flock.Comp.RelayLocation = coords;
        ent.Comp.RiftPlaced = true;

        if (ent.Comp.RiftActionEntity != null)
            _actions.RemoveAction(ent.Comp.RiftActionEntity.Value);

        foreach (var proto in ent.Comp.Actions)
        {
            EntityUid? act = null;
            _actions.AddAction(ent, ref act, proto);
            if (act != null)
                ent.Comp.ActionEntities.Add(act.Value);
        }
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("flock-rift-placed"), ent, ent, PopupType.Large);
        args.Handled = true;
    }

    // ---------------- basic marking abilities ----------------

    [SubscribeLocalEvent]
    private void OnPing(Entity<FlockMemberComponent> ent, ref FlockPingEvent args)
    {
        if (args.Handled)
            return;
        var ping = Spawn("FlockPing", _xform.GetMoverCoordinates(args.Target));
        _audio.PlayEntity(_pingSound, ent, ping);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnOpenPanel(Entity<FlockMemberComponent> ent, ref FlockOpenPanelEvent args)
    {
        if (args.Handled || !_flock.TryGetFlock(ent, out var flock))
            return;
        _ui.TryOpenUi(ent.Owner, FlockUiKey.Panel, ent);
        _ui.SetUiState(ent.Owner, FlockUiKey.Panel, _flock.BuildPanelState(flock));
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDesignateTile(Entity<FlockMemberComponent> ent, ref FlockDesignateTileEvent args)
    {
        if (args.Handled || !_flock.TryGetFlock(ent, out var flock))
            return;
        if (!_conv.TryGetTile(args.Target, out var grid, out var gc, out var idx))
            return;
        var key = (grid, idx);
        if (flock.Comp.PriorityTiles.Remove(key))
        {
            if (_priorityMarkers.Remove(key, out var m))
                QueueDel(m);
        }
        else if (_conv.CanConvertTile(grid, gc, idx))
        {
            flock.Comp.PriorityTiles.Add(key);
            _priorityMarkers[key] = Spawn("FlockPriorityMarker", _map.GridTileToLocal(grid, gc, idx));
        }
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDesignateEnemy(Entity<FlockMemberComponent> ent, ref FlockDesignateEnemyEvent args)
    {
        if (args.Handled || !_flock.TryGetFlock(ent, out var flock))
            return;
        if (HasComp<FlockMemberComponent>(args.Target) || !HasComp<MobStateComponent>(args.Target))
            return;
        var on = _flock.ToggleEnemy(flock, args.Target);
        _popup.PopupEntity(Loc.GetString(on ? "flock-enemy-marked" : "flock-enemy-unmarked", ("target", args.Target)), ent, ent);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDesignateIgnore(Entity<FlockMemberComponent> ent, ref FlockDesignateIgnoreEvent args)
    {
        if (args.Handled || !_flock.TryGetFlock(ent, out var flock))
            return;
        var on = _flock.ToggleIgnore(flock, args.Target);
        _popup.PopupEntity(Loc.GetString(on ? "flock-ignore-marked" : "flock-ignore-unmarked", ("target", args.Target)), ent, ent);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnMarkDeconstruct(Entity<FlockMemberComponent> ent, ref FlockMarkDeconstructEvent args)
    {
        if (args.Handled || !_flock.TryGetFlock(ent, out var flock))
            return;
        var t = args.Target;
        if (!HasComp<FlockMemberComponent>(t) || HasComp<FlockDeconImmuneComponent>(t) || HasComp<FlockDroneComponent>(t))
            return;
        var ann = EnsureComp<FlockAnnotatedComponent>(t);
        if (flock.Comp.DeconstructMarks.Remove(t))
            ann.Annotations.Remove(FlockAnnotation.Deconstruct);
        else
        {
            flock.Comp.DeconstructMarks.Add(t);
            ann.Annotations.Add(FlockAnnotation.Deconstruct);
        }
        Dirty(t, ann);
        args.Handled = true;
    }

    // ---------------- big abilities ----------------

    [SubscribeLocalEvent]
    private void OnPartition(Entity<FlockmindComponent> ent, ref FlockPartitionMindEvent args)
    {
        if (args.Handled || !_flock.TryGetFlock(ent, out var flock))
            return;
        if (!_flock.CanAffordCompute(flock.Comp, FlockConsts.FlocktraceComputeCost))
        {
            _popup.PopupEntity(Loc.GetString("flock-need-compute"), ent, ent);
            return;
        }
        var trace = Spawn("MobFlocktraceGhostRole", Transform(ent).Coordinates);
        _flock.AddMember(flock, trace);
        _popup.PopupEntity(Loc.GetString("flock-partition-done"), ent, ent);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDiffract(Entity<FlockmindComponent> ent, ref FlockDiffractEvent args)
    {
        if (args.Handled || !_flock.TryGetFlock(ent, out var flock))
            return;
        var t = args.Target;
        if (!TryComp<FlockDroneComponent>(t, out var drone) || !_flock.IsSameFlock(ent, t))
        {
            _popup.PopupEntity(Loc.GetString("flock-diffract-invalid"), ent, ent);
            return;
        }
        if (drone.Dead)
            return;
        if (_flock.GetComplexDroneCount(flock.Comp) <= 1)
        {
            _popup.PopupEntity(Loc.GetString("flock-diffract-last"), ent, ent, PopupType.LargeCaution);
            return;
        }
        // Kick a controller out first, then split.
        if (TryComp<StationAIShuntComponent>(t, out var shunt) && shunt.Return != null)
            RaiseLocalEvent(t, new AIUnShuntActionEvent { Performer = t });
        var coords = Transform(t).Coordinates;
        for (var i = 0; i < 3; i++)
        {
            var bit = Spawn("MobFlockBit", coords);
            _flock.AddMember(flock, bit);
        }
        flock.Comp.StatBitsMade += 3;
        // resources left behind
        var debris = Spawn("FlockDroneDebris", coords);
        if (TryComp<FlockCacheComponent>(debris, out var cache))
            cache.Resources = drone.Resources;
        QueueDel(t);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnGatecrash(Entity<FlockmindComponent> ent, ref FlockGatecrashEvent args)
    {
        if (args.Handled)
            return;
        var opened = 0;
        foreach (var d in _lookup.GetEntitiesInRange<DoorComponent>(Transform(ent).Coordinates, 10f))
        {
            if (!HasComp<AirlockComponent>(d))
                continue;
            if (_door.TryOpen(d, d.Comp, null, false, true))
                opened++;
        }
        if (opened == 0)
        {
            _popup.PopupEntity(Loc.GetString("flock-gatecrash-none"), ent, ent);
            return;
        }
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnRepairBurst(Entity<FlockmindComponent> ent, ref FlockRepairBurstEvent args)
    {
        if (args.Handled)
            return;
        var healed = 0;
        foreach (var e in _lookup.GetEntitiesInRange<FlockMemberComponent>(args.Target, 3f))
        {
            if (healed >= 4)
                break;
            if (!_flock.IsSameFlock(ent, e) || (TryComp<FlockDroneComponent>(e, out var d) && d.Dead))
                continue;
            if (!TryComp<DamageableComponent>(e, out var dmg) || _damageable.GetTotalDamage((e, dmg)) <= 0)
                continue;
            var heal = new DamageSpecifier();
            foreach (var (type, val) in _damageable.GetAllDamage((e, dmg)).DamageDict)
                if (val > 0)
                    heal.DamageDict[type] = -FixedPoint2.Min(val, 30);
            _damageable.TryChangeDamage(e.Owner, heal, true, false);
            Spawn("FlockHealEffect", Transform(e).Coordinates);
            healed++;
        }
        args.Handled = healed > 0;
    }

    [SubscribeLocalEvent]
    private void OnRadioStun(Entity<FlockmindComponent> ent, ref FlockRadioStunEvent args)
    {
        if (args.Handled)
            return;
        var hit = 0;
        foreach (var m in _lookup.GetEntitiesInRange<MobStateComponent>(args.Target, 3f))
        {
            if (HasComp<FlockMemberComponent>(m))
                continue;
            if (!_inventory.TryGetSlotEntity(m, "ears", out var ears) || !HasComp<HeadsetComponent>(ears))
                continue;
            _stun.TryAddStunDuration(m, TimeSpan.FromSeconds(3));
            _popup.PopupEntity(Loc.GetString("flock-radio-stun-victim"), m, m, PopupType.LargeCaution);
            hit++;
        }
        if (hit == 0)
        {
            _popup.PopupEntity(Loc.GetString("flock-radio-stun-none"), ent, ent);
            return;
        }
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnNarrowbeam(Entity<FlockmindComponent> ent, ref FlockNarrowbeamEvent args)
    {
        if (args.Handled)
            return;
        // Message text is entered through the generic say box: whatever the flockmind says next is relayed.
        // Simplified port: relay a fixed garbled transmission and open a text prompt later (see TODO in docs).
        if (!_inventory.TryGetSlotEntity(args.Target, "ears", out var ears) || !HasComp<HeadsetComponent>(ears))
        {
            _popup.PopupEntity(Loc.GetString("flock-narrowbeam-no-radio"), ent, ent);
            return;
        }
        if (TryComp<ActorComponent>(args.Target, out var actor))
            _chatMan.DispatchServerMessage(actor.PlayerSession, Loc.GetString("flock-narrowbeam-message"));
        args.Handled = true;
    }

    // ---------------- tealprint ----------------

    [SubscribeLocalEvent]
    private void OnTealprintAction(Entity<FlockmindComponent> ent, ref FlockTealprintEvent args)
    {
        if (args.Handled || !_flock.TryGetFlock(ent, out var flock))
            return;
        if (!_conv.TryGetTile(args.Target, out var grid, out var gc, out var idx) || _map.GetTileRef(grid, gc, idx).Tile.IsEmpty)
            return;
        var state = new FlockTealprintMenuState { At = GetNetCoordinates(args.Target) };
        foreach (var id in flock.Comp.UnlockedStructures)
        {
            if (!_proto.TryIndex<EntityPrototype>(id, out var p) || !p.TryComp<FlockStructureComponent>(out var sc, _compFactory))
                continue;
            state.Available.Add((id, p.Name, sc.ResourceCost));
        }
        if (_flock.RelayUnlocked(flock.Comp))
            state.Available.Add(("FlockRelay", "Relay", 0));
        _menuOpen.Add(ent);
        _ui.TryOpenUi(ent.Owner, FlockUiKey.Panel, ent);
        _ui.SetUiState(ent.Owner, FlockUiKey.Panel, state);
        args.Handled = true;
    }

    private void OnTealprintChoice(Entity<FlockMemberComponent> ent, ref FlockTealprintChoiceMessage msg)
    {
        if (!_flock.TryGetFlock(ent, out var flock))
            return;
        if (!flock.Comp.UnlockedStructures.Contains(msg.Structure) && !(msg.Structure == "FlockRelay" && _flock.RelayUnlocked(flock.Comp)))
            return;
        if (!_proto.TryIndex<EntityPrototype>(msg.Structure, out var p) || !p.TryComp<FlockStructureComponent>(out var sc, _compFactory))
            return;
        var at = GetCoordinates(msg.At);
        if (msg.Structure == "FlockRelay" && flock.Comp.RelayInProgress)
            return;
        var print = Spawn("FlockTealprint", at);
        var pc = EnsureComp<FlockTealprintComponent>(print);
        pc.Structure = msg.Structure;
        pc.Required = msg.Structure == "FlockRelay" ? 0 : sc.ResourceCost;
        Dirty(print, pc);
        _flock.AddMember(flock, print);
        if (msg.Structure == "FlockRelay")
        {
            flock.Comp.RelayInProgress = true;
            RaiseLocalEvent(print, new FlockTealprintCompleteEvent());
        }
        _menuOpen.Remove(ent);
        _ui.CloseUi(ent.Owner, FlockUiKey.Panel);
    }

    // ---------------- panel messages ----------------

    private void OnJump(Entity<FlockMemberComponent> ent, ref FlockPanelJumpMessage msg)
    {
        var target = GetEntity(msg.Target);
        if (!Exists(target) || (!HasComp<FlockmindComponent>(ent) && !HasComp<FlocktraceComponent>(ent)))
            return;
        _xform.SetCoordinates(ent, Transform(target).Coordinates);
    }

    private void OnReleaseTrace(Entity<FlockMemberComponent> ent, ref FlockPanelReleaseTraceMessage msg)
    {
        if (!HasComp<FlockmindComponent>(ent))
            return;
        var t = GetEntity(msg.Target);
        if (!HasComp<FlocktraceComponent>(t) || !_flock.IsSameFlock(ent, t))
            return;
        ReleaseTrace(t);
    }

    public void ReleaseTrace(EntityUid trace)
    {
        if (_mind.TryGetMind(trace, out var mindId, out var mind))
            _ghost.OnGhostAttempt(mindId, false, forced: true, mind: mind);
        QueueDel(trace);
    }

    private void OnControl(Entity<FlockMemberComponent> ent, ref FlockPanelControlMessage msg)
    {
        var t = GetEntity(msg.Target);
        if (!HasComp<FlockDroneComponent>(t) || !_flock.IsSameFlock(ent, t))
            return;
        var ev = new AIShuntActionEvent { Performer = ent, Target = t, IgnoreCameraView = true };
        RaiseLocalEvent(ent, ev);
    }

    // ---------------- tick ----------------

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextPanel)
            return;
        _nextPanel = _timing.CurTime + TimeSpan.FromSeconds(2);
        var q = EntityQueryEnumerator<FlockMemberComponent, UserInterfaceComponent>();
        while (q.MoveNext(out var uid, out var m, out _))
        {
            if (_menuOpen.Contains(uid) || !_ui.IsUiOpen(uid, FlockUiKey.Panel) || !_flock.TryGetFlock(uid, out var flock))
                continue;
            // The tealprint menu state is sticky until closed; only refresh the panel
            _ui.SetUiState(uid, FlockUiKey.Panel, _flock.BuildPanelState(flock));
        }
    }
}
