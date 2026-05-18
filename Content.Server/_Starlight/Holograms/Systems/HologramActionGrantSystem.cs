using Content.Server._Starlight.Holograms.Components;
using Content.Server.Power.Components;
using Content.Shared._Moffstation.BladeServer;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Grants hologram hotbar actions from explicit YAML configuration.
/// Projected holograms can open their console and view laws.
/// Brain chips can view laws, and may open their console only while installed in a powered rack-backed blade.
/// </summary>
public sealed partial class HologramActionGrantSystem : EntitySystem
{
    private const float RefreshInterval = 1f;

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    private float _refreshAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramActionGrantComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HologramActionGrantComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<HologramActionGrantComponent, MindAddedMessage>(OnMindChanged);
        SubscribeLocalEvent<HologramActionGrantComponent, MindRemovedMessage>(OnMindChanged);

        // HologramBladeLawSystem adds/removes this link when chips enter/leave blades.
        SubscribeLocalEvent<HologramConsoleActionComponent, ComponentStartup>(OnConsoleLinkChanged);
        SubscribeLocalEvent<HologramConsoleActionComponent, ComponentShutdown>(OnConsoleLinkChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _refreshAccumulator += frameTime;
        if (_refreshAccumulator < RefreshInterval)
            return;

        _refreshAccumulator = 0f;

        var query = EntityQueryEnumerator<HologramActionGrantComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            RefreshActions(uid, component);
        }
    }

    private void OnStartup(EntityUid uid, HologramActionGrantComponent component, ComponentStartup args)
        => RefreshActions(uid, component);

    private void OnShutdown(EntityUid uid, HologramActionGrantComponent component, ComponentShutdown args)
        => RemoveAllActions(uid, component);

    private void OnMindChanged(EntityUid uid, HologramActionGrantComponent component, EntityEventArgs args)
        => RefreshActions(uid, component);

    private void OnConsoleLinkChanged(EntityUid uid, HologramConsoleActionComponent component, EntityEventArgs args)
    {
        if (TryComp<HologramActionGrantComponent>(uid, out var grant))
            RefreshActions(uid, grant);
    }

    private void RefreshActions(EntityUid uid, HologramActionGrantComponent component)
    {
        if (!TryComp<MindContainerComponent>(uid, out var mindContainer) || mindContainer.Mind == null)
        {
            RemoveAllActions(uid, component);
            return;
        }

        RefreshAction(uid, ref component.LawsActionEntity, component.LawsAction, component.GrantLawsAction);
        RefreshAction(uid, ref component.ConsoleActionEntity, component.ConsoleAction, CanUseConsoleAction(uid, component));
    }

    private void RefreshAction(EntityUid uid, ref EntityUid? actionEntity, EntProtoId actionPrototype, bool shouldHave)
    {
        if (!shouldHave)
        {
            RemoveAction(ref actionEntity);
            return;
        }

        if (actionEntity is { } existing && Exists(existing))
            return;

        _actions.AddAction(uid, ref actionEntity, actionPrototype);
    }

    private bool CanUseConsoleAction(EntityUid uid, HologramActionGrantComponent component)
    {
        if (!component.GrantConsoleAction)
            return false;

        if (HasComp<HologramComponent>(uid))
            return true;

        if (!component.GrantConsoleActionWhileContainedInPoweredRack)
            return false;

        if (!HasComp<HologramBrainChipComponent>(uid))
            return false;

        return TryGetLinkedBlade(uid, out var bladeUid, out var blade) && IsChipInstalledInPoweredRackBlade(uid, bladeUid, blade);
    }

    private bool TryGetLinkedBlade(EntityUid uid, out EntityUid bladeUid, out HologramBladeServerComponent blade)
    {
        bladeUid = default;
        blade = default!;

        if (!TryComp<HologramConsoleActionComponent>(uid, out var link) ||
            link.BladeServer is not { } linkedBlade)
        {
            return false;
        }

        if (!Exists(linkedBlade) ||
            !TryComp<HologramBladeServerComponent>(linkedBlade, out var bladeComp))
        {
            return false;
        }

        bladeUid = linkedBlade;
        blade = bladeComp;
        return true;
    }

    private bool IsChipInstalledInPoweredRackBlade(EntityUid chipUid, EntityUid bladeUid, HologramBladeServerComponent blade)
    {
        if (!TryComp<ItemSlotsComponent>(bladeUid, out var bladeSlots))
            return false;

        if (!_itemSlots.TryGetSlot(bladeUid, blade.BrainChipSlot, out var brainSlot, bladeSlots) || brainSlot.Item != chipUid)
            return false;

        if (!TryGetContainingRack(bladeUid, out var rackUid, out var rack))
            return false;

        if (!TryComp<ApcPowerReceiverComponent>(rackUid, out var rackPower) || !rackPower.Powered)
            return false;

        foreach (var slot in rack.BladeSlots)
        {
            if (slot.Item == bladeUid)
                return slot.IsPowerEnabled;
        }

        return false;
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

    private void RemoveAllActions(EntityUid _, HologramActionGrantComponent component)
    {
        RemoveAction(ref component.ConsoleActionEntity);
        RemoveAction(ref component.LawsActionEntity);
    }

    private void RemoveAction(ref EntityUid? actionEntity)
    {
        if (actionEntity is { } action && Exists(action))
            _actions.RemoveAction(action);

        actionEntity = null;
    }
}
