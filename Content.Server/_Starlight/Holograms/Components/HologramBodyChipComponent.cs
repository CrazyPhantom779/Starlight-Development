using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// Stores physical appearance data for holographic projection.
/// Body chips intentionally store only a hologram-safe projection recipe.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBodyChipComponent : Component
{
    /// <summary>
    /// The safe mob prototype used when projecting this body.
    /// Raw scanned mob prototypes should not be stored here.
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
    /// Safe non-humanoid scanned-body data. This replaces the old raw-prototype scanning path.
    /// </summary>
    [ViewVariables]
    public HologramScannedBodyData? ScannedBody;

    /// <summary>
    /// Original body/entity this body chip was written from, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? SourceBody;

    /// <summary>
    /// Settings used to write this chip.
    /// </summary>
    [ViewVariables]
    public ProtoId<HologramScanSettingsPrototype>? ScanSettings;

    /// <summary>
    /// True once the chip has actual scanned/job body data, not merely a default prototype value from YAML.
    /// </summary>
    public bool HasStoredBodyData =>
        SourceBody != null ||
        HologramProfile != null ||
        ScannedBody != null ||
        !string.IsNullOrWhiteSpace(HologramName);
}
