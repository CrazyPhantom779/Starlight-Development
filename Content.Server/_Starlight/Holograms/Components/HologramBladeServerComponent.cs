namespace Content.Server._Starlight.Holograms;

/// <summary>
/// A blade server that stores hologram data via brain and body chips.
/// Consoles read from these servers to populate the hologram list.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBladeServerComponent : Component
{
    [DataField]
    public string BrainChipSlot = "hologram_brain_chip";

    [DataField]
    public string BodyChipSlot = "hologram_body_chip";

    [ViewVariables]
    public bool IsPowered;

    [ViewVariables]
    public EntityUid? ActiveHologram;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Emagged;

    [DataField]
    public string NormalLawset = "Drone";

    [DataField]
    public string EmaggedLawset = "SyndicateStatic";
}
