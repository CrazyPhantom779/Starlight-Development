using Content.Shared._Starlight.Holograms.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Examine;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Content.Shared._Starlight.Holograms.Components;

namespace Content.Shared._Starlight.Holograms.Systems;

public abstract partial class SharedHologramSystem : EntitySystem
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;

    public const string TagHardLight = "Hardlight";

    public override void Initialize()
    {
        base.Initialize();

        InitializeProjected();
        SubscribeLocalEvent<HologramComponent, ComponentStartup>(OnHologramStartup);
    }

    private void OnHologramStartup(EntityUid uid, HologramComponent component, ComponentStartup args)
    {
        if (TryComp<StealthComponent>(uid, out var stealth))
            _stealth.SetVisibility(uid, component.DefaultVisibility, stealth);
    }

    /// <summary>
    /// Holograms are solid hardlight mobs. Keep this helper so old call sites have
    /// a single place to ask permission without reintroducing pass-through logic.
    /// </summary>
    public bool HoloInteractionAllowed(EntityUid hologram, EntityUid? _, HologramComponent? holoComp = null)
        => Resolve(hologram, ref holoComp, logMissing: false);

    /// <summary>
    /// Kills a hologram after raising cancellation and notification events.
    /// </summary>
    public bool TryKillHologram(EntityUid hologram, HologramComponent? holoComp = null)
    {
        if (!Resolve(hologram, ref holoComp))
            return false;

        var killAttempt = new HologramKillAttemptEvent();
        RaiseLocalEvent(hologram, ref killAttempt);

        if (killAttempt.Cancelled)
            return false;

        RaiseLocalEvent(hologram, new HologramKilledEvent());
        DoKillHologram(hologram, holoComp);
        return true;
    }

    /// <summary>
    /// Kills a hologram, playing effects and deleting the entity on the server.
    /// The shared implementation is intentionally empty for clients.
    /// </summary>
    public virtual void DoKillHologram(EntityUid hologram, HologramComponent? holoComp = null)
    {
    }
}
