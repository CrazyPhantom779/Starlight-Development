using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms;

/// <summary>
/// Data-driven policy for hologram body scanners.
/// Components listed here are copied from the scanned body onto the safe hologram base.
/// A hard-coded safety denylist in HologramBodyScannerSystem still blocks dangerous components.
/// </summary>
[Prototype]
public sealed partial class HologramScanSettingsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Safe projection prototype for humanoid scans.
    /// Humanoid scans also store a HumanoidCharacterProfile.
    /// </summary>
    [DataField]
    public EntProtoId HumanoidProjectionPrototype = "MobHologramHardlight";

    /// <summary>
    /// Safe projection prototype for non-humanoid/simple mob scans.
    /// </summary>
    [DataField]
    public EntProtoId ScannedProjectionPrototype = "MobHologramScannedBody";

    /// <summary>
    /// Component names to copy from the source body.
    /// Names are the same strings used in YAML component type fields.
    /// </summary>
    [DataField]
    public List<string> Components = new();

    /// <summary>
    /// Extra denylist for a specific scanner policy.
    /// This is applied in addition to the code safety denylist.
    /// </summary>
    [DataField]
    public HashSet<string> Blacklist = new(StringComparer.OrdinalIgnoreCase);
}
