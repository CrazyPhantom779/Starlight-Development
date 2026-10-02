using Robust.Shared.Player;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.RoundEnd;
using Content.Shared._Starlight.Flock.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Stunnable;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Content.Shared.Mobs.Components;

namespace Content.Server._Starlight.Flock;

/// <summary>Raised when a relay is destroyed before it fired. The flockmind dies with it.</summary>
[ByRefEvent]
public record struct FlockRelayDestroyedEvent(EntityUid Flock);

/// <summary>Raised when the relay finishes transmitting.</summary>
[ByRefEvent]
public record struct FlockRelayFiredEvent(EntityUid Flock);

/// <summary>The flock win condition. Faithful port of goon's /obj/flock_structure/relay.</summary>
public sealed partial class FlockRelaySystem : EntitySystem
{
    [Dependency] private FlockSystem _flock = default!;
    [Dependency] private FlockConversionSystem _conv = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TransformSystem _xform = default!;

    /// <summary>The round-end countdown after the signal fires. Goon: 60s; Starlight: 10 minutes.</summary>
    public static readonly TimeSpan EndCountdown = TimeSpan.FromMinutes(10);

    private static readonly SoundSpecifier _reactor = new SoundPathSpecifier("/Audio/_Starlight/Flock/Flock_Reactor.ogg");
    private static readonly SoundSpecifier _broadcastCharge = new SoundPathSpecifier("/Audio/_Starlight/Flock/flock_broadcast_charge.ogg");
    private static readonly SoundSpecifier _kaboom = new SoundPathSpecifier("/Audio/_Starlight/Flock/flock_broadcast_kaboom.ogg");

