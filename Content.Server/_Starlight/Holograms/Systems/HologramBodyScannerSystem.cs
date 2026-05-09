using Content.Server.Mind;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Writes scanned mind/body data onto hologram chips used on an occupied body scanner.
/// </summary>
public sealed class HologramBodyScannerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    private static readonly EntProtoId _defaultHologramPrototype = "MobHologramHardlight";

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

        var wroteData = false;

        if (TryComp<HologramBrainChipComponent>(args.Used, out var brainChip))
        {
            if (!TryComp<MindContainerComponent>(scannedEntity, out var mindContainer) ||
                mindContainer.Mind is not { } mindId)
            {
                _popup.PopupEntity("The scanner cannot detect a consciousness to transfer!", uid, args.User);
                return;
            }

            brainChip.HoloMind = mindId;
            _mind.TransferTo(mindId, args.Used, ghostCheckOverride: true);
            _mind.UnVisit(mindId);

            _popup.PopupEntity("Mind data saved to the hologram mind chip.", uid, args.User);
            wroteData = true;
        }

        if (TryComp<HologramBodyChipComponent>(args.Used, out var bodyChip))
        {
            bodyChip.HologramName = MetaData(scannedEntity).EntityName;
            bodyChip.HologramPrototype = _defaultHologramPrototype;

            _popup.PopupEntity("Body data saved to the hologram body chip.", uid, args.User);
            wroteData = true;
        }

        if (!wroteData)
        {
            _popup.PopupEntity("Use a hologram mind chip or body chip on the occupied scanner.", uid, args.User);
            return;
        }

        component.LastScanTime = _timing.CurTime;
        args.Handled = true;
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

