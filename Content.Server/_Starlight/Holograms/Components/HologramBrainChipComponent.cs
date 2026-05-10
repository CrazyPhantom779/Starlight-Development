namespace Content.Server._Starlight.Holograms;

/// <summary>
/// Stores a mind for holographic projection.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBrainChipComponent : Component
{
    /// <summary>
    /// The mind stored in this brain chip.
    /// </summary>
    [ViewVariables]
    public EntityUid? HoloMind;

    /// <summary>
    /// Whether this chip is currently in a powered hologram blade server.
    /// </summary>
    [ViewVariables]
    public bool IsPowered;
}
