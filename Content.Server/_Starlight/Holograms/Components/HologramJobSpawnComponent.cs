namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// Marks a hologram blade server as a valid Hologram job spawn target.
/// The job mind is stored in the brain chip until projected by a console.
/// </summary>
[RegisterComponent]
public sealed partial class HologramJobSpawnComponent : Component
{
    /// <summary>
    /// Kept for YAML compatibility. Hologram jobs should not auto-project on join.
    /// </summary>
    [DataField]
    public bool SpawnOnJoin = false;
}
