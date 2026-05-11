using Content.Shared.Actions;
using Content.Shared.Mind.Components;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Grants the hologram console action only while the mind is housed in a blade or a projected hologram.
/// </summary>
public sealed class HologramActionGrantSystem : EntitySystem
{
    private const string HologramConsoleAction = "ActionOpenHologramConsole";

    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramConsoleActionComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<HologramConsoleActionComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<HologramConsoleActionComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMindAdded(EntityUid uid, HologramConsoleActionComponent component, MindAddedMessage args)
        => EnsureAction(uid, component);

    private void OnMindRemoved(EntityUid uid, HologramConsoleActionComponent component, MindRemovedMessage args)
        => RemoveAction(uid, component);

    private void OnShutdown(EntityUid uid, HologramConsoleActionComponent component, ComponentShutdown args)
        => RemoveAction(uid, component);

    private void EnsureAction(EntityUid uid, HologramConsoleActionComponent component)
    {
        if (!TryComp<MindContainerComponent>(uid, out var mind) || mind.Mind == null)
            return;

        if (component.Action != null && Exists(component.Action.Value))
            return;

        component.Action = _actions.AddAction(uid, HologramConsoleAction);
    }

    private void RemoveAction(EntityUid _, HologramConsoleActionComponent component)
    {
        if (component.Action is { } action && Exists(action))
            _actions.RemoveAction(action);

        component.Action = null;
    }
}
