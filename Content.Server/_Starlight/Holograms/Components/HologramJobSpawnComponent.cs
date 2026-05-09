namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// Marks a hologram blade server as a valid round-start job container spawn.
/// </summary>
[RegisterComponent]
public sealed partial class HologramJobSpawnComponent : Component
{
    /// <summary>
    /// If true, the server immediately projects the spawned job mind into a hologram.
    /// </summary>
    [DataField]
    public bool SpawnOnJoin = true;
}
