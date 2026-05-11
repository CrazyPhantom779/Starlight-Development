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
using Content.Shared.Mind.Components;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Silicons.Laws.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Content.Server._Starlight.Holograms;
using Content.Shared.Mind;

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
        var xform = Transform(uid);
        if (xform.GridUid is { } grid)
            return grid;

        var parent = xform.ParentUid;
        if (parent == EntityUid.Invalid || !Exists(parent))
            return null;

        return Transform(parent).GridUid;
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
        var bladeServerList = new List<BladeServerInfo>();
        NetEntity? firstActiveHologram = null;
        var activeCount = 0;

        foreach (var bladeServerUid in CollectBladeServers(console))
        {
            if (!TryGetBladeServerData(bladeServerUid, out var bladeComp, out var brainComp, out var bodyChip, out var bodyComp))
                continue;

            if (brainComp.HoloMind == null && bodyComp == null)
                continue;

            CleanupBladeHologram(bladeComp);

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
                GetHologramName(brainComp, bodyChip, bodyComp),
                isActive,
                hasBody,
                bladeComp.Emagged,
                activeNet,
                currentProjector));
        }

        var projectors = new List<ProjectorInfo>();
        var projectorCoordinates = new Dictionary<NetEntity, NetCoordinates>();

        var consoleGrid = GetEffectiveGridUid(console);

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

        if (HasComp<HologramBladeServerComponent>(console))
        {
            bladeServers.Add(console);
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
            if (Transform(rackUid).GridUid != consoleGrid)
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
        [NotNullWhen(true)] out HologramBrainChipComponent? brainComp,
        out EntityUid? bodyChip,
        out HologramBodyChipComponent? bodyComp)
    {
        bladeComp = null;
        brainComp = null;
        bodyChip = null;
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

        if (!TryComp<HologramBrainChipComponent>(brainChip, out var brainChipComp))
            return false;

        if (_itemSlots.TryGetSlot(bladeServer, bladeServerComp.BodyChipSlot, out var bodySlot, itemSlots) &&
            bodySlot.Item is { } bodyChipUid &&
            TryComp<HologramBodyChipComponent>(bodyChipUid, out var bodyChipComp))
        {
            bodyChip = bodyChipUid;
            bodyComp = bodyChipComp;
        }

        bladeComp = bladeServerComp;
        brainComp = brainChipComp;
        return true;
    }

    private string GetHologramName(HologramBrainChipComponent brainComp, EntityUid? bodyChip, HologramBodyChipComponent? bodyComp)
    {
        if (brainComp.HoloMind is { } holoMind && TryComp<MindComponent>(holoMind, out var mindComp))
            return mindComp.CharacterName ?? "Unknown";

        if (!string.IsNullOrWhiteSpace(bodyComp?.HologramName))
            return bodyComp.HologramName;

        if (bodyChip != null)
            return MetaData(bodyChip.Value).EntityName;

        return "Missing Body";
    }

    private bool IsBladeServerPowered(EntityUid bladeServerUid)
    {
        if (TryComp<ApcPowerReceiverComponent>(bladeServerUid, out var ownPower))
            return ownPower.Powered;

        var parent = Transform(bladeServerUid).ParentUid;
        if (parent == EntityUid.Invalid)
            return false;

        if (!TryComp<BladeServerRackComponent>(parent, out var rackComp))
            return false;

        if (!TryComp<ApcPowerReceiverComponent>(parent, out var rackPower) || !rackPower.Powered)
            return false;

        foreach (var slot in rackComp.BladeSlots)
        {
            if (slot.Item == bladeServerUid)
                return slot.IsPowerEnabled;
        }

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
        if (!Exists(bladeServer) || !TryGetBladeServerData(bladeServer, out var bladeComp, out var brainChipComp, out _, out var bodyChipComp))
            return;

        if (brainChipComp.HoloMind == null)
            return;

        if (bodyChipComp?.HologramPrototype == null)
        {
            UpdateUserInterface(console, component);
            return;
        }

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

            if (!TrySpawnHologram(brainChipComp, bodyChipComp, Transform(console).Coordinates, out var hologram))
                return;

            component.ActiveHolograms[bladeServer] = hologram;
            bladeComp.ActiveHologram = hologram;
            SetProjection(hologram, console, true);
            _bladeLaws.ApplyBladeLaws(bladeServer, bladeComp, hologram);
        }
        else
        {
            var projector = GetEntity(args.ProjectorUid);
            if (!Exists(projector) || !HasComp<HologramProjectorComponent>(projector))
                return;

            var consoleGrid = GetEffectiveGridUid(console);
            if (consoleGrid == null || consoleGrid != Transform(projector).GridUid)
                return;

            CleanupBladeHologram(bladeComp);

            var activeCount = CountActiveSameGridBladeHolograms(console);
            if (bladeComp.ActiveHologram is { } existing && Exists(existing))
            {
                _hologram.MoveHologramToProjector(existing, projector);
                SetProjection(existing, projector, false);
                _bladeLaws.ApplyBladeLaws(bladeServer, bladeComp, existing);
                UpdateUserInterface(console, component);
                return;
            }

            if (component.MaxActiveHolograms > 0 && activeCount >= component.MaxActiveHolograms)
                return;

            if (!TrySpawnHologram(brainChipComp, bodyChipComp, Transform(projector).Coordinates, out var hologram))
                return;

            bladeComp.ActiveHologram = hologram;
            SetProjection(hologram, projector, false);
            _bladeLaws.ApplyBladeLaws(bladeServer, bladeComp, hologram);
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

        if (brainChipComp.HoloMind is { } mind)
        {
            if (!_hologram.TryGenerateHumanoidHologram(mind, bodyChipComp, coords, out var generatedHologram) ||
                generatedHologram is not { } generated)
                return false;

            hologram = generated;
            return true;
        }

        if (bodyChipComp.HologramPrototype == null)
            return false;

        hologram = Spawn(bodyChipComp.HologramPrototype, coords);
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
