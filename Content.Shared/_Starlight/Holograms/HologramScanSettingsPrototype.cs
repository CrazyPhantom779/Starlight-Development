using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Holograms;

/// <summary>
/// Settings for a hologram body scanner. Says which prototype to project into, and which
/// components are safe to copy from a scanned body onto that projection.
/// </summary>
[Prototype]
public sealed partial class HologramScanSettingsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Projection prototype used for humanoid scans, these also carry a HumanoidCharacterProfile.
    /// </summary>
    [DataField]
    public EntProtoId HumanoidProjectionPrototype = "MobHologramHardlight";

    /// <summary>
    /// Projection prototype used for non-humanoid scans (animals, simple mobs, etc).
    /// </summary>
    [DataField]
    public EntProtoId ScannedProjectionPrototype = "MobHologramScannedBody";

    /// <summary>
    /// Components copied from a scanned non-humanoid body onto its projection.
    /// </summary>
    [DataField]
    public List<string> Components = [];
}
