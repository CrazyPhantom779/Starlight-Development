using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// Hologram body scanner that captures appearance and mind data.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBodyScannerComponent : Component
{
    /// <summary>
    /// Time between scans to prevent spam.
    /// </summary>
    [DataField]
    public TimeSpan ScanDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Scan policy used by this scanner.
    /// Different scanners can copy different safe component sets.
    /// </summary>
    [DataField]
    public ProtoId<HologramScanSettingsPrototype> Settings = "DefaultHologramScan";

    /// <summary>
    /// Last time a scan was performed.
    /// </summary>
    [ViewVariables]
    public TimeSpan LastScanTime = TimeSpan.Zero;
}
