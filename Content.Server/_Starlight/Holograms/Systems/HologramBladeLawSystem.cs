using Content.Server._Starlight.Holograms.Components;
using Content.Server.Silicons.Laws;
using Content.Shared._Moffstation.BladeServer;
using Content.Shared._Starlight.Holograms;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared._Starlight.Holograms.Events;
using Content.Shared.Actions.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Emag.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Keeps hologram laws and the hologram console link tied to the blade server that houses the mind.
/// Action entities are granted by HologramActionGrantSystem from YAML-driven HologramActionGrantComponent.
/// </summary>
public sealed partial class HologramBladeLawSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SiliconLawSystem _laws = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private HologramConsoleSystem _console = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("hologram.blade");

        SubscribeLocalEvent<HologramBladeServerComponent, GotEmaggedEvent>(OnBladeEmagged);
        SubscribeLocalEvent<HologramBladeServerComponent, EntInsertedIntoContainerMessage>(OnBladeSlotChanged);
        SubscribeLocalEvent<HologramBladeServerComponent, EntRemovedFromContainerMessage>(OnBladeSlotChanged);
        SubscribeLocalEvent<BladeServerRackComponent, GotEmaggedEvent>(OnRackEmagged);
        SubscribeLocalEvent<HologramBrainChipComponent, MindAddedMessage>(OnBrainMindAdded);
        SubscribeLocalEvent<HologramBrainChipComponent, MindRemovedMessage>(OnBrainMindRemoved);
        SubscribeLocalEvent<HologramBrainChipComponent, HologramOpenConsoleActionEvent>(OnOpenConsoleFromBrain);
        SubscribeLocalEvent<HologramComponent, HologramOpenConsoleActionEvent>(OnOpenConsoleFromProjection);
        SubscribeLocalEvent<HologramBladeLawProviderComponent, GetSiliconLawsEvent>(OnGetBladeLaws, before: [typeof(SiliconLawSystem)]);
    }

    public void SyncBladeLawsToOccupants(EntityUid bladeUid, HologramBladeServerComponent? blade = null)
    {
        if (!Resolve(bladeUid, ref blade, false))
            return;

        if (TryGetBrainChip(bladeUid, blade, out var brainChip))
            ApplyBladeLaws(bladeUid, blade, brainChip);

        if (blade.ActiveHologram is { } hologram && Exists(hologram))
            ApplyBladeLaws(bladeUid, blade, hologram);
    }

    public void ApplyBladeLaws(EntityUid bladeUid, HologramBladeServerComponent _, EntityUid target)
    {
        var lawProvider = EnsureComp<HologramBladeLawProviderComponent>(target);
        lawProvider.BladeServer = bladeUid;

        EnsureComp<SiliconLawBoundComponent>(target);
        EnsureComp<SiliconLawProviderComponent>(target);
        EnsureComp<ActionsComponent>(target);

        var action = EnsureComp<HologramConsoleActionComponent>(target);
        action.BladeServer = bladeUid;
    }

    public bool TryGetBrainChip(EntityUid bladeUid, HologramBladeServerComponent blade, out EntityUid brainChip)
    {
        brainChip = default;

        if (!TryComp(bladeUid, out ItemSlotsComponent? slots))
            return false;

        if (!_itemSlots.TryGetSlot(bladeUid, blade.BrainChipSlot, out var brainSlot, slots) || brainSlot.Item is not { } chip)
            return false;

        brainChip = chip;
        return true;
    }

    public bool TryGetBodyChip(EntityUid bladeUid, HologramBladeServerComponent blade, out EntityUid bodyChip)
    {
        bodyChip = default;

        if (!TryComp(bladeUid, out ItemSlotsComponent? slots))
            return false;

        if (!_itemSlots.TryGetSlot(bladeUid, blade.BodyChipSlot, out var bodySlot, slots) || bodySlot.Item is not { } chip)
            return false;

        bodyChip = chip;
        return true;
    }

    private void OnBladeSlotChanged(EntityUid uid, HologramBladeServerComponent component, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != component.BrainChipSlot && args.Container.ID != component.BodyChipSlot)
            return;

        SyncBladeLawsToOccupants(uid, component);
    }

    private void OnBladeSlotChanged(EntityUid uid, HologramBladeServerComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != component.BrainChipSlot && args.Container.ID != component.BodyChipSlot)
            return;

        ClearBladeBinding(args.Entity, uid);

        // A projection is only valid while both chips are still installed in its blade.
        if (component.ActiveHologram is { } activeHologram && Exists(activeHologram))
            _console.KillBladeHologram(uid, component);

        SyncBladeLawsToOccupants(uid, component);
    }

    private void ClearBladeBinding(EntityUid target, EntityUid bladeUid)
    {
        if (TryComp<HologramConsoleActionComponent>(target, out var action) && action.BladeServer == bladeUid)
            RemCompDeferred<HologramConsoleActionComponent>(target);

        if (TryComp<HologramBladeLawProviderComponent>(target, out var lawProvider) && lawProvider.BladeServer == bladeUid)
            RemCompDeferred<HologramBladeLawProviderComponent>(target);
    }

    private void OnBrainMindAdded(EntityUid uid, HologramBrainChipComponent component, MindAddedMessage args)
    {
        component.HoloMind = args.Mind;

        if (!TryFindBladeForChip(uid, out var bladeUid, out var blade))
            return;

        SyncBladeLawsToOccupants(bladeUid, blade);
    }

    private void OnBrainMindRemoved(EntityUid uid, HologramBrainChipComponent component, MindRemovedMessage args)
    {
        if (component.HoloMind == args.Mind)
            component.HoloMind = null;
    }

    private void OnBladeEmagged(EntityUid uid, HologramBladeServerComponent component, ref GotEmaggedEvent args)
    {
        if ((args.Type & EmagType.Interaction) == 0 || component.Emagged)
            return;

        component.Emagged = true;
        SyncBladeLawsToOccupants(uid, component);
        NotifyActiveHologram(component);
        _popup.PopupEntity("The hologram blade's law storage flickers red.", uid, args.UserUid);
        args.Handled = true;
    }

    private void OnRackEmagged(EntityUid uid, BladeServerRackComponent component, ref GotEmaggedEvent args)
    {
        if ((args.Type & EmagType.Interaction) == 0)
            return;

        var changed = false;
        foreach (var slot in component.BladeSlots)
        {
            if (slot.Item is not { } bladeUid || !TryComp(bladeUid, out HologramBladeServerComponent? blade))
                continue;

            if (blade.Emagged)
                continue;

            blade.Emagged = true;
            SyncBladeLawsToOccupants(bladeUid, blade);
            NotifyActiveHologram(blade);
            changed = true;
        }

        if (!changed)
            return;

        _popup.PopupEntity("The rack pushes a corrupted law update to its hologram blades.", uid, args.UserUid);
        args.Handled = true;
    }

    private void NotifyActiveHologram(HologramBladeServerComponent blade)
    {
        if (blade.ActiveHologram is not { } hologram || !Exists(hologram))
            return;

        _laws.NotifyLawsChanged(hologram);
    }

    private void OnGetBladeLaws(EntityUid uid, HologramBladeLawProviderComponent component, ref GetSiliconLawsEvent args)
    {
        if (args.Handled)
            return;

        if (component.BladeServer is not { } bladeUid || !TryComp(bladeUid, out HologramBladeServerComponent? blade))
            return;

        var laws = blade.Emagged ? blade.EmaggedLawset : blade.NormalLawset;
        args.Laws = _laws.GetLawset(laws);
        args.Handled = true;
    }

    private void OnOpenConsoleFromBrain(EntityUid uid, HologramBrainChipComponent component, HologramOpenConsoleActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetBladeForActionOwner(uid, out var bladeUid))
        {
            _popup.PopupEntity("This hologram brain is not linked to a blade server.", uid, uid);
            return;
        }

        TryOpenBladeConsole(bladeUid, args.Performer);
        args.Handled = true;
    }

    private void OnOpenConsoleFromProjection(EntityUid uid, HologramComponent _, HologramOpenConsoleActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetBladeForActionOwner(uid, out var bladeUid))
        {
            _popup.PopupEntity("This projection is not linked to a blade server.", uid, args.Performer);
            return;
        }

        TryOpenBladeConsole(bladeUid, args.Performer);
        args.Handled = true;
    }

    private bool TryGetBladeForActionOwner(EntityUid uid, out EntityUid bladeUid)
    {
        if (TryFindBladeForChip(uid, out bladeUid, out _))
            return true;

        if (TryFindBladeForProjection(uid, out bladeUid))
            return true;

        if (TryComp(uid, out HologramConsoleActionComponent? action) && action.BladeServer is { } storedBlade && Exists(storedBlade))
        {
            bladeUid = storedBlade;
            return true;
        }

        bladeUid = default;
        return false;
    }

    private void TryOpenBladeConsole(EntityUid bladeUid, EntityUid user)
    {
        if (!TryComp(user, out ActorComponent? actor))
        {
            _sawmill.Warning($"Unable to open hologram console for {ToPrettyString(user)}: no ActorComponent.");
            return;
        }

        if (!TryFindConsoleForBlade(bladeUid, out var consoleUid))
        {
            _popup.PopupEntity("No hologram console was found for this blade server.", user, user);
            return;
        }

        _ui.TryToggleUi(consoleUid, HologramConsoleUiKey.Key, actor.PlayerSession);
    }

    private bool TryFindConsoleForBlade(EntityUid bladeUid, out EntityUid consoleUid)
    {
        consoleUid = default;

        var current = bladeUid;
        while (Exists(current))
        {
            var parent = Transform(current).ParentUid;
            if (parent == EntityUid.Invalid || parent == current)
                break;

            current = parent;

            if (HasComp<BladeServerRackComponent>(current) && _ui.HasUi(current, HologramConsoleUiKey.Key))
            {
                consoleUid = current;
                return true;
            }
        }

        if (_ui.HasUi(bladeUid, HologramConsoleUiKey.Key))
        {
            consoleUid = bladeUid;
            return true;
        }

        var bladeGrid = GetEffectiveGridUid(bladeUid);
        if (bladeGrid == null)
            return false;

        var query = EntityQueryEnumerator<HologramConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_ui.HasUi(uid, HologramConsoleUiKey.Key))
                continue;

            if (GetEffectiveGridUid(uid) != bladeGrid)
                continue;

            consoleUid = uid;
            return true;
        }

        return false;
    }

    private EntityUid? GetEffectiveGridUid(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is { } grid)
            return grid;

        var current = xform.ParentUid;
        while (Exists(current))
        {
            var parentXform = Transform(current);
            if (parentXform.GridUid is { } parentGrid)
                return parentGrid;

            if (parentXform.ParentUid == EntityUid.Invalid || parentXform.ParentUid == current)
                break;

            current = parentXform.ParentUid;
        }

        return null;
    }

    private bool TryFindBladeForChip(EntityUid chip, out EntityUid bladeUid, out HologramBladeServerComponent blade)
    {
        bladeUid = default;
        blade = default!;

        var query = EntityQueryEnumerator<HologramBladeServerComponent, ItemSlotsComponent>();
        while (query.MoveNext(out var uid, out var bladeComp, out var slots))
        {
            if (!_itemSlots.TryGetSlot(uid, bladeComp.BrainChipSlot, out var brainSlot, slots) || brainSlot.Item != chip)
                continue;

            bladeUid = uid;
            blade = bladeComp;
            return true;
        }

        return false;
    }

    private bool TryFindBladeForProjection(EntityUid hologram, out EntityUid bladeUid)
    {
        bladeUid = default;

        var query = EntityQueryEnumerator<HologramBladeServerComponent>();
        while (query.MoveNext(out var uid, out var blade))
        {
            if (blade.ActiveHologram != hologram)
                continue;

            bladeUid = uid;
            return true;
        }

        return false;
    }
}

[RegisterComponent]
public sealed partial class HologramConsoleActionComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? BladeServer;
}

[RegisterComponent]
public sealed partial class HologramBladeLawProviderComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? BladeServer;
}
