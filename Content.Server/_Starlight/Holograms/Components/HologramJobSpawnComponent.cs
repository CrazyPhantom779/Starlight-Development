namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// Marks a hologram blade server as a round-start job spawn target.
/// When a Hologram job player is inserted into this server, their mind is stored
/// on the brain chip and optionally projected immediately.
/// </summary>
[RegisterComponent]
public sealed partial class HologramJobSpawnComponent : Component
{
    [DataField]
    public bool SpawnOnJoin = true;

    [ViewVariables]
    public EntityUid? LinkedHologram;
}
