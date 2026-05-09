using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Starlight.Holograms.Components;
using Content.Server.Mind;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Holograms;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Power;
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
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly HologramSystem _hologram = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<HologramConsoleComponent, BoundUIClosedEvent>(OnUIClosed);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleProjectHologramMessage>(OnProjectHologram);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleRecallMessage>(OnRecallHologram);
        SubscribeLocalEvent<HologramConsoleComponent, HologramConsoleToggleCarryMessage>(OnToggleCarry);
        SubscribeLocalEvent<HologramConsoleComponent, PowerCellSlotEmptyEvent>(OnBatteryEmpty);
        SubscribeLocalEvent<HologramConsoleComponent, PowerChangedEvent>(OnConsolePowerChanged);
        SubscribeLocalEvent<HologramConsoleComponent, EntInsertedIntoContainerMessage>(OnBladeInserted);
        SubscribeLocalEvent<HologramConsoleComponent, EntRemovedFromContainerMessage>(OnBladeRemoved);
        SubscribeLocalEvent<HologramServerComponent, ComponentRemove>(OnServerRemoved);
    }

    public bool IsPortable(EntityUid uid) => HasComp<ItemComponent>(uid);
    public bool IsBatteryPowered(EntityUid uid) => HasComp<PowerCellSlotComponent>(uid);

    private void OnBatteryEmpty(EntityUid uid, HologramConsoleComponent component, ref PowerCellSlotEmptyEvent args)
    {
        if (!IsPortable(uid))
            return;

        KillAllHolograms(component.ActiveHolograms);
        UpdateUserInterface(uid, component);
        UpdateBriefcaseAppearance(uid, component);
    }

    private void OnConsolePowerChanged(EntityUid uid, HologramConsoleComponent component, ref PowerChangedEvent args)
    {
        if (IsPortable(uid) || args.Powered)
            return;

        if (TryGetLinkedServer(uid, component, out _, out var serverComp))
            KillAllHolograms(serverComp.ActiveHolograms);

        UpdateUserInterface(uid, component);
    }

    private void OnServerRemoved(EntityUid uid, HologramServerComponent component, ComponentRemove args)
    {
        KillAllHolograms(component.ActiveHolograms);

        if (component.LinkedHologram is { } linkedHologram && Exists(linkedHologram))
            _hologram.DoKillHologram(linkedHologram);

        component.LinkedHologram = null;
    }

    private void OnUIOpened(EntityUid uid, HologramConsoleComponent component, BoundUIOpenedEvent args)
    {
        EnsureLinkedServer(uid, component);
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

        if (component.ActiveHolograms.Remove(args.Entity, out var hologram) && Exists(hologram))
            _hologram.DoKillHologram(hologram);

        UpdateBriefcaseAppearance(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void EnsureLinkedServer(EntityUid console, HologramConsoleComponent component)
    {
        if (IsPortable(console))
            return;

        if (component.LinkedServer is { } linkedServer &&
            Exists(linkedServer) &&
            HasComp<HologramServerComponent>(linkedServer))
            return;

        component.LinkedServer = null;

        foreach (var nearby in _lookup.GetEntitiesInRange(Transform(console).Coordinates, component.SearchRange))
        {
            if (!HasComp<HologramServerComponent>(nearby))
                continue;

            component.LinkedServer = nearby;
            return;
        }
    }

    private bool TryGetLinkedServer(
        EntityUid console,
        HologramConsoleComponent component,
        out EntityUid server,
        [NotNullWhen(true)] out HologramServerComponent? serverComp)
    {
        EnsureLinkedServer(console, component);

        if (component.LinkedServer is not { } linkedServer ||
            !TryComp<HologramServerComponent>(linkedServer, out var linkedServerComp))
        {
            server = default;
            serverComp = null;
            return false;
        }

        server = linkedServer;
        serverComp = linkedServerComp;
        return true;
    }

    private void UpdateBriefcaseAppearance(EntityUid uid, HologramConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component, logMissing: false))
            return;

        if (!IsPortable(uid) || !HasComp<AppearanceComponent>(uid))
            return;

        CleanupHolograms(component.ActiveHolograms);

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

        EnsureLinkedServer(console, component);

        var isPortable = IsPortable(console);
        HologramServerComponent? serverComp = null;
        Dictionary<EntityUid, EntityUid> activeHolograms;

        if (isPortable)
        {
            activeHolograms = component.ActiveHolograms;
        }
        else if (TryGetLinkedServer(console, component, out _, out serverComp))
        {
            activeHolograms = serverComp.ActiveHolograms;
            MigrateLegacyLinkedHologram(serverComp);
        }
        else
        {
            activeHolograms = [];
        }

        CleanupHolograms(activeHolograms);

        var bladeServerList = new List<BladeServerInfo>();
        NetEntity? firstActiveHologram = null;

        foreach (var bladeServerUid in CollectBladeServers(console, component))
        {
            if (!TryGetBladeServerData(bladeServerUid, out _, out var brainComp, out var bodyChip, out var bodyComp))
                continue;

            if (brainComp.HoloMind == null && bodyComp.HologramPrototype == null)
                continue;

            var isActive = activeHolograms.TryGetValue(bladeServerUid, out var activeHologram) && Exists(activeHologram);
            NetEntity? activeNet = null;
            NetEntity? currentProjector = null;

            if (isActive)
            {
                activeNet = GetNetEntity(activeHologram);
                firstActiveHologram ??= activeNet;

                if (TryComp<HologramProjectedComponent>(activeHologram, out var projected))
                    currentProjector = projected.CurProjector;
            }

            bladeServerList.Add(new BladeServerInfo(
                GetNetEntity(bladeServerUid),
                GetHologramName(brainComp, bodyChip, bodyComp),
                isActive,
                activeNet,
                currentProjector));
        }

        var projectors = new List<ProjectorInfo>();
        var projectorCoordinates = new Dictionary<NetEntity, NetCoordinates>();

        if (!isPortable && _station.GetOwningStation(console) is { } station)
        {
            var query = EntityQueryEnumerator<HologramProjectorComponent, TransformComponent>();
            while (query.MoveNext(out var projector, out _, out var xform))
            {
                if (_station.GetOwningStation(projector, xform) != station)
                    continue;

                if (HasComp<ItemComponent>(projector))
                    continue;

                var netEntity = GetNetEntity(projector);
                projectors.Add(new ProjectorInfo(netEntity, MetaData(projector).EntityName, GetProjectorLocation(projector, xform)));
                projectorCoordinates[netEntity] = GetNetCoordinates(xform.Coordinates);
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
            activeHolograms.Count,
            component.MaxActiveHolograms,
            bladeServerList.Count,
            component.ShowMap,
            component.ShowProjectButton,
            component.ShowRecallButton,
            component.ShowBladeServerPanel,
            isPortable || component.LinkedServer != null);

        _ui.SetUiState(console, HologramConsoleUiKey.Key, state);
    }

    private HashSet<EntityUid> CollectBladeServers(EntityUid console, HologramConsoleComponent component)
    {
        var bladeServers = new HashSet<EntityUid>();

        if (IsPortable(console) && _itemSlots.GetItemOrNull(console, PortableBladeSlot) is { } portableBlade)
        {
            if (HasComp<HologramBladeServerComponent>(portableBlade))
                bladeServers.Add(portableBlade);
        }

        var entitiesInRange = _lookup.GetEntitiesInRange(Transform(console).Coordinates, component.BladeServerScanRange);
        foreach (var entity in entitiesInRange)
        {
            if (HasComp<HologramBladeServerComponent>(entity))
            {
                bladeServers.Add(entity);
                continue;
            }

            if (TryComp<Content.Shared._Moffstation.BladeServer.BladeServerRackComponent>(entity, out _) &&
                TryComp<ItemSlotsComponent>(entity, out var rackSlots))
            {
                foreach (var slot in rackSlots.Slots.Values)
                {
                    if (slot.Item is { } bladeServer && HasComp<HologramBladeServerComponent>(bladeServer))
                        bladeServers.Add(bladeServer);
                }
            }
        }

        return bladeServers;
    }

    private bool TryGetBladeServerData(
        EntityUid bladeServer,
        [NotNullWhen(true)] out HologramBladeServerComponent? bladeComp,
        [NotNullWhen(true)] out HologramBrainChipComponent? brainComp,
        out EntityUid bodyChip,
        [NotNullWhen(true)] out HologramBodyChipComponent? bodyComp)
    {
        bladeComp = null;
        brainComp = null;
        bodyChip = default;
        bodyComp = null;

        if (!TryComp<HologramBladeServerComponent>(bladeServer, out var bladeServerComp))
            return false;

        if (!IsBladeServerPowered(bladeServer))
            return false;

        if (!TryComp<ItemSlotsComponent>(bladeServer, out var itemSlots))
            return false;

        if (!_itemSlots.TryGetSlot(bladeServer, bladeServerComp.BrainChipSlot, out var brainSlot, itemSlots) ||
            brainSlot.Item is not { } brainChip)
            return false;

        if (!_itemSlots.TryGetSlot(bladeServer, bladeServerComp.BodyChipSlot, out var bodySlot, itemSlots) ||
            bodySlot.Item is not { } bodyChipUid)
            return false;

        if (!TryComp<HologramBrainChipComponent>(brainChip, out var brainChipComp))
            return false;

        if (!TryComp<HologramBodyChipComponent>(bodyChipUid, out var bodyChipComp))
            return false;

        bladeComp = bladeServerComp;
        brainComp = brainChipComp;
        bodyChip = bodyChipUid;
        bodyComp = bodyChipComp;
        return true;
    }

    private string GetHologramName(HologramBrainChipComponent brainComp, EntityUid bodyChip, HologramBodyChipComponent bodyComp)
        => brainComp.HoloMind is { } holoMind && TryComp<MindComponent>(holoMind, out var mindComp)
            ? mindComp.CharacterName ?? "Unknown"
            : !string.IsNullOrWhiteSpace(bodyComp.HologramName)
            ? bodyComp.HologramName
            : bodyComp.HologramPrototype != null ? MetaData(bodyChip).EntityName : "Unknown";

    private bool IsBladeServerPowered(EntityUid bladeServerUid)
        => (TryComp<ApcPowerReceiverComponent>(bladeServerUid, out var powerReceiver)
            && powerReceiver.Powered)
            || (TryComp(bladeServerUid, out TransformComponent? xform) && xform.ParentUid != EntityUid.Invalid
                && TryComp<ApcPowerReceiverComponent>(xform.ParentUid, out var rackPower)
                && rackPower.Powered);

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
        if (!Exists(bladeServer) || !TryGetBladeServerData(bladeServer, out _, out var brainChipComp, out _, out var bodyChipComp))
            return;

        if (brainChipComp.HoloMind == null && bodyChipComp.HologramPrototype == null)
            return;

        if (IsPortable(console))
        {
            if (component.ActiveHolograms.ContainsKey(bladeServer))
                return;

            CleanupHolograms(component.ActiveHolograms);
            if (component.ActiveHolograms.Count >= component.MaxActiveHolograms)
                return;

            if (IsBatteryPowered(console) &&
                (!_powerCell.TryGetBatteryFromSlot(console, out var batteryNullable) ||
                 batteryNullable is not { } battery ||
                 _battery.GetCharge(battery.AsNullable()) <= 0))
                return;

            if (!TrySpawnHologram(brainChipComp, bodyChipComp, Transform(console).Coordinates, out var hologram))
                return;

            component.ActiveHolograms[bladeServer] = hologram;
            SetProjection(hologram, console, true);
        }
        else
        {
            if (!TryGetLinkedServer(console, component, out _, out var serverComp))
                return;

            var projector = GetEntity(args.ProjectorUid);
            if (!Exists(projector) || !HasComp<HologramProjectorComponent>(projector))
                return;

            if (_station.GetOwningStation(console) != _station.GetOwningStation(projector))
                return;

            CleanupHolograms(serverComp.ActiveHolograms);
            MigrateLegacyLinkedHologram(serverComp);

            if (serverComp.ActiveHolograms.TryGetValue(bladeServer, out var existing) && Exists(existing))
            {
                _hologram.MoveHologramToProjector(existing, projector);
                SetProjection(existing, projector, false);
                UpdateUserInterface(console, component);
                return;
            }

            if (serverComp.ActiveHolograms.Count >= component.MaxActiveHolograms)
                return;

            if (!TrySpawnHologram(brainChipComp, bodyChipComp, Transform(projector).Coordinates, out var hologram))
                return;

            serverComp.ActiveHolograms[bladeServer] = hologram;
            serverComp.LinkedHologram = hologram;
            SetProjection(hologram, projector, false);
        }

        UpdateUserInterface(console, component);
        UpdateBriefcaseAppearance(console, component);
    }

    private bool TrySpawnHologram(
        HologramBrainChipComponent brainChipComp,
        HologramBodyChipComponent bodyChipComp,
        EntityCoordinates coords,
        out EntityUid hologram)
    {
        hologram = default;

        if (brainChipComp.HoloMind is { } mind &&
            _hologram.TryGenerateHumanoidHologram(mind, coords, out var generatedHologram) &&
            generatedHologram is { } generated)
        {
            hologram = generated;
            return true;
        }

        if (bodyChipComp.HologramPrototype == null)
            return false;

        hologram = Spawn(bodyChipComp.HologramPrototype, coords);

        if (brainChipComp.HoloMind is { } mindId)
        {
            _mind.TransferTo(mindId, hologram, ghostCheckOverride: true);
            _mind.UnVisit(mindId);
        }

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
        Dictionary<EntityUid, EntityUid>? activeHolograms;

        if (IsPortable(console))
            activeHolograms = component.ActiveHolograms;
        else if (TryGetLinkedServer(console, component, out _, out var serverComp))
            activeHolograms = serverComp.ActiveHolograms;
        else
            return;

        if (args.BladeServerUid is { } bladeServerNetEntity)
        {
            var bladeServer = GetEntity(bladeServerNetEntity);
            if (activeHolograms.Remove(bladeServer, out var hologram) && Exists(hologram))
                _hologram.DoKillHologram(hologram);
        }
        else
        {
            KillAllHolograms(activeHolograms);
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

        var query = EntityQueryEnumerator<HologramConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (IsPortable(uid))
            {
                CleanupHolograms(component.ActiveHolograms);

                if (component.ActiveHolograms.Count == 0)
                    continue;

                if (IsBatteryPowered(uid))
                {
                    var draw = component.PowerDrawPerHologram * component.ActiveHolograms.Count * frameTime;
                    if (!_powerCell.TryUseCharge(uid, draw))
                    {
                        KillAllHolograms(component.ActiveHolograms);
                        UpdateBriefcaseAppearance(uid, component);
                        UpdateUserInterface(uid, component);
                    }
                }
            }
            else if (TryGetLinkedServer(uid, component, out _, out var serverComp))
            {
                CleanupHolograms(serverComp.ActiveHolograms);
            }
        }
    }

    private void CleanupHolograms(Dictionary<EntityUid, EntityUid> activeHolograms)
    {
        var removed = false;
        foreach (var (blade, hologram) in activeHolograms.ToArray())
        {
            if (Exists(blade) && Exists(hologram))
                continue;

            activeHolograms.Remove(blade);
            removed = true;
        }

        if (!removed)
            return;
    }

    private void KillAllHolograms(Dictionary<EntityUid, EntityUid> activeHolograms)
    {
        foreach (var hologram in activeHolograms.Values)
        {
            if (Exists(hologram))
                _hologram.DoKillHologram(hologram);
        }

        activeHolograms.Clear();
    }

    private void MigrateLegacyLinkedHologram(HologramServerComponent serverComp)
    {
        if (serverComp.LinkedHologram is not { } linkedHologram || !Exists(linkedHologram))
        {
            serverComp.LinkedHologram = null;
            return;
        }

        if (serverComp.ActiveHolograms.ContainsValue(linkedHologram))
            return;

        var bladeServer = serverComp.ActiveHolograms.Keys.FirstOrDefault();
        if (bladeServer != default)
            serverComp.ActiveHolograms[bladeServer] = linkedHologram;
    }
}
