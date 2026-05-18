using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms;

/// <summary>
/// Stores physical appearance data for holographic projection.
/// Body chips intentionally store only the projection recipe: prototype, optional humanoid profile,
/// display name, and the source body that produced the data.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBodyChipComponent : Component
{
    /// <summary>
    /// The mob prototype used when projecting this body.
    /// Null means the hologram system should use its normal hardlight humanoid fallback.
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
    /// This is deliberately runtime-only; scanned body data should not be serialized into maps.
    /// </summary>
    [ViewVariables]
    public HumanoidCharacterProfile? HologramProfile;

    /// <summary>
    /// Original body/entity this body chip was written from, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? SourceBody;

    /// <summary>
    /// True once the chip has actual scanned/job body data, not merely a default prototype value from YAML.
    /// </summary>
    public bool HasStoredBodyData =>
        SourceBody != null ||
        HologramProfile != null ||
        !string.IsNullOrWhiteSpace(HologramName);
}
