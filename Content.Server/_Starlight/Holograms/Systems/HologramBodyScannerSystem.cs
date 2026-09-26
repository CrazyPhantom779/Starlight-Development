using Content.Server._Starlight.Holograms.Components;
using Content.Server.EUI;
using Content.Shared._Starlight.Holograms;
using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;
using Content.Shared.Mind;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Writes scanned mind/body data onto hologram chips used on an occupied body scanner.
/// Scanned bodies only ever copy components named in the scan settings' allowlist -
/// see HologramScanSettingsPrototype.Components. Nothing else can get through.
///
/// Pulling in a mind needs the scanned player's consent - same deal as cloning. If they're
/// online, the transfer waits on the AcceptHologramScanEui prompt instead of happening
/// immediately. Body data isn't gated the same way; it's just an appearance snapshot.
/// </summary>
public sealed partial class HologramBodyScannerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private EuiManager _euiManager = default!;

    private readonly ISawmill _sawmill = default!;

    [SubscribeLocalEvent]
    private void OnInteractUsing(EntityUid uid, HologramBodyScannerComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_timing.CurTime < component.LastScanTime + component.ScanDelay)
        {
            _popup.PopupEntity(Loc.GetString("hologram-scanner-cooldown"), uid, args.User);
            return;
        }

        if (!_prototype.Resolve(component.Settings, out var scanSettings))
        {
            _popup.PopupEntity(Loc.GetString("hologram-scanner-invalid-settings"), uid, args.User);
            return;
        }

        if (!TryGetScannedEntity(uid, out var scannedEntity))
        {
            _popup.PopupEntity(Loc.GetString("hologram-scanner-empty"), uid, args.User);
            return;
        }

        var hasBrainChip = TryComp<HologramBrainChipComponent>(args.Used, out var brainChip);
        var hasBodyChip = TryComp<HologramBodyChipComponent>(args.Used, out var bodyChip);

        if (!hasBrainChip && !hasBodyChip)
        {
            _popup.PopupEntity(Loc.GetString("hologram-scanner-need-chip"), uid, args.User);
            return;
        }

        EntityUid mindToTransfer = default;
        MindComponent? mindComp = null;
        var transferMind = false;

        if (hasBrainChip && brainChip != null)
        {
            if (!CanWriteBrainChip(uid, scannedEntity, args.Used, brainChip, args.User, out mindToTransfer))
                return;

            transferMind = TryComp(mindToTransfer, out mindComp);
        }

        if (hasBodyChip && bodyChip != null &&
            !CanWriteBodyChip(uid, bodyChip, args.User))
        {
            return;
        }

        // Body data is captured before moving the mind away from the scanned entity.
        if (hasBodyChip && bodyChip != null)
            WriteBodyChip(scannedEntity, bodyChip, scanSettings);

        component.LastScanTime = _timing.CurTime;
        args.Handled = true;

        if (transferMind && mindComp != null &&
            mindComp.UserId is { } userId && _playerManager.TryGetSessionById(userId, out var session))
        {
            _euiManager.OpenEui(new AcceptHologramScanEui(mindToTransfer, mindComp, uid, args.Used, this), session);
            _popup.PopupEntity(Loc.GetString("hologram-scanner-waiting-consent"), uid, args.User);
            return;
        }

        // No one to ask (offline/no player attached to that mind) - just take it, same as
        // cloning falls back to when it can't track down a session either.
        if (transferMind && brainChip != null)
            WriteBrainChip(args.Used, brainChip, mindToTransfer);

        _popup.PopupEntity(GetSuccessMessage(hasBrainChip, hasBodyChip), uid, args.User);
    }

    /// <summary>
    /// Called by AcceptHologramScanEui when the scanned person accepts. The chip and its owning
    /// mind might not exist anymore by now (scanner moved, chip destroyed, player ghosted) -
    /// everything here re-checks rather than trusting what was true when the prompt opened.
    /// </summary>
    public void OnScanConsentAccepted(EntityUid mindId, MindComponent _, EntityUid scanner, EntityUid chip)
    {
        if (!Exists(chip) || !TryComp<HologramBrainChipComponent>(chip, out var brainChip))
            return;

        WriteBrainChip(chip, brainChip, mindId);
        _popup.PopupEntity(Loc.GetString("hologram-scanner-mind-captured-target"), chip, chip);

        if (Exists(scanner))
            _popup.PopupEntity(Loc.GetString("hologram-scanner-success-mind"), scanner, scanner);
    }

    /// <summary>
    /// Called by AcceptHologramScanEui when the scanned person declines, or just closes the
    /// prompt. Nothing was written yet, so there's nothing to undo - just let them know.
    /// </summary>
    public void OnScanConsentDenied(EntityUid scanner, EntityUid _)
    {
        if (Exists(scanner))
            _popup.PopupEntity(Loc.GetString("hologram-scanner-consent-denied"), scanner, scanner);
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
            _popup.PopupEntity(Loc.GetString("hologram-scanner-chip-cant-store-mind"), scanner, user);
            return false;
        }

        if (brainChip.HoloMind is { } storedMind && Exists(storedMind))
        {
            _popup.PopupEntity(Loc.GetString("hologram-scanner-chip-already-has-mind"), scanner, user);
            return false;
        }

        if (TryComp<MindContainerComponent>(chip, out var chipMind) && chipMind.Mind != null)
        {
            _popup.PopupEntity(Loc.GetString("hologram-scanner-chip-occupied"), scanner, user);
            return false;
        }

        if (!_mobState.IsDead(scannedEntity))
        {
            _popup.PopupEntity(Loc.GetString("hologram-scanner-target-not-dead"), scanner, user);
            return false;
        }

        if (!TryComp<MindContainerComponent>(scannedEntity, out var mindContainer) || mindContainer.Mind is not { } scannedMind)
        {
            _popup.PopupEntity(Loc.GetString("hologram-scanner-no-mind-detected"), scanner, user);
            return false;
        }

        mindId = scannedMind;
        return true;
    }

    private bool CanWriteBodyChip(EntityUid scanner, HologramBodyChipComponent bodyChip, EntityUid user)
    {
        if (!bodyChip.HasStoredBodyData)
            return true;

        _popup.PopupEntity(Loc.GetString("hologram-scanner-body-chip-occupied"), scanner, user);
        return false;
    }

    private void WriteBrainChip(EntityUid chip, HologramBrainChipComponent brainChip, EntityUid mindId)
    {
        EnsureComp<MindContainerComponent>(chip);

        _mind.UnVisit(mindId);
        _mind.TransferTo(mindId, chip, ghostCheckOverride: true);
        brainChip.HoloMind = mindId;
    }

    private void WriteBodyChip(EntityUid scannedEntity, HologramBodyChipComponent bodyChip, HologramScanSettingsPrototype scanSettings)
    {
        var meta = MetaData(scannedEntity);

        bodyChip.SourceBody = scannedEntity;
        bodyChip.HologramName = meta.EntityName;
        bodyChip.HologramProfile = null;
        bodyChip.ScanSettings = scanSettings.ID;

        var isHumanoid = TryComp<HumanoidAppearanceComponent>(scannedEntity, out var appearance);

        if (isHumanoid)
        {
            bodyChip.HologramProfile = _humanoid.GetBaseProfile((scannedEntity, appearance!));
            bodyChip.HologramPrototype = scanSettings.HumanoidProjectionPrototype;
        }

        var scannedData = new HologramScannedBodyData
        {
            Name = meta.EntityName,
            SourcePrototype = meta.EntityPrototype is { } sourcePrototype
                ? (EntProtoId?) new EntProtoId(sourcePrototype.ID)
                : null,
            ProjectionPrototype = isHumanoid ? scanSettings.HumanoidProjectionPrototype : scanSettings.ScannedProjectionPrototype,
        };

        CaptureConfiguredComponents(scannedEntity, scanSettings, scannedData);
        bodyChip.ScannedBody = scannedData;

        if (!isHumanoid)
            bodyChip.HologramPrototype = scannedData.ProjectionPrototype;
    }

    private void CaptureConfiguredComponents(
        EntityUid scannedEntity,
        HologramScanSettingsPrototype scanSettings,
        HologramScannedBodyData scannedData)
    {
        foreach (var componentName in scanSettings.Components)
        {
            if (!TryCopyComponentByName(scannedEntity, componentName, out var copy))
                continue;

            scannedData.ComponentCopies[componentName] = copy;
        }
    }

    private bool TryCopyComponentByName(EntityUid source, string componentName, out Component copy)
    {
        copy = default!;

        try
        {
            var registration = _componentFactory.GetRegistration(componentName);

            if (!TryComp(source, registration.Type, out var sourceComponent))
                return false;

            copy = (Component) _serialization.CreateCopy(sourceComponent, notNullableOverride: true);
            return true;
        }
        catch (Exception exc)
        {
            _sawmill.Warning($"Unable to copy hologram scanned component {componentName} from {ToPrettyString(source)}: {exc.Message}");
            return false;
        }
    }

    private string GetSuccessMessage(bool wroteBrain, bool wroteBody)
        => wroteBrain && wroteBody
            ? Loc.GetString("hologram-scanner-success-both")
            : wroteBrain
            ? Loc.GetString("hologram-scanner-success-mind")
            : Loc.GetString("hologram-scanner-success-body");

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
