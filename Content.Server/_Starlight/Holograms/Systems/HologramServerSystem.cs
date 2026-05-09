using Content.Shared._Starlight.Holograms;
using Content.Shared.Power;

namespace Content.Server._Starlight.Holograms.Systems;

public sealed partial class HologramServerSystem : EntitySystem
{
    [Dependency] private readonly HologramSystem _hologram = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramServerComponent, PowerChangedEvent>(ServerOnPowerChanged);
        SubscribeLocalEvent<HologramServerComponent, ComponentShutdown>(OnServerShutdown);
    }

    private void ServerOnPowerChanged(EntityUid uid, HologramServerComponent component, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        KillAllHolograms(component);
    }

    private void OnServerShutdown(EntityUid uid, HologramServerComponent component, ComponentShutdown args)
        => KillAllHolograms(component);

    private void KillAllHolograms(HologramServerComponent component)
    {
        foreach (var hologram in component.ActiveHolograms.Values)
        {
            if (Exists(hologram))
                _hologram.DoKillHologram(hologram);
        }

        component.ActiveHolograms.Clear();

        if (component.LinkedHologram != null && Exists(component.LinkedHologram.Value))
            _hologram.DoKillHologram(component.LinkedHologram.Value);

        component.LinkedHologram = null;
    }
}
