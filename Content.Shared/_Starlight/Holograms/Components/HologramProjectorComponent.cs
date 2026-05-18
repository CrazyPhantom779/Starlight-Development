using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Holograms;

/// <summary>
/// Marks an entity as capable of projecting holograms.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HologramProjectorComponent : Component
{
    /// <summary>
    /// Maximum range before connected holograms start their return grace timer.
    /// Kept close to a camera-like room/nearby-hall range; visibility still requires line of sight.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float ProjectorRange = 7f;

    /// <summary>
    /// Tile offset of the projector effect for each direction.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public Dictionary<Direction, Vector2> EffectOffsets { get; set; } = new()
    {
        { Direction.North, Vector2.Zero },
        { Direction.East, Vector2.Zero },
        { Direction.South, Vector2.Zero },
        { Direction.West, Vector2.Zero },
    };

    /// <summary>
    /// Whether this projector is currently active and usable.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public bool IsActive = true;
}
