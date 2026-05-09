namespace Content.Server._Starlight.Holograms;

/// <summary>
/// A blade server that stores hologram data via brain and body chips.
/// Consoles read from these servers to populate the hologram list.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBladeServerComponent : Component
{
    /// <summary>
    /// Slot ID for the mind/brain chip.
    /// </summary>
    [DataField]
    public string BrainChipSlot = "hologram_brain_chip";

    /// <summary>
    /// Slot ID for the body/profile chip.
    /// </summary>
    [DataField]
    public string BodyChipSlot = "hologram_body_chip";

    /// <summary>
    /// Whether this blade server is currently powered and functional.
    /// </summary>
    [ViewVariables]
    public bool IsPowered;

    /// <summary>
    /// The currently projected hologram owned by this blade server, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? ActiveHologram;
}
