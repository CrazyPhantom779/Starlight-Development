using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// Gives a hologram a random name on startup.
/// Intended for explicit ghost-role hologram prototypes only, not the round-start Hologram job.
/// </summary>
[RegisterComponent]
public sealed partial class HologramRandomNameComponent : Component
{
    [DataField]
    public ProtoId<LocalizedDatasetPrototype> NameDataset = "NamesHologram";
}
