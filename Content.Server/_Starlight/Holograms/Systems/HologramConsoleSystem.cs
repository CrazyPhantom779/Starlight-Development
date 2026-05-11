using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Starlight.Holograms.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Moffstation.BladeServer;
using Content.Shared._Starlight.Holograms;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Holograms.Systems;

public sealed class HologramConsoleSystem : EntitySystem
{
    private const string PortableBladeSlot = "blade_server_slot";

    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly HologramSystem _hologram = default!;
    [Dependency] private readonly HologramBladeLawSystem _bladeLaws = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<HologramConsoleComponent, BoundUIClosedEvent>(OnUIClosed);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleProjectHologramMessage>(OnProjectHologram);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleRecallMessage>(OnRecallHologram);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleToggleCarryMessage>(OnToggleCarry);
        SubscribeLocalEvent<HologramConsoleComponent, PowerCellSlotEmptyEvent>(OnBatteryEmpty);
        SubscribeLocalEvent<HologramConsoleComponent, EntInsertedIntoContainerMessage>(OnBladeInserted);
        SubscribeLocalEvent<HologramConsoleComponent, EntRemovedFromContainerMessage>(OnBladeRemoved);
        SubscribeLocalEvent<HologramBladeServerComponent, ComponentShutdown>(OnBladeShutdown);
    }

    public bool IsPortable(EntityUid uid)
    {
        if (!HasComp<ItemComponent>(uid))
            return false;

        return _itemSlots.TryGetSlot(uid, PortableBladeSlot, out _);
    }

    public bool IsBatteryPowered(EntityUid uid) => HasComp<PowerCellSlotComponent>(uid);

    private void OnBatteryEmpty(EntityUid uid, HologramConsoleComponent component, ref PowerCellSlotEmptyEvent args)
    {
        if (!IsPortable(uid))
            return;

        KillAllPortableHolograms(component.ActiveHolograms);
        UpdateUserInterface(uid, component);
        UpdateBriefcaseAppearance(uid, component);
    }

    private void OnBladeShutdown(EntityUid uid, HologramBladeServerComponent component, ComponentShutdown args)
        => KillBladeHologram(uid, component);

    private void OnUIOpened(EntityUid uid, HologramConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdateUserInterface(uid, component);
        UpdateBriefcaseAppearance(uid, component);
    }

    private void OnUIClosed(EntityUid uid, HologramConsoleComponent component, BoundUIClosedEvent args)
        => UpdateBriefcaseAppearance(uid, component);

    private void OnBladeInserted(EntityUid uid, HologramConsoleComponent component, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != PortableBladeSlot)
            return;

        UpdateBriefcaseAppearance(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void OnBladeRemoved(EntityUid uid, HologramConsoleComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != PortableBladeSlot)
            return;

        if (component.ActiveHolograms.Remove(args.Entity, out var portableHologram) && Exists(portableHologram))
            _hologram.DoKillHologram(portableHologram);

        if (TryComp<HologramBladeServerComponent>(args.Entity, out var blade))
            KillBladeHologram(args.Entity, blade);

        UpdateBriefcaseAppearance(uid, component);
        UpdateUserInterface(uid, component);
    }

    private EntityUid? GetEffectiveGridUid(EntityUid uid)
    {
        var current = uid;

        while (Exists(current))
        {
            var xform = Transform(current);
            if (xform.GridUid is { } grid)
                return grid;

            if (xform.ParentUid == EntityUid.Invalid || xform.ParentUid == current)
                return null;

            current = xform.ParentUid;
        }

        return null;
    }

    private bool TryGetContainingRack(EntityUid uid, out EntityUid rackUid, out BladeServerRackComponent rack)
    {
        var current = uid;

        while (Exists(current))
        {
            var xform = Transform(current);
            if (xform.ParentUid == EntityUid.Invalid || xform.ParentUid == current)
                break;

            current = xform.ParentUid;

            if (!TryComp<BladeServerRackComponent>(current, out var rackComp))
                continue;

            rackUid = current;
            rack = rackComp;
            return true;
        }

        rackUid = default;
        rack = default!;
        return false;
    }

    private bool TryGetStoredMind(EntityUid? brainChip, HologramBrainChipComponent? brainComp, out EntityUid mind)
    {
        if (brainChip == null || brainComp == null)
        {
            mind = default;
            return false;
        }

        if (brainComp.HoloMind is { } storedMind && Exists(storedMind))
        {
            mind = storedMind;
            return true;
        }

        if (TryComp<MindContainerComponent>(brainChip.Value, out var mindContainer) &&
            mindContainer.Mind is { } containedMind &&
            Exists(containedMind))
        {
            brainComp.HoloMind = containedMind;
            mind = containedMind;
            return true;
        }

        brainComp.HoloMind = null;
        mind = default;
        return false;
    }

    private void UpdateBriefcaseAppearance(EntityUid uid, HologramConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component, logMissing: false))
            return;

        if (!IsPortable(uid) || !HasComp<AppearanceComponent>(uid))
            return;

        CleanupPortableHolograms(component.ActiveHolograms);

        var uiOpen = _ui.IsUiOpen(uid, HologramConsoleUiKey.Key);
        var hasActive = component.ActiveHolograms.Count > 0;

        var state = !uiOpen
            ? HologramBriefcaseState.Closed
            : hasActive
                ? HologramBriefcaseState.Active
                : HologramBriefcaseState.Open;

        var hasBlade = _itemSlots.GetItemOrNull(uid, PortableBladeSlot) != null;
        var showBlade = hasBlade && uiOpen;

        _appearance.SetData(uid, HologramBriefcaseVisuals.State, state);
        _appearance.SetData(uid, HologramBriefcaseVisuals.HasBlade, showBlade);
    }

    private void UpdateUserInterface(EntityUid console, HologramConsoleComponent? component = null)
    {
        if (!Resolve(console, ref component))
            return;

        if (!_ui.HasUi(console, HologramConsoleUiKey.Key))
            return;

        var isPortable = IsPortable(console);
        var consoleGrid = GetEffectiveGridUid(console);
        var bladeServerList = new List<BladeServerInfo>();
        NetEntity? firstActiveHologram = null;
        var activeCount = 0;

        foreach (var bladeServerUid in CollectBladeServers(console))
        {
            if (!TryGetBladeServerData(bladeServerUid, out var bladeComp, out var brainChip, out var brainComp, out var bodyChip, out var bodyComp))
                continue;

            CleanupBladeHologram(bladeComp);

            var hasMind = TryGetStoredMind(brainChip, brainComp, out _);
            if (!hasMind && bodyComp == null)
                continue;

            var isActive = bladeComp.ActiveHologram is { } activeHologram && Exists(activeHologram);
            NetEntity? activeNet = null;
            NetEntity? currentProjector = null;

            if (isActive && bladeComp.ActiveHologram is { } activeHologramUid)
            {
                activeNet = GetNetEntity(activeHologramUid);
                firstActiveHologram ??= activeNet;
                activeCount++;

                if (TryComp<HologramProjectedComponent>(activeHologramUid, out var projected))
                    currentProjector = projected.CurProjector;
            }

            var hasBody = bodyComp?.HologramPrototype != null;
            bladeServerList.Add(new BladeServerInfo(
                GetNetEntity(bladeServerUid),
                GetHologramName(brainChip, brainComp, bodyChip, bodyComp),
                isActive,
                hasBody,
                bladeComp.Emagged,
                activeNet,
                currentProjector));
        }

        var projectors = new List<ProjectorInfo>();
        var projectorCoordinates = new Dictionary<NetEntity, NetCoordinates>();

        if (!isPortable && consoleGrid is { } gridUid)
        {
            var query = EntityQueryEnumerator<HologramProjectorComponent>();
            while (query.MoveNext(out var projector, out var projectorComp))
            {
                if (!projectorComp.IsActive)
                    continue;

                var projectorXform = Transform(projector);
                if (projectorXform.GridUid != gridUid)
                    continue;

                if (HasComp<ItemComponent>(projector))
                    continue;

                var netEntity = GetNetEntity(projector);
                projectors.Add(new ProjectorInfo(netEntity, MetaData(projector).EntityName, GetProjectorLocation(projector, projectorXform)));
                projectorCoordinates[netEntity] = GetNetCoordinates(projectorXform.Coordinates);
            }
        }

        float? batteryPercent = null;
        if (IsBatteryPowered(console) &&
            _powerCell.TryGetBatteryFromSlot(console, out var batteryNullable) &&
            batteryNullable is { } battery)
        {
            var charge = _battery.GetCharge(battery.AsNullable());
            var maxCharge = battery.Comp.MaxCharge;
            batteryPercent = maxCharge > 0 ? charge / maxCharge * 100f : 0f;
        }

        var state = new HologramConsoleBoundUserInterfaceState(
            bladeServerList,
            firstActiveHologram,
            projectors,
            projectorCoordinates,
            isPortable,
            batteryPercent,
            component.AllowHologramCarry,
            activeCount,
            component.MaxActiveHolograms,
            bladeServerList.Count,
            component.ShowMap,
            component.ShowProjectButton,
            component.ShowRecallButton,
            component.ShowBladeServerPanel,
            isPortable || consoleGrid != null || HasComp<HologramBladeServerComponent>(console));

        _ui.SetUiState(console, HologramConsoleUiKey.Key, state);
    }

    private HashSet<EntityUid> CollectBladeServers(EntityUid console)
    {
        var bladeServers = new HashSet<EntityUid>();

        // If this UI belongs to a blade directly, it is the personal/action UI.
        // Only expose that blade to avoid projecting or recalling other occupants from your action.
        if (HasComp<HologramBladeServerComponent>(console))
        {
            bladeServers.Add(console);
            return bladeServers;
        }

        if (TryGetContainingRack(console, out _, out var containingRack))
        {
            foreach (var slot in containingRack.BladeSlots)
            {
                if (slot.Item is { } bladeServer && HasComp<HologramBladeServerComponent>(bladeServer))
                    bladeServers.Add(bladeServer);
            }

            return bladeServers;
        }

        if (IsPortable(console))
        {
            if (_itemSlots.GetItemOrNull(console, PortableBladeSlot) is { } portableBlade &&
                HasComp<HologramBladeServerComponent>(portableBlade))
            {
                bladeServers.Add(portableBlade);
            }

            return bladeServers;
        }

        var consoleGrid = GetEffectiveGridUid(console);
        if (consoleGrid == null)
            return bladeServers;

        var rackQuery = EntityQueryEnumerator<BladeServerRackComponent>();
        while (rackQuery.MoveNext(out var rackUid, out var rackComp))
        {
            if (GetEffectiveGridUid(rackUid) != consoleGrid)
                continue;

            foreach (var slot in rackComp.BladeSlots)
            {
                if (slot.Item is { } bladeServer && HasComp<HologramBladeServerComponent>(bladeServer))
                    bladeServers.Add(bladeServer);
            }
        }

        return bladeServers;
    }

    private bool TryGetBladeServerData(
        EntityUid bladeServer,
        [NotNullWhen(true)] out HologramBladeServerComponent? bladeComp,
        out EntityUid? brainChip,
        out HologramBrainChipComponent? brainComp,
        out EntityUid? bodyChip,
        out HologramBodyChipComponent? bodyComp)
    {
        bladeComp = null;
        brainChip = null;
        brainComp = null;
        bodyChip = null;
        bodyComp = null;

        if (!TryComp<HologramBladeServerComponent>(bladeServer, out var bladeServerComp))
            return false;

        if (!IsBladeServerPowered(bladeServer))
            return false;

        if (!TryComp<ItemSlotsComponent>(bladeServer, out var itemSlots))
            return false;

        if (_itemSlots.TryGetSlot(bladeServer, bladeServerComp.BrainChipSlot, out var brainSlot, itemSlots) &&
            brainSlot.Item is { } brainChipUid &&
            TryComp<HologramBrainChipComponent>(brainChipUid, out var brainChipComp))
        {
            brainChip = brainChipUid;
            brainComp = brainChipComp;
        }

        if (_itemSlots.TryGetSlot(bladeServer, bladeServerComp.BodyChipSlot, out var bodySlot, itemSlots) &&
            bodySlot.Item is { } bodyChipUid &&
            TryComp<HologramBodyChipComponent>(bodyChipUid, out var bodyChipComp))
        {
            bodyChip = bodyChipUid;
            bodyComp = bodyChipComp;
        }

        bladeComp = bladeServerComp;
        return brainComp != null || bodyComp != null;
    }

    private string GetHologramName(EntityUid? brainChip, HologramBrainChipComponent? brainComp, EntityUid? bodyChip, HologramBodyChipComponent? bodyComp)
    {
        if (TryGetStoredMind(brainChip, brainComp, out var holoMind) && TryComp<MindComponent>(holoMind, out var mindComp))
            return mindComp.CharacterName ?? "Unknown";

        if (!string.IsNullOrWhiteSpace(bodyComp?.HologramName))
            return bodyComp.HologramName;

        if (bodyChip != null)
            return MetaData(bodyChip.Value).EntityName;

        return "Missing Body";
    }

    private bool IsBladeServerPowered(EntityUid bladeServerUid)
    {
        if (TryGetContainingRack(bladeServerUid, out var rackUid, out var rackComp))
        {
            if (!TryComp<ApcPowerReceiverComponent>(rackUid, out var rackPower) || !rackPower.Powered)
                return false;

            foreach (var slot in rackComp.BladeSlots)
            {
                if (slot.Item == bladeServerUid)
                    return slot.IsPowerEnabled;
            }

            return false;
        }

        if (TryComp<ApcPowerReceiverComponent>(bladeServerUid, out var ownPower))
            return ownPower.Powered;

        return false;
    }

    private string GetProjectorLocation(EntityUid projector, TransformComponent xform)
    {
        var coords = _transform.GetMapCoordinates(projector, xform);

        if (xform.GridUid is { } gridUid)
        {
            var gridName = MetaData(gridUid).EntityName;
            return $"{gridName} ({xform.Coordinates.X:F0}, {xform.Coordinates.Y:F0})";
        }

        return $"({coords.X:F0}, {coords.Y:F0})";
    }

    private void OnProjectHologram(EntityUid console, HologramConsoleComponent component, HologramConsoleProjectHologramMessage args)
    {
        var bladeServer = GetEntity(args.BladeServerUid);
        if (!Exists(bladeServer) || !TryGetBladeServerData(bladeServer, out var bladeComp, out var brainChip, out var brainChipComp, out _, out var bodyChipComp))
        {
            Logger.Warning($"Hologram projection failed: invalid, unpowered, or incomplete blade server {bladeServer}.");
            _popup.PopupEntity("Projection failed: invalid, unpowered, or incomplete blade server.", console);
            return;
        }

        if (bodyChipComp?.HologramPrototype == null)
        {
            Logger.Warning($"Hologram projection failed: blade server {ToPrettyString(bladeServer)} has no body chip/prototype.");
            _popup.PopupEntity("Projection failed: no body chip is installed.", console);
            UpdateUserInterface(console, component);
            return;
        }

        var hasMind = TryGetStoredMind(brainChip, brainChipComp, out var mind);
        var bodyOnly = !hasMind;

        if (IsPortable(console))
        {
            CleanupPortableHolograms(component.ActiveHolograms);
            CleanupBladeHologram(bladeComp);

            if (bladeComp.ActiveHologram is { } active && Exists(active))
                return;

            if (component.ActiveHolograms.Count >= component.MaxActiveHolograms)
                return;

            if (IsBatteryPowered(console) &&
                (!_powerCell.TryGetBatteryFromSlot(console, out var batteryNullable) ||
                 batteryNullable is not { } battery ||
                 _battery.GetCharge(battery.AsNullable()) <= 0))
                return;

            if (!TrySpawnHologram(hasMind ? mind : null, bodyChipComp, Transform(console).Coordinates, out var hologram))
            {
                Logger.Warning($"Hologram projection failed: could not spawn prototype {bodyChipComp.HologramPrototype}.");
                _popup.PopupEntity("Projection failed: could not spawn the configured hologram body.", console);
                return;
            }

            component.ActiveHolograms[bladeServer] = hologram;
            bladeComp.ActiveHologram = hologram;
            SetProjection(hologram, console, true);
            _bladeLaws.ApplyBladeLaws(bladeServer, bladeComp, hologram);

            if (bodyOnly)
                _popup.PopupEntity("No mind detected. Projecting an autonomous hardlight body.", console);
        }
        else
        {
            var projector = GetEntity(args.ProjectorUid);
            if (!Exists(projector) || !HasComp<HologramProjectorComponent>(projector))
            {
                Logger.Warning("Hologram projection failed: no valid projector selected.");
                _popup.PopupEntity("Projection failed: select a projector first.", console);
                return;
            }

            var consoleGrid = GetEffectiveGridUid(console);
            if (consoleGrid == null || consoleGrid != Transform(projector).GridUid)
            {
                Logger.Warning("Hologram projection failed: selected projector is not on the console/blade grid.");
                _popup.PopupEntity("Projection failed: selected projector is not on this grid.", console);
                return;
            }

            CleanupBladeHologram(bladeComp);

            var activeCount = CountActiveSameGridBladeHolograms(console);
            if (bladeComp.ActiveHologram is { } existing && Exists(existing))
            {
                // Moving projections is treated as a clean unproject + reproject.
                // This gives a fresh, healed body and prevents carried items/damage from following projectors.
                if (!ReprojectHologram(bladeServer, bladeComp, existing, brainChip, bodyChipComp, Transform(projector).Coordinates, projector, false, out _))
                {
                    Logger.Warning($"Hologram reprojection failed: could not refresh prototype {bodyChipComp.HologramPrototype}.");
                    _popup.PopupEntity("Reprojection failed: could not refresh the configured hologram body.", console);
                }

                UpdateUserInterface(console, component);
                UpdateBriefcaseAppearance(console, component);
                return;
            }

            if (component.MaxActiveHolograms > 0 && activeCount >= component.MaxActiveHolograms)
                return;

            if (!TrySpawnHologram(hasMind ? mind : null, bodyChipComp, Transform(projector).Coordinates, out var hologram))
            {
                Logger.Warning($"Hologram projection failed: could not spawn prototype {bodyChipComp.HologramPrototype}.");
                _popup.PopupEntity("Projection failed: could not spawn the configured hologram body.", console);
                return;
            }

            bladeComp.ActiveHologram = hologram;
            SetProjection(hologram, projector, false);
            _bladeLaws.ApplyBladeLaws(bladeServer, bladeComp, hologram);

            if (bodyOnly)
                _popup.PopupEntity("No mind detected. Projecting an autonomous hardlight body.", projector);
        }

        UpdateUserInterface(console, component);
        UpdateBriefcaseAppearance(console, component);
    }


    private bool ReprojectHologram(
        EntityUid bladeServerUid,
        HologramBladeServerComponent bladeComp,
        EntityUid oldHologram,
        EntityUid? brainChip,
        HologramBodyChipComponent bodyChipComp,
        EntityCoordinates coords,
        EntityUid projector,
        bool lockToProjector,
        out EntityUid hologram)
    {
        hologram = default;

        EntityUid? mind = null;
        if (TryComp<MindContainerComponent>(oldHologram, out var oldMind) && oldMind.Mind is { } oldMindUid)
            mind = oldMindUid;

        if (mind != null && brainChip is { } chip && Exists(chip))
            _hologram.TryReturnMindToBrainChip(oldHologram, chip);

        _hologram.DoKillHologram(oldHologram);
        bladeComp.ActiveHologram = null;

        if (!TrySpawnHologram(mind, bodyChipComp, coords, out hologram))
            return false;

        bladeComp.ActiveHologram = hologram;
        SetProjection(hologram, projector, lockToProjector);
        _bladeLaws.ApplyBladeLaws(bladeServerUid, bladeComp, hologram);
        return true;
    }

    private bool TrySpawnHologram(
        EntityUid? mind,
        HologramBodyChipComponent bodyChipComp,
        EntityCoordinates coords,
        out EntityUid hologram)
    {
        hologram = default;

        if (mind is { } mindUid)
        {
            if (!_hologram.TryGenerateHologram(mindUid, bodyChipComp, coords, out var generatedHologram) ||
                generatedHologram is not { } generated)
                return false;

            hologram = generated;
            return true;
        }

        if (bodyChipComp.HologramPrototype == null)
            return false;

        hologram = Spawn(bodyChipComp.HologramPrototype, coords);
        EnsureComp<HologramComponent>(hologram);
        EnsureComp<HologramProjectedComponent>(hologram);
        return true;
    }

    private void SetProjection(EntityUid hologram, EntityUid projector, bool lockToProjector)
    {
        if (!TryComp<HologramProjectedComponent>(hologram, out var projectedComp))
            return;

        var netProjector = GetNetEntity(projector);
        projectedComp.CurProjector = netProjector;
        projectedComp.ProjectorOverride = lockToProjector ? netProjector : null;
        projectedComp.CurrentlyInProjector = true;
        projectedComp.VanishTime = TimeSpan.Zero;
        Dirty(hologram, projectedComp);
    }

    private void OnRecallHologram(EntityUid console, HologramConsoleComponent component, HologramConsoleRecallMessage args)
    {
        if (args.BladeServerUid is { } bladeServerNetEntity)
        {
            var bladeServer = GetEntity(bladeServerNetEntity);
            if (TryComp<HologramBladeServerComponent>(bladeServer, out var bladeComp))
                KillBladeHologram(bladeServer, bladeComp);
        }
        else if (IsPortable(console))
        {
            KillAllPortableHolograms(component.ActiveHolograms);
        }
        else
        {
            foreach (var bladeServer in CollectBladeServers(console))
            {
                if (TryComp<HologramBladeServerComponent>(bladeServer, out var bladeComp))
                    KillBladeHologram(bladeServer, bladeComp);
            }
        }

        UpdateUserInterface(console, component);
        UpdateBriefcaseAppearance(console, component);
    }

    private void OnToggleCarry(EntityUid uid, HologramConsoleComponent component, HologramConsoleToggleCarryMessage args)
    {
        if (!IsPortable(uid))
            return;

        component.AllowHologramCarry = args.AllowCarry;
        UpdateUserInterface(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var bladeQuery = EntityQueryEnumerator<HologramBladeServerComponent>();
        while (bladeQuery.MoveNext(out _, out var bladeComp))
        {
            CleanupBladeHologram(bladeComp);
        }

        var query = EntityQueryEnumerator<HologramConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!IsPortable(uid))
                continue;

            CleanupPortableHolograms(component.ActiveHolograms);

            if (component.ActiveHolograms.Count == 0)
                continue;

            if (!IsBatteryPowered(uid))
                continue;

            var draw = component.PowerDrawPerHologram * component.ActiveHolograms.Count * frameTime;
            if (_powerCell.TryUseCharge(uid, draw))
                continue;

            KillAllPortableHolograms(component.ActiveHolograms);
            UpdateBriefcaseAppearance(uid, component);
            UpdateUserInterface(uid, component);
        }
    }

    private int CountActiveSameGridBladeHolograms(EntityUid console)
    {
        var count = 0;
        foreach (var bladeServer in CollectBladeServers(console))
        {
            if (!TryComp<HologramBladeServerComponent>(bladeServer, out var bladeComp))
                continue;

            CleanupBladeHologram(bladeComp);

            if (bladeComp.ActiveHologram is { } hologram && Exists(hologram))
                count++;
        }

        return count;
    }

    private void CleanupBladeHologram(HologramBladeServerComponent bladeComp)
    {
        if (bladeComp.ActiveHologram is not { } hologram || Exists(hologram))
            return;

        bladeComp.ActiveHologram = null;
    }

    private void KillBladeHologram(EntityUid bladeServerUid, HologramBladeServerComponent bladeComp)
    {
        if (bladeComp.ActiveHologram is { } hologram && Exists(hologram))
            ReturnMindAndKill(bladeServerUid, bladeComp, hologram);

        bladeComp.ActiveHologram = null;
    }

    private void ReturnMindAndKill(EntityUid bladeServerUid, HologramBladeServerComponent bladeComp, EntityUid hologram)
    {
        if (TryGetBrainChip(bladeServerUid, bladeComp, out var brainChip))
        {
            _hologram.TryReturnMindToBrainChip(hologram, brainChip);
            _bladeLaws.ApplyBladeLaws(bladeServerUid, bladeComp, brainChip);
        }

        _hologram.DoKillHologram(hologram);
    }

    private bool TryGetBrainChip(EntityUid bladeServerUid, HologramBladeServerComponent bladeComp, out EntityUid brainChip)
    {
        brainChip = default;

        if (!TryComp<ItemSlotsComponent>(bladeServerUid, out var slots))
            return false;

        if (!_itemSlots.TryGetSlot(bladeServerUid, bladeComp.BrainChipSlot, out var brainSlot, slots) ||
            brainSlot.Item is not { } chip)
        {
            return false;
        }

        brainChip = chip;
        return true;
    }

    private void CleanupPortableHolograms(Dictionary<EntityUid, EntityUid> activeHolograms)
    {
        foreach (var (blade, hologram) in activeHolograms.ToArray())
        {
            if (Exists(blade) && Exists(hologram))
                continue;

            activeHolograms.Remove(blade);
        }
    }

    private void KillAllPortableHolograms(Dictionary<EntityUid, EntityUid> activeHolograms)
    {
        foreach (var (blade, hologram) in activeHolograms.ToArray())
        {
            if (!Exists(hologram))
                continue;

            if (Exists(blade) && TryComp<HologramBladeServerComponent>(blade, out var bladeComp))
                ReturnMindAndKill(blade, bladeComp, hologram);
            else
                _hologram.DoKillHologram(hologram);
        }

        activeHolograms.Clear();
    }
}
