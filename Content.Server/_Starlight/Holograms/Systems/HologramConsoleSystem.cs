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
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Holograms.Systems;

public sealed partial class HologramConsoleSystem : EntitySystem
{
    private const string PortableBladeSlot = "blade_server_slot";

    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private HologramSystem _hologram = default!;
    [Dependency] private HologramBladeLawSystem _bladeLaws = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("hologram.console");

        SubscribeLocalEvent<HologramConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<HologramConsoleComponent, BoundUIClosedEvent>(OnUIClosed);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleProjectHologramMessage>(OnProjectHologram);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleRecallMessage>(OnRecallHologram);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleEjectBladeServerMessage>(OnEjectBladeServer);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleToggleCarryMessage>(OnToggleCarry);
        SubscribeLocalEvent<HologramConsoleComponent, PowerCellSlotEmptyEvent>(OnBatteryEmpty);
        SubscribeLocalEvent<HologramConsoleComponent, EntInsertedIntoContainerMessage>(OnBladeInserted);
        SubscribeLocalEvent<HologramConsoleComponent, EntRemovedFromContainerMessage>(OnBladeRemoved);
        SubscribeLocalEvent<HologramBladeServerComponent, ComponentShutdown>(OnBladeShutdown);
        SubscribeLocalEvent<HologramComponent, MobStateChangedEvent>(OnHologramMobStateChanged);
    }

    public bool IsPortable(EntityUid uid)
        => HasComp<ItemComponent>(uid) && TryComp<ItemSlotsComponent>(uid, out var itemSlots) && _itemSlots.TryGetSlot(uid, PortableBladeSlot, out _, itemSlots);

    public bool IsBatteryPowered(EntityUid uid) => HasComp<PowerCellSlotComponent>(uid);

    private void OnHologramMobStateChanged(EntityUid uid, HologramComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState is not (MobState.Critical or MobState.Dead))
            return;

        var query = EntityQueryEnumerator<HologramBladeServerComponent>();
        while (query.MoveNext(out var bladeUid, out var blade))
        {
            if (blade.ActiveHologram != uid)
                continue;

            KillBladeHologram(bladeUid, blade);
            return;
        }

        if (Exists(uid))
            _hologram.DoKillHologram(uid, component);
    }

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

        component.ActiveHolograms.Remove(args.Entity);

        if (TryComp<HologramBladeServerComponent>(args.Entity, out var blade))
            KillBladeHologram(args.Entity, blade);

        UpdateBriefcaseAppearance(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void OnEjectBladeServer(EntityUid console, HologramConsoleComponent component, HologramConsoleEjectBladeServerMessage args)
    {
        if (!IsPortable(console))
            return;

        var bladeServer = GetEntity(args.BladeServerUid);

        if (!CollectBladeServers(console).Contains(bladeServer))
            return;

        if (_itemSlots.GetItemOrNull(console, PortableBladeSlot) != bladeServer)
            return;

        if (_itemSlots.TryGetSlot(console, PortableBladeSlot, out var slot))
            _itemSlots.TryEject(console, slot, user: null, out _);

        UpdateUserInterface(console, component);
        UpdateBriefcaseAppearance(console, component);
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
        var hasVirtualBlade = TryGetVirtualBlade(console, out _);
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

            var hasBody = bodyComp != null;
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
        if (isPortable &&
            IsBatteryPowered(console) &&
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
            isPortable || consoleGrid != null || HasComp<HologramBladeServerComponent>(console) || hasVirtualBlade);

        _ui.SetUiState(console, HologramConsoleUiKey.Key, state);
    }

    private HashSet<EntityUid> CollectBladeServers(EntityUid console)
    {
        var bladeServers = new HashSet<EntityUid>();

        if (TryGetVirtualBlade(console, out var virtualBlade))
        {
            bladeServers.Add(virtualBlade);
            return bladeServers;
        }

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

    private bool TryGetVirtualBlade(EntityUid console, out EntityUid bladeServer)
    {
        bladeServer = default;

        if (!TryComp<HologramConsoleActionComponent>(console, out var action) ||
            action.BladeServer is not { } linkedBlade ||
            !Exists(linkedBlade) ||
            !HasComp<HologramBladeServerComponent>(linkedBlade))
        {
            return false;
        }

        bladeServer = linkedBlade;
        return true;
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

        if (!CollectBladeServers(console).Contains(bladeServer))
        {
            _sawmill.Warning($"Hologram projection failed: blade server {bladeServer} is not available from console {ToPrettyString(console)}.");
            _popup.PopupEntity("Projection failed: that blade server is not available from this console.", console);
            UpdateConsoleIfAlive(console, component);
            return;
        }

        if (!Exists(bladeServer) ||
            !TryGetBladeServerData(bladeServer, out var bladeComp, out var brainChip, out var brainChipComp, out _, out var bodyChipComp))
        {
            _sawmill.Warning($"Hologram projection failed: invalid, unpowered, or incomplete blade server {bladeServer}.");
            _popup.PopupEntity("Projection failed: invalid, unpowered, or incomplete blade server.", console);
            UpdateConsoleIfAlive(console, component);
            return;
        }

        if (bodyChipComp == null)
        {
            _sawmill.Warning($"Hologram projection failed: blade server {ToPrettyString(bladeServer)} has no body chip.");
            _popup.PopupEntity("Projection failed: no body chip is installed.", console);
            UpdateConsoleIfAlive(console, component);
            return;
        }

        var hasMind = TryGetStoredMind(brainChip, brainChipComp, out var mind);
        var autonomousProjection = !hasMind && brainChipComp?.AllowAutonomousProjection == true;

        if (!hasMind && !autonomousProjection)
        {
            _popup.PopupEntity("Projection failed: no mind chip is installed.", console);
            UpdateConsoleIfAlive(console, component);
            return;
        }

        EntityUid projector;
        EntityCoordinates coords;
        var lockToProjector = false;

        if (IsPortable(console))
        {
            projector = console;
            coords = Transform(console).Coordinates;
            lockToProjector = true;

            CleanupPortableHolograms(component.ActiveHolograms);

            if (component.MaxActiveHolograms > 0 &&
                component.ActiveHolograms.Count >= component.MaxActiveHolograms &&
                bladeComp.ActiveHologram is not { })
            {
                _popup.PopupEntity("Projection failed: this console is already at its active hologram limit.", console);
                return;
            }

            if (IsBatteryPowered(console) &&
                (!_powerCell.TryGetBatteryFromSlot(console, out var batteryNullable) ||
                 batteryNullable is not { } battery ||
                 _battery.GetCharge(battery.AsNullable()) <= 0))
            {
                _popup.PopupEntity("Projection failed: the portable projector has no charge.", console);
                return;
            }
        }
        else
        {
            projector = GetEntity(args.ProjectorUid);
            if (!TryValidateProjector(console, projector))
            {
                _sawmill.Warning("Hologram projection failed: no valid same-grid projector selected.");
                _popup.PopupEntity("Projection failed: select a valid same-grid projector.", console);
                UpdateConsoleIfAlive(console, component);
                return;
            }

            var activeCount = CountActiveSameGridBladeHolograms(console);
            if (component.MaxActiveHolograms > 0 &&
                bladeComp.ActiveHologram is not { } &&
                activeCount >= component.MaxActiveHolograms)
            {
                _popup.PopupEntity("Projection failed: this console is already at its active hologram limit.", console);
                return;
            }

            coords = Transform(projector).Coordinates;
        }

        if (bladeComp.ActiveHologram is { } existing && Exists(existing))
        {
            if (!ReprojectHologram(bladeServer, bladeComp, existing, brainChip, brainChipComp, bodyChipComp, coords, projector, lockToProjector, out var refreshed))
            {
                _sawmill.Warning($"Hologram reprojection failed: could not refresh prototype {bodyChipComp.HologramPrototype}.");
                _popup.PopupEntity("Reprojection failed: could not refresh the configured hologram body.", console);
                return;
            }

            if (IsPortable(console))
                component.ActiveHolograms[bladeServer] = refreshed;

            UpdateConsoleIfAlive(console, component);
            return;
        }

        EntityUid hologram;

        if (autonomousProjection)
        {
            hologram = _hologram.SpawnAutonomousHologram(bodyChipComp, coords);
        }
        else if (!TrySpawnHologram(mind, bodyChipComp, coords, out hologram))
        {
            _sawmill.Warning($"Hologram projection failed: could not spawn prototype {bodyChipComp.HologramPrototype}.");
            _popup.PopupEntity("Projection failed: could not spawn the configured hologram body.", console);
            return;
        }

        if (IsPortable(console))
            component.ActiveHolograms[bladeServer] = hologram;

        bladeComp.ActiveHologram = hologram;
        SetProjection(hologram, projector, lockToProjector);
        _bladeLaws.ApplyBladeLaws(bladeServer, bladeComp, hologram);

        UpdateConsoleIfAlive(console, component);
    }

    private bool TryValidateProjector(EntityUid console, EntityUid projector)
    {
        if (!Exists(projector) || !TryComp<HologramProjectorComponent>(projector, out var projectorComp))
            return false;

        if (!projectorComp.IsActive)
            return false;

        if (HasComp<ItemComponent>(projector) || HasComp<HologramComponent>(projector))
            return false;

        var consoleGrid = GetEffectiveGridUid(console);
        if (consoleGrid == null)
            return false;

        if (Transform(projector).GridUid != consoleGrid)
            return false;

        return true;
    }

    private bool ReprojectHologram(
        EntityUid bladeServerUid,
        HologramBladeServerComponent bladeComp,
        EntityUid oldHologram,
        EntityUid? brainChip,
        HologramBrainChipComponent? brainChipComp,
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

        var autonomousProjection = mind == null && brainChipComp?.AllowAutonomousProjection == true;

        if (mind is { } mindUid && brainChip is { } chip && Exists(chip))
            _hologram.TryReturnMindToBrainChip(oldHologram, chip);
        else if (mind == null && !autonomousProjection)
        {
            _hologram.DoKillHologram(oldHologram);
            bladeComp.ActiveHologram = null;
            return false;
        }

        _hologram.DoKillHologram(oldHologram);
        bladeComp.ActiveHologram = null;

        if (autonomousProjection)
        {
            hologram = _hologram.SpawnAutonomousHologram(bodyChipComp, coords);
        }
        else if (mind is { } mindUidToProject)
        {
            if (!TrySpawnHologram(mindUidToProject, bodyChipComp, coords, out hologram))
                return false;
        }
        else
        {
            return false;
        }

        bladeComp.ActiveHologram = hologram;
        SetProjection(hologram, projector, lockToProjector);
        _bladeLaws.ApplyBladeLaws(bladeServerUid, bladeComp, hologram);
        return true;
    }

    private bool TrySpawnHologram(
        EntityUid mind,
        HologramBodyChipComponent bodyChipComp,
        EntityCoordinates coords,
        out EntityUid hologram)
    {
        hologram = default;

        if (!_hologram.TryGenerateHologram(mind, bodyChipComp, coords, out var generatedHologram) ||
            generatedHologram is not { } generated)
            return false;

        hologram = generated;
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
            if (!CollectBladeServers(console).Contains(bladeServer))
                return;

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

        UpdateConsoleIfAlive(console, component);
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

    public void KillBladeHologram(EntityUid bladeServerUid, HologramBladeServerComponent bladeComp)
    {
        if (bladeComp.ActiveHologram is { } hologram && Exists(hologram))
            ReturnMindAndKill(bladeServerUid, bladeComp, hologram);

        bladeComp.ActiveHologram = null;
        RemovePortableHologramEntry(bladeServerUid);
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

    private void RemovePortableHologramEntry(EntityUid bladeServerUid)
    {
        var query = EntityQueryEnumerator<HologramConsoleComponent>();
        while (query.MoveNext(out var consoleUid, out var console))
        {
            if (!IsPortable(consoleUid))
                continue;

            if (!console.ActiveHolograms.Remove(bladeServerUid))
                continue;

            UpdateBriefcaseAppearance(consoleUid, console);
            UpdateUserInterface(consoleUid, console);
        }
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

    private void UpdateConsoleIfAlive(EntityUid console, HologramConsoleComponent component)
    {
        if (!Exists(console) || Terminating(console))
            return;

        UpdateUserInterface(console, component);
        UpdateBriefcaseAppearance(console, component);
    }
}
