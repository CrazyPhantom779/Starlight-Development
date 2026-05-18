using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Holograms;

/// <summary>
/// Appearance keys used by the portable hologram console/briefcase visualizer.
/// </summary>
[Serializable, NetSerializable]
public enum HologramBriefcaseVisuals : byte
{
    State,
    HasBlade
}

/// <summary>
/// Visual states for a portable hologram console/briefcase.
/// </summary>
[Serializable, NetSerializable]
public enum HologramBriefcaseState : byte
{
    Closed,
    Open,
    Active
}
