using Content.Server._Starlight.Holograms.Components;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Lets empty hologram brain chips be activated into a ghost role, similar to a positronic brain.
/// </summary>
public sealed class HologramBrainChipGhostRoleSystem : EntitySystem
{
    [Dependency] private readonly GhostRoleSystem _ghostRole = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramBrainChipGhostRoleComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<HologramBrainChipGhostRoleComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnActivate(EntityUid uid, HologramBrainChipGhostRoleComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        EnsureComp<MindContainerComponent>(uid);

        if (TryComp<MindContainerComponent>(uid, out var mindContainer) && mindContainer.Mind != null)
        {
            _popup.PopupEntity("This hologram brain chip is already occupied.", uid, args.User);
            args.Handled = true;
            return;
        }

        if (TryComp<GhostRoleComponent>(uid, out var existingRole) && !existingRole.Taken)
        {
            _popup.PopupEntity("This hologram brain chip is already searching for a mind.", uid, args.User);
            args.Handled = true;
            return;
        }

        // Add this first so the role is actually take-over capable by the time it is registered.
        EnsureComp<GhostTakeoverAvailableComponent>(uid);

        var role = EnsureComp<GhostRoleComponent>(uid);

        role.RoleName = component.RoleName;
        role.RoleDescription = component.RoleDescription;
        role.RoleRules = component.RoleRules;

        _ghostRole.RegisterGhostRole((uid, role));

        _popup.PopupEntity("The hologram brain chip begins searching for a mind.", uid, args.User);
        args.Handled = true;
    }

    private void OnMindAdded(EntityUid uid, HologramBrainChipGhostRoleComponent component, MindAddedMessage args)
    {
        if (TryComp<HologramBrainChipComponent>(uid, out var brainChip))
            brainChip.HoloMind = args.Mind;

        if (TryComp<GhostRoleComponent>(uid, out var role))
            _ghostRole.UnregisterGhostRole((uid, role));
    }
}
