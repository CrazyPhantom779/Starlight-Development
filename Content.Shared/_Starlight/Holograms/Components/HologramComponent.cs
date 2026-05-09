using Content.Shared.Whitelist;
using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Holograms;

/// <summary>
///     Marks the entity as a being made of light.
///     Details determined by sister components.
/// </summary>
[RegisterComponent]
public sealed partial class HologramComponent : Component
{
    /// <summary>
    ///     The sound to play when the Hologram is turned on.
    /// </summary>
    [DataField]
    public SoundSpecifier OnSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/Holograms/holo_on.ogg");

    /// <summary>
    ///     The sound to play when the Hologram is turned off.
    /// </summary>
    [DataField]
    public SoundSpecifier OffSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/Holograms/holo_off.ogg");

    /// <summary>
    ///     The string to use for the popup when the Hologram appears, shown to others.
    /// </summary>
    [DataField]
    public string PopupAppearOther = "system-hologram-phasing-appear-others";

    /// <summary>
    ///     The string to use for the popup when the Hologram appears, shown to themselves.
    /// </summary>
    [DataField]
    public string PopupAppearSelf = "system-hologram-phasing-appear-self";

    /// <summary>
    ///     The string to use for the popup when the Hologram disappears, shown to others.
    /// </summary>
    [DataField]
    public string PopupDisappearOther = "system-hologram-phasing-disappear-others";

    /// <summary>
    ///     The string to use for the popup when the Hologram is killed, shown to themselves.
    /// </summary>
    [DataField]
    public string PopupDeathSelf = "system-hologram-phasing-death-self";

    /// <summary>
    ///     The string to use for the popup when the Hologram fails to interact with something, due to their non-solid nature.
    /// </summary>
    [DataField]
    public string PopupHoloInteractionFail = "system-hologram-interaction-with-others-fail";

    /// <summary>
    ///     The string to use for the popup when the someone fails to interact with the Hologram, due to their non-holographic nature.
    /// </summary>
    [DataField]
    public string PopupInteractionWithHoloFail = "system-hologram-interaction-with-holo-fail";

    /// <summary>
    ///     A list of tags for the Hologram to collide with, assuming they're not hardlight.
    /// </summary>
    /// <remarks>
    ///     This should generally include the 'Wall' tag.
    /// </remarks>
    [DataField]
    public EntityWhitelist CollideWhitelist = new();

    [DataField]
    public string ShaderName = "StarlightHologram";

    [DataField]
    public float HologramHue = 0.64f;
}