    private readonly List<(EntityUid Headset, TimeSpan At)> _radiosToBreak = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlockRelayComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<FlockRelayComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<FlockRelayComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.BuildAt = _timing.CurTime + ent.Comp.BuildDelay;
        if (_flock.TryGetFlock(ent, out var flock))
        {
            flock.Comp.Relay = ent;
            flock.Comp.RelayInProgress = true;
        }
    }

    private void OnShutdown(Entity<FlockRelayComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Fired || LifeStage(ent) < EntityLifeStage.Terminating)
            return;
        if (_flock.TryGetFlock(ent, out var flock))
        {
            flock.Comp.RelayInProgress = false;
            var ev = new FlockRelayDestroyedEvent(flock);
            RaiseLocalEvent(ref ev);
        }
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        for (var i = _radiosToBreak.Count - 1; i >= 0; i--)
        {
            if (now < _radiosToBreak[i].At)
                continue;
            BreakRadio(_radiosToBreak[i].Headset);
            _radiosToBreak.RemoveAt(i);
        }

        var q = EntityQueryEnumerator<FlockRelayComponent>();
        while (q.MoveNext(out var uid, out var relay))
        {
            if (relay.Fired)
            {
                if (relay.Charging && now >= relay.ChargeStart)
                    Detonate((uid, relay));
                continue;
            }

            if (!relay.Charging)
            {
                if (now < relay.BuildAt)
                    continue;
                StartCharging((uid, relay));
                continue;
            }

            var elapsed = now - relay.ChargeStart;
            if (elapsed >= relay.ChargeTime)
            {
                Unleash((uid, relay));
                continue;
            }

            // Radius grows linearly with charge.
            var frac = (float) (elapsed / relay.ChargeTime);
            relay.ConversionRadius = Math.Clamp(1 + (int) (frac * (relay.MaxConversionRadius - 1)), 1, relay.MaxConversionRadius);

            if (now >= relay.NextConvert)
            {
                relay.NextConvert = now + TimeSpan.FromSeconds(1);
                ConvertStep((uid, relay));
            }
            if (now >= relay.NextSound)
            {
                relay.NextSound = now + relay.SoundLength;
                _audio.PlayGlobal(_reactor, Filter.Broadcast(), false);
            }
        }
    }

    private void StartCharging(Entity<FlockRelayComponent> ent)
    {
        ent.Comp.Charging = true;
        ent.Comp.ChargeStart = _timing.CurTime;
        ent.Comp.NextSound = _timing.CurTime;
        Dirty(ent);

        // No shuttle for you: either destroy the relay or flee when it unleashes (goon).
        if (_roundEnd.IsRoundEndRequested())
            _roundEnd.CancelRoundEndCountdown(null, null, true);

        _chat.DispatchGlobalAnnouncement(Loc.GetString("flock-relay-constructed-announcement"),
            Loc.GetString("flock-announcement-sender"), true);
        if (_flock.TryGetFlock(ent, out var flock) && flock.Comp.Flockmind is { } mind)
            _popup.PopupEntity(Loc.GetString("flock-relay-constructed-flockmind"), mind, mind, PopupType.LargeCaution);
    }

    private void ConvertStep(Entity<FlockRelayComponent> ent)
    {
        if (!_flock.TryGetFlock(ent, out var flock))
            return;
        var xform = Transform(ent);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gc))
            return;
        var center = _map.TileIndicesFor(grid, gc, xform.Coordinates);
        var r = ent.Comp.ConversionRadius;
        var budget = 25;
        for (var x = -r; x <= r && budget > 0; x++)
        {
            for (var y = -r; y <= r && budget > 0; y++)
            {
                if ((x * x) + (y * y) > r * r)
                    continue;
                if (_conv.TryConvertTile(flock, grid, gc, center + new Vector2i(x, y)))
                    budget--;
            }
        }
    }

    private void Unleash(Entity<FlockRelayComponent> ent)
    {
        ent.Comp.Fired = true;
        ent.Comp.Charging = true;
        // ChargeStart is re-used as "detonation time" once fired.
        ent.Comp.ChargeStart = _timing.CurTime + ent.Comp.FinalChargeTime;
        Dirty(ent);
        _chat.DispatchGlobalAnnouncement(Loc.GetString("flock-relay-transmitting"),
            Loc.GetString("flock-announcement-sender"), true);
        _audio.PlayGlobal(_broadcastCharge, Filter.Broadcast(), false);
        if (_flock.TryGetFlock(ent, out var flock))
            flock.Comp.RelayInProgress = true;
    }

    private void Detonate(Entity<FlockRelayComponent> ent)
    {
        ent.Comp.Charging = false;
        _audio.PlayGlobal(_kaboom, Filter.Broadcast(), false);

        if (_flock.TryGetFlock(ent, out var flock))
        {
            flock.Comp.RelayFinished = true;
            var ev = new FlockRelayFiredEvent(flock);
            RaiseLocalEvent(ref ev);
        }

        // Round ends in 10 minutes and cannot be recalled (goon: emergency shuttle, 60s, can_recall = false).
        _roundEnd.RequestRoundEnd(EndCountdown, ent, null, false,
            "flock-relay-shuttle-called", "flock-announcement-sender");

        _explosion.QueueExplosion(ent, "Default", 2000f, 4f, 200f, 1f, int.MaxValue, true, ent);

        // Brick every headset, noisily and with a slight cascading effect.
        var delay = TimeSpan.FromSeconds(2);
        var q = EntityQueryEnumerator<HeadsetComponent>();
        while (q.MoveNext(out var uid, out _))
        {
            delay += TimeSpan.FromMilliseconds(Random.Shared.NextDouble() < 0.3 ? 100 : 0);
            _radiosToBreak.Add((uid, _timing.CurTime + delay));
        }
    }

    private void BreakRadio(EntityUid headset)
    {
        if (!Exists(headset))
            return;
        _audio.PlayPvs(new SoundCollectionSpecifier("FlockRadioSweep"), headset);
        if (Transform(headset).ParentUid is { } wearer && HasComp<MobStateComponent>(wearer))
        {
            _popup.PopupEntity(Loc.GetString("flock-relay-radio-scream"), wearer, wearer, PopupType.LargeCaution);
            _stun.TryAddStunDuration(wearer, TimeSpan.FromSeconds(3));
        }
        RemComp<ActiveRadioComponent>(headset);
        RemComp<HeadsetComponent>(headset);
    }
}
