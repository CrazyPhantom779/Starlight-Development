namespace Content.Server._Starlight.Holograms;

/// <summary>
/// Stores a mind for holographic projection.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBrainChipComponent : Component
{
    /// <summary>
    /// The mind stored in this brain chip.
    /// </summary>
    [ViewVariables]
    public EntityUid? HoloMind;

    /// <summary>
    /// Whether this chip is currently in a powered hologram blade server.
    /// </summary>
    [ViewVariables]
    public bool IsPowered;

    /// <summary>
    /// Allows this chip to project a body without storing a player mind.
    /// Used for autonomous/NPC holograms such as holo-corgis.
    /// </summary>
    [DataField]
    public bool AllowAutonomousProjection;

    /// <summary>
    /// Prevents scanners and ghost-role setup from placing a real player mind in this chip.
    /// Use with AllowAutonomousProjection for stunted/NPC-only chips.
    /// </summary>
    [DataField]
    public bool PreventMindStorage;
}

