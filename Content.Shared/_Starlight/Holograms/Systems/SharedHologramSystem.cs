using Content.Shared.Interaction.Events;
using Content.Shared.Tag;
using Content.Shared.Popups;
using Content.Shared.Administration.Logs;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Events;
using Robust.Shared.Audio.Systems;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Whitelist;
using Content.Shared.Examine;

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
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    private void HologramComponentStartup(EntityUid uid, HologramComponent comp, ComponentStartup args)
    {
        if (TryComp<Stealth.Components.StealthComponent>(uid, out var stealth))
            _stl.SetVisibility(uid, 0.8f, stealth);
    }

    [Dependency] private readonly Stealth.SharedStealthSystem _stl = default!;
    public const string TagHardLight = "Hardlight";
    public const string TagHoloMapped = "HoloMapped"; // TODO: HOLO

    private const string PopupHoloInteractionFail = "holo-interaction-fail";
    private const string PopupInteractionWithHoloFail = "interaction-with-holo-fail";

    public override void Initialize()
    {
        SubscribeLocalEvent<HologramComponent, InteractionAttemptEvent>(OnHoloInteractionAttempt);
        SubscribeLocalEvent<HologramComponent, GettingInteractedWithAttemptEvent>(OnInteractionWithHoloAttempt);
        SubscribeLocalEvent<HologramComponent, PreventCollideEvent>(OnHoloCollide);

        InitializeProjected();
        SubscribeLocalEvent<HologramComponent, ComponentStartup>(HologramComponentStartup);
    }

    private void OnHoloInteractionAttempt(EntityUid uid, HologramComponent component, InteractionAttemptEvent args)
    {
        // Hardlight holograms are currently allowed to interact normally.
    }

    private void OnInteractionWithHoloAttempt(EntityUid uid, HologramComponent component, GettingInteractedWithAttemptEvent args)
    {
        // Other entities are currently allowed to interact with holograms normally.
    }

    private void OnHoloCollide(EntityUid uid, HologramComponent component, ref PreventCollideEvent args)
    {
        if (HoloInteractionAllowed(args.OurEntity, args.OtherEntity, component))
            return;

        args.Cancelled = true;
    }

    /// <summary>
    ///     Validates an interaction between two possibly-hologramatic entities.
    /// </summary>
    /// <param name="hologram">This should be the hologramatic entity, if one is known.</param>
    /// <param name="potential">This entity can be anything, a null value will return true.</param>
    /// <returns>True if both entities are holograms, or if either is hardlight. A null entity will return true.</returns>
    public bool HoloInteractionAllowed(EntityUid hologram, EntityUid? potential, HologramComponent? holoComp = null)
    {
        if (potential is not { } potentialUid)
            return true;

        if (!Resolve(hologram, ref holoComp))
            return false;

        return _tag.HasTag(hologram, TagHardLight) ||
            _tag.HasTag(potentialUid, TagHardLight) ||
            HasComp<HologramComponent>(potentialUid) ||
            _whitelist.IsValid(holoComp.CollideWhitelist, potentialUid);
    }

    /// <summary>
    ///     Kills a Hologram after playing the visual and auditory effects.
    /// </summary>
    /// <remarks>
    ///     Note that the effects of killing a Hologram are not predicted.
    /// </remarks>
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
    ///     Kills a Hologram, playing the effects and deleting the entity.
    /// </summary>
    /// <remarks>
    ///     This function does nothing if called on the client.
    /// </remarks>
    public virtual void DoKillHologram(EntityUid hologram, HologramComponent? holoComp = null) { } // The killing is dealt with server-side, due to mind component.
}
