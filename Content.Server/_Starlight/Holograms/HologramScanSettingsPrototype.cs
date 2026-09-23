using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms;

/// <summary>
/// Settings for a hologram body scanner. Says which prototype to project into, and which
/// components are safe to copy from a scanned body onto that projection.
///
/// Components is an allowlist, not a denylist: only the names listed here are ever copied,
/// so anything not listed is automatically excluded. There's no separate blacklist to keep
/// in sync - if a component shouldn't be copied, just don't add it to the list.
/// </summary>
[Prototype]
public sealed partial class HologramScanSettingsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Projection prototype used for humanoid scans (these also carry a full HumanoidCharacterProfile,
    /// so most of their look comes from that rather than from Components below).
    /// </summary>
    [DataField]
    public EntProtoId HumanoidProjectionPrototype = "MobHologramHardlight";

    /// <summary>
    /// Projection prototype used for non-humanoid scans (animals, simple mobs, etc).
    /// </summary>
    [DataField]
    public EntProtoId ScannedProjectionPrototype = "MobHologramScannedBody";

    /// <summary>
    /// Components copied from a scanned non-humanoid body onto its projection. Use the same
    /// name you'd write after "type:" in YAML. Only used for the non-humanoid scan path.
    /// </summary>
    [DataField]
    public List<string> Components = [];
}
