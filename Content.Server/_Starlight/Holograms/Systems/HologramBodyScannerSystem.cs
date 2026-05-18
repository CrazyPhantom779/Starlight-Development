using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Writes scanned mind/body data onto hologram chips used on an occupied body scanner.
/// </summary>
public sealed partial class HologramBodyScannerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramBodyScannerComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, HologramBodyScannerComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_timing.CurTime < component.LastScanTime + component.ScanDelay)
        {
            _popup.PopupEntity("The scanner is still processing the last scan!", uid, args.User);
            return;
        }

        if (!TryGetScannedEntity(uid, out var scannedEntity))
        {
            _popup.PopupEntity("The scanner is empty!", uid, args.User);
            return;
        }

        var hasBrainChip = TryComp<HologramBrainChipComponent>(args.Used, out var brainChip);
        var hasBodyChip = TryComp<HologramBodyChipComponent>(args.Used, out var bodyChip);

        if (!hasBrainChip && !hasBodyChip)
        {
            _popup.PopupEntity("Use a hologram mind chip or body chip on the occupied scanner.", uid, args.User);
            return;
        }

        EntityUid mindToTransfer = default;
        var transferMind = false;

        if (hasBrainChip && brainChip != null)
        {
            if (!CanWriteBrainChip(uid, scannedEntity, args.Used, brainChip, args.User, out mindToTransfer))
                return;

            transferMind = true;
        }

        if (hasBodyChip && bodyChip != null &&
            !CanWriteBodyChip(uid, bodyChip, args.User))
        {
            return;
        }

        // Body data is captured before moving the mind away from the scanned entity.  This keeps
        // combined mind+body chips from half-writing a mind if body capture fails.
        if (hasBodyChip && bodyChip != null)
            WriteBodyChip(scannedEntity, bodyChip);

        if (transferMind && brainChip != null)
            WriteBrainChip(args.Used, brainChip, mindToTransfer);

        component.LastScanTime = _timing.CurTime;
        args.Handled = true;

        _popup.PopupEntity(GetSuccessMessage(hasBrainChip, hasBodyChip), uid, args.User);
    }

    private bool CanWriteBrainChip(
        EntityUid scanner,
        EntityUid scannedEntity,
        EntityUid chip,
        HologramBrainChipComponent brainChip,
        EntityUid user,
        out EntityUid mindId)
    {
        mindId = default;

        if (brainChip.PreventMindStorage)
        {
            _popup.PopupEntity("This chip cannot store a consciousness.", scanner, user);
            return false;
        }

        if (brainChip.HoloMind is { } storedMind && Exists(storedMind))
        {
            _popup.PopupEntity("This hologram mind chip already contains a consciousness.", scanner, user);
            return false;
        }

        if (TryComp<MindContainerComponent>(chip, out var chipMind) && chipMind.Mind != null)
        {
            _popup.PopupEntity("This hologram mind chip is already occupied.", scanner, user);
            return false;
        }

        if (!_mobState.IsDead(scannedEntity))
        {
            _popup.PopupEntity("The scanner can only transfer consciousness from a dead body.", scanner, user);
            return false;
        }

        if (!TryComp<MindContainerComponent>(scannedEntity, out var mindContainer) || mindContainer.Mind is not { } scannedMind)
        {
            _popup.PopupEntity("The scanner cannot detect a consciousness to transfer!", scanner, user);
            return false;
        }

        mindId = scannedMind;
        return true;
    }

    private bool CanWriteBodyChip(EntityUid scanner, HologramBodyChipComponent bodyChip, EntityUid user)
    {
        if (!bodyChip.HasStoredBodyData)
            return true;

        _popup.PopupEntity("This hologram body chip already contains body data.", scanner, user);
        return false;
    }

    private void WriteBrainChip(EntityUid chip, HologramBrainChipComponent brainChip, EntityUid mindId)
    {
        EnsureComp<MindContainerComponent>(chip);

        _mind.UnVisit(mindId);
        _mind.TransferTo(mindId, chip, ghostCheckOverride: true);
        brainChip.HoloMind = mindId;
    }

    private void WriteBodyChip(EntityUid scannedEntity, HologramBodyChipComponent bodyChip)
    {
        var meta = MetaData(scannedEntity);

        bodyChip.SourceBody = scannedEntity;
        bodyChip.HologramName = meta.EntityName;
        bodyChip.HologramPrototype = ResolveBodyPrototype(scannedEntity);

        if (TryComp<HumanoidAppearanceComponent>(scannedEntity, out var appearance))
            bodyChip.HologramProfile = _humanoid.GetBaseProfile((scannedEntity, appearance));
    }

    private EntProtoId ResolveBodyPrototype(EntityUid scannedEntity)
    {
        if (TryComp<HumanoidAppearanceComponent>(scannedEntity, out var appearance))
        {
            var profile = _humanoid.GetBaseProfile((scannedEntity, appearance));

            return !string.IsNullOrWhiteSpace(profile?.ForcedPrototype)
                ? new EntProtoId(profile.ForcedPrototype)
                : HologramSystem.DefaultHologramPrototype;
        }

        if (MetaData(scannedEntity).EntityPrototype is { } prototype)
            return new EntProtoId(prototype.ID);

        return HologramSystem.DefaultHologramPrototype;
    }

    private string GetSuccessMessage(bool wroteBrain, bool wroteBody)
    {
        if (wroteBrain && wroteBody)
            return "Mind and body data saved to the hologram chip.";

        return wroteBrain
            ? "Mind data saved to the hologram mind chip."
            : "Body data saved to the hologram body chip.";
    }

    private bool TryGetScannedEntity(EntityUid uid, out EntityUid scannedEntity)
    {
        scannedEntity = default;

        if (!TryComp<ContainerManagerComponent>(uid, out var containerManager))
            return false;

        foreach (var container in containerManager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                scannedEntity = contained;
                return true;
            }
        }

        return false;
    }
}
