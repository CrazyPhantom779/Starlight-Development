using Content.Shared.Administration.Logs;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Stealth;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Content.Shared.Stealth.Components;

namespace Content.Shared._Starlight.Holograms;

public abstract partial class SharedHologramSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;

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
            _stealth.SetVisibility(uid, 0.8f, stealth);
    }

    /// <summary>
    /// Holograms are currently solid hardlight-style mobs.
    /// Keep this helper for callers that still ask whether interaction is allowed.
    /// </summary>
    public bool HoloInteractionAllowed(EntityUid _, EntityUid? potential, HologramComponent? holoComp = null)
        => true;

    /// <summary>
    /// Kills a hologram after playing the visual and auditory effects.
    /// </summary>
    public bool TryKillHologram(EntityUid hologram, HologramComponent? holoComp = null)
    {
        if (!Resolve(hologram, ref holoComp))
            return false;

        var killedEvent = new HologramKillAttemptEvent();
        RaiseLocalEvent(hologram, ref killedEvent);

        if (killedEvent.Cancelled)
            return false;

        DoKillHologram(hologram, holoComp);
        return true;
    }

    /// <summary>
    /// Kills a hologram, playing the effects and deleting the entity.
    /// This function does nothing if called on the client.
    /// </summary>
    public virtual void DoKillHologram(EntityUid hologram, HologramComponent? holoComp = null)
    {
    }
}
