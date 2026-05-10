using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms;

/// <summary>
/// Stores physical appearance data for holographic projection.
/// Output by body scanners or initialized for hologram jobs.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBodyChipComponent : Component
{
    /// <summary>
    /// The prototype ID of a hologram mob appearance to use.
    /// </summary>
    [DataField]
    public EntProtoId? HologramPrototype;

    /// <summary>
    /// The name to display for this hologram body.
    /// </summary>
    [DataField]
    public string? HologramName;

    /// <summary>
    /// Runtime humanoid profile captured from the owning player/profile or scanner target.
    /// This avoids falling back to a random enabled profile when the stored mind has no humanoid body.
    /// </summary>
    [ViewVariables]
    public HumanoidCharacterProfile? HologramProfile;

    /// <summary>
    /// Original body/entity this body chip was written from, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? SourceBody;
}
