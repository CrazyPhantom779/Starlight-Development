using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Holograms;

/// <summary>
/// Marks the entity as a being made of light.
/// Details are handled by sister components.
/// </summary>
[RegisterComponent]
public sealed partial class HologramComponent : Component
{
    /// <summary>
    /// The sound to play when the hologram is turned on.
    /// </summary>
    [DataField]
    public SoundSpecifier OnSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/Holograms/holo_on.ogg");

    /// <summary>
    /// The sound to play when the hologram is turned off.
    /// </summary>
    [DataField]
    public SoundSpecifier OffSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/Holograms/holo_off.ogg");

    /// <summary>
    /// The string to use for the popup when the hologram appears, shown to others.
    /// </summary>
    [DataField]
    public string PopupAppearOther = "system-hologram-phasing-appear-others";

    /// <summary>
    /// The string to use for the popup when the hologram appears, shown to themselves.
    /// </summary>
    [DataField]
    public string PopupAppearSelf = "system-hologram-phasing-appear-self";

    /// <summary>
    /// The string to use for the popup when the hologram disappears, shown to others.
    /// </summary>
    [DataField]
    public string PopupDisappearOther = "system-hologram-phasing-disappear-others";

    /// <summary>
    /// The string to use for the popup when the hologram is killed, shown to themselves.
    /// </summary>
    [DataField]
    public string PopupDeathSelf = "system-hologram-phasing-death-self";

    [DataField]
    public string ShaderName = "StarlightHologram";

    [DataField]
    public float HologramHue = 0.64f;
}
