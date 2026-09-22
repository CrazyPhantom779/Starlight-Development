using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms;

/// <summary>
/// Runtime-only safe body data captured by a hologram body scanner.
/// This intentionally stores copied allowlisted components instead of a raw scanned mob prototype.
/// </summary>
public sealed partial class HologramScannedBodyData
{
    [ViewVariables]
    public string? Name;

    [ViewVariables]
    public EntProtoId ProjectionPrototype = "MobHologramScannedBody";

    /// <summary>
    /// Original scanned prototype, only for debug/reference.
    /// This may be null if the scanned entity had no prototype.
    /// </summary>
    [ViewVariables]
    public EntProtoId? SourcePrototype;

    [ViewVariables]
    public Dictionary<string, Component> ComponentCopies = [];
}
