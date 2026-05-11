namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// Allows an empty hologram brain chip to be activated as a ghost role, like a positronic brain.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBrainChipGhostRoleComponent : Component
{
    [DataField]
    public string RoleName = "hologram brain";

    [DataField]
    public string RoleDescription = "A dormant hologram brain chip is searching for a mind.";

    [DataField]
    public string RoleRules = "You are a holographic consciousness. Follow your laws and roleplay as a synthetic hardlight intelligence.";
}
