using Content.Shared._Starlight.Holograms.Components;
using Content.Shared.Teleportation.Components;

namespace Content.Shared._Starlight.Holograms.Systems;

/// <summary>
/// Prevents projected holograms from using portals.
/// </summary>
public sealed class HologramPortalBlockSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PortalComponent, OnAttemptPortalEvent>(OnAttemptPortal);
    }

    private void OnAttemptPortal(EntityUid uid, PortalComponent component, OnAttemptPortalEvent args)
    {
        if (!HasComp<HologramComponent>(args.Subject))
            return;

        args.Cancel();
    }
}
