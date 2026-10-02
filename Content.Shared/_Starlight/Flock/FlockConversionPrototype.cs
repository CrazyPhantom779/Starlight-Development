using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Flock;

/// <summary>
/// Data-driven map from station entity -> flock replacement, used by the nanite spray / flockbits / relay.
/// Entries are checked in <see cref="Priority"/> order (highest first); first whitelist match wins.
/// </summary>
[Prototype]
public sealed partial class FlockConversionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public int Priority;

    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    /// <summary>Spawn this in place (keeping position/rotation). Null = just delete the original.</summary>
    [DataField]
    public EntProtoId? Replacement;

    /// <summary>Resource value granted to the flock-side on conversion (informational; cost is flat 20 in goon).</summary>
    [DataField]
    public int Cost;

    /// <summary>Bit conversions are free for flockbits.</summary>
    [DataField]
    public bool Anchored;
}
