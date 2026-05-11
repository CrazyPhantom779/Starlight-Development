namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// Marks a blade server rack as a hologram job rack.
/// These racks start empty; job blades are created only when hologram players join.
/// </summary>
[RegisterComponent]
public sealed partial class HologramJobRackComponent : Component
{
}
