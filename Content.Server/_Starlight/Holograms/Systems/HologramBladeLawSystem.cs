using Content.Server.Silicons.Laws;
using Content.Shared._Moffstation.BladeServer;
using Content.Shared._Starlight.Holograms;
using Content.Shared.Actions;
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
/// Keeps hologram laws and the hologram console action tied to the blade server that houses the mind.
/// The blade owns the lawset; the chip/projected body only provide those laws while tied to the blade.
/// </summary>
public sealed class HologramBladeLawSystem : EntitySystem
{
    private const string HologramConsoleAction = "ActionOpenHologramConsole";

    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SiliconLawSystem _laws = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramBladeServerComponent, GotEmaggedEvent>(OnBladeEmagged);
        SubscribeLocalEvent<HologramBladeServerComponent, EntInsertedIntoContainerMessage>(OnBladeSlotChanged);
        SubscribeLocalEvent<HologramBladeServerComponent, EntRemovedFromContainerMessage>(OnBladeSlotChanged);

        SubscribeLocalEvent<BladeServerRackComponent, GotEmaggedEvent>(OnRackEmagged);

        SubscribeLocalEvent<HologramBrainChipComponent, MindAddedMessage>(OnBrainMindAdded);
        SubscribeLocalEvent<HologramBrainChipComponent, MindRemovedMessage>(OnBrainMindRemoved);
        SubscribeLocalEvent<HologramBrainChipComponent, HologramOpenConsoleActionEvent>(OnOpenConsoleFromBrain);

        SubscribeLocalEvent<HologramComponent, HologramOpenConsoleActionEvent>(OnOpenConsoleFromProjection);

        SubscribeLocalEvent<HologramBladeLawProviderComponent, GetSiliconLawsEvent>(
            OnGetBladeLaws,
            before: new[] { typeof(SiliconLawSystem) });
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
        EnsureComp<ActionsComponent>(target);

        var action = EnsureComp<HologramConsoleActionComponent>(target);
        action.BladeServer = bladeUid;

        if (action.Action is { } existingAction && !Exists(existingAction))
            action.Action = null;

        if (!_actions.AddAction(target, ref action.Action, HologramConsoleAction))
            Logger.Warning($"Failed to grant {HologramConsoleAction} to {ToPrettyString(target)}.");
    }

    public bool TryGetBrainChip(EntityUid bladeUid, HologramBladeServerComponent blade, out EntityUid brainChip)
    {
        brainChip = default;

        if (!TryComp<ItemSlotsComponent>(bladeUid, out var slots))
            return false;

        if (!_itemSlots.TryGetSlot(bladeUid, blade.BrainChipSlot, out var brainSlot, slots) ||
            brainSlot.Item is not { } chip)
            return false;

        brainChip = chip;
        return true;
    }

    public bool TryGetBodyChip(EntityUid bladeUid, HologramBladeServerComponent blade, out EntityUid bodyChip)
    {
        bodyChip = default;

        if (!TryComp<ItemSlotsComponent>(bladeUid, out var slots))
            return false;

        if (!_itemSlots.TryGetSlot(bladeUid, blade.BodyChipSlot, out var bodySlot, slots) ||
            bodySlot.Item is not { } chip)
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

        SyncBladeLawsToOccupants(uid, component);
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
        if ((args.Type & EmagType.Interaction) == 0)
            return;

        if (component.Emagged)
            return;

        component.Emagged = true;
        SyncBladeLawsToOccupants(uid, component);
        NotifyActiveHologram(uid, component);
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
            if (slot.Item is not { } bladeUid ||
                !TryComp<HologramBladeServerComponent>(bladeUid, out var blade))
                continue;

            if (blade.Emagged)
                continue;

            blade.Emagged = true;
            SyncBladeLawsToOccupants(bladeUid, blade);
            NotifyActiveHologram(bladeUid, blade);
            changed = true;
        }

        if (!changed)
            return;

        _popup.PopupEntity("The rack pushes a corrupted law update to its hologram blades.", uid, args.UserUid);
        args.Handled = true;
    }

    private void NotifyActiveHologram(EntityUid _, HologramBladeServerComponent blade)
    {
        if (blade.ActiveHologram is not { } hologram || !Exists(hologram))
            return;

        _laws.NotifyLawsChanged(hologram);
    }

    private void OnGetBladeLaws(EntityUid uid, HologramBladeLawProviderComponent component, ref GetSiliconLawsEvent args)
    {
        if (args.Handled)
            return;

        if (component.BladeServer is not { } bladeUid ||
            !TryComp<HologramBladeServerComponent>(bladeUid, out var blade))
            return;

        var laws = blade.Emagged ? blade.EmaggedLawset : blade.NormalLawset;
        args.Laws = _laws.GetLawset(laws);
        args.Handled = true;
    }

    private void OnOpenConsoleFromBrain(EntityUid uid, HologramBrainChipComponent component, HologramOpenConsoleActionEvent args)
    {
        if (args.Handled)
            return;

        EntityUid bladeUid;
        if (TryFindBladeForChip(uid, out var foundBladeUid, out _))
        {
            bladeUid = foundBladeUid;
        }
        else if (TryComp<HologramConsoleActionComponent>(uid, out var action) &&
                 action.BladeServer is { } storedBlade &&
                 Exists(storedBlade))
        {
            bladeUid = storedBlade;
        }
        else
        {
            return;
        }

        TryOpenBladeConsole(bladeUid, args.Performer);
        args.Handled = true;
    }

    private void OnOpenConsoleFromProjection(EntityUid uid, HologramComponent component, HologramOpenConsoleActionEvent args)
    {
        if (args.Handled)
            return;

        EntityUid bladeUid;
        if (TryFindBladeForProjection(uid, out var foundBladeUid))
        {
            bladeUid = foundBladeUid;
        }
        else if (TryComp<HologramConsoleActionComponent>(uid, out var action) &&
                 action.BladeServer is { } storedBlade &&
                 Exists(storedBlade))
        {
            bladeUid = storedBlade;
        }
        else
        {
            return;
        }

        TryOpenBladeConsole(bladeUid, args.Performer);
        args.Handled = true;
    }

    private void TryOpenBladeConsole(EntityUid bladeUid, EntityUid user)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        _ui.TryToggleUi(bladeUid, HologramConsoleUiKey.Key, actor.PlayerSession);
    }

    private bool TryFindBladeForChip(EntityUid chip, out EntityUid bladeUid, out HologramBladeServerComponent blade)
    {
        bladeUid = default;
        blade = default!;

        var query = EntityQueryEnumerator<HologramBladeServerComponent, ItemSlotsComponent>();
        while (query.MoveNext(out var uid, out var bladeComp, out var slots))
        {
            if (!_itemSlots.TryGetSlot(uid, bladeComp.BrainChipSlot, out var brainSlot, slots) ||
                brainSlot.Item != chip)
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

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? Action;
}

[RegisterComponent]
public sealed partial class HologramBladeLawProviderComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? BladeServer;
}
