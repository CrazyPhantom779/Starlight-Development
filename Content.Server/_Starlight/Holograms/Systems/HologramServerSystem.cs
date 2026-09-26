using System.Linq;
using Content.Server._Starlight.Holograms.Components;
using Content.Shared._Starlight.Holograms;
using Content.Shared.Power;

namespace Content.Server._Starlight.Holograms.Systems;

public sealed partial class HologramServerSystem : EntitySystem
{
    [Dependency] private HologramSystem _hologram = default!;
    [Dependency] private HologramConsoleSystem _console = default!;

    [SubscribeLocalEvent]
    private void ServerOnPowerChanged(EntityUid _, HologramServerComponent component, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        KillAllHolograms(component);
    }

    [SubscribeLocalEvent]
    private void OnServerShutdown(EntityUid _, HologramServerComponent component, ComponentShutdown __)
        => KillAllHolograms(component);

    private void KillAllHolograms(HologramServerComponent component)
    {
        foreach (var (blade, hologram) in component.ActiveHolograms.ToArray())
        {
            if (!Exists(hologram))
                continue;

            if (Exists(blade) && TryComp<HologramBladeServerComponent>(blade, out var bladeComp))
                _console.KillBladeHologram(blade, bladeComp);
            else
                _hologram.DoKillHologram(hologram);
        }

        component.ActiveHolograms.Clear();

        if (component.LinkedHologram is { } linkedHologram && Exists(linkedHologram))
            _hologram.DoKillHologram(linkedHologram);

        component.LinkedHologram = null;
    }
}
