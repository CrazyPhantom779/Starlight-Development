using System.Numerics;
using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Holograms.Components;

/// <summary>
/// Marks the entity as a hardlight hologram.
/// Holograms use a dedicated projection shader, but are still real solid mobs.
/// Keep static holopad presentation options opt-in so walking bodies stay aligned with their physics.
/// </summary>
[RegisterComponent]
public sealed partial class HologramComponent : Component
{
    [DataField]
    public SoundSpecifier OnSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/Holograms/holo_on.ogg");

    [DataField]
    public SoundSpecifier OffSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/Holograms/holo_off.ogg");

    [DataField]
    public string PopupAppearOther = "system-hologram-phasing-appear-others";

    [DataField]
    public string PopupAppearSelf = "system-hologram-phasing-appear-self";

    [DataField]
    public string PopupDisappearOther = "system-hologram-phasing-disappear-others";

    [DataField]
    public string PopupDeathSelf = "system-hologram-phasing-death-self";

    /// <summary>
    /// Name of the post-shader used by projected hardlight holograms.
    /// </summary>
    [DataField]
    public string ShaderName = "HologramProjection";

    [DataField]
    public Color Color1 = Color.FromHex("#65b8e2");

    [DataField]
    public Color Color2 = Color.FromHex("#3a6981");

    [DataField]
    public float Hue = 0.64f;

    [DataField]
    public float Saturation = 0.95f;

    [DataField]
    public float Alpha = 0.82f;

    [DataField]
    public float Intensity = 1.6f;

    [DataField]
    public float ColorBlend = 0.6f;

    [DataField]
    public float ScanlineOpacity = 0.45f;

    [DataField]
    public float NoiseOpacity = 0.35f;

    [DataField]
    public float FlickerStrength = 0.2f;

    [DataField]
    public float LineScrollSpeed = 0.14f;

    /// <summary>
    /// Time multiplier used by the upstream holopad-compatible Hologram shader.
    /// HologramProjection uses shader TIME directly, but this remains for fallback compatibility.
    /// </summary>
    [DataField]
    public float ScrollRate = 0.125f;

    /// <summary>
    /// Visibility used when the hologram has StealthComponent.
    /// </summary>
    [DataField]
    public float DefaultVisibility = 0.9f;

    /// <summary>
    /// Visual offset for static holopad-like projections.
    /// Walking hardlight mobs default to zero so the sprite remains centered on the body/eye/collision.
    /// </summary>
    [DataField]
    public Vector2 Offset = Vector2.Zero;

    /// <summary>
    /// Holopad caller projections are static and force south-facing/no-rotation.
    /// Normal hardlight mobs should keep their normal humanoid direction visuals, so this is opt-in.
    /// </summary>
    [DataField]
    public bool ForceHolopadFacing;
}
