using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Flock;

/// <summary>Settings for tiles that are converted. One per round typically.</summary>
[Prototype]
public sealed partial class FlockTileSettingsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string FloorTile = "FloorFlock";

    [DataField(required: true)]
    public string BrokenTile = "FloorFlockBroken";

    /// <summary>Wall entity spawned over wall-like station walls.</summary>
    [DataField]
    public EntProtoId Wall = "WallFlock";
}
