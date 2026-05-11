namespace Content.Server._Starlight.Holograms;

/// <summary>
/// A blade server that stores hologram data via brain and body chips.
/// Consoles read from these servers to populate the hologram list.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBladeServerComponent : Component
{
    /// <summary>
    /// Slot ID for the brain chip.
    /// </summary>
    [DataField]
    public string BrainChipSlot = "hologram_brain_chip";

    /// <summary>
    /// Slot ID for the body chip.
    /// </summary>
    [DataField]
    public string BodyChipSlot = "hologram_body_chip";

    /// <summary>
    /// Whether this blade server is currently powered and functional.
    /// </summary>
    [ViewVariables]
    public bool IsPowered;

    /// <summary>
    /// The currently projected hologram for this blade, if any.
    /// Null means the mind is stored in the chip but not currently projected.
    /// </summary>
    [ViewVariables]
    public EntityUid? ActiveHologram;

    /// <summary>
    /// Whether this blade has been emagged.
    /// Laws belong to the blade, not the body projection.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Emagged;

    /// <summary>
    /// Default lawset for a non-emagged hologram.
    /// Must match a siliconLawset prototype ID.
    /// </summary>
    [DataField]
    public string NormalLawset = "Drone";

    /// <summary>
    /// Lawset used once the blade/rack has been emagged.
    /// Must match a siliconLawset prototype ID.
    /// </summary>
    [DataField]
    public string EmaggedLawset = "SyndicateStatic";
}
