using System.Numerics;
using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Holograms.Components;

/// <summary>
/// Marks the entity as a hardlight hologram.
/// The client visualizer intentionally mirrors holopad hologram rendering while
/// adding Starlight projection scanlines, noise, and flicker through shader data.
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
    /// Use HologramProjection for Starlight hardlight mobs. The visualizer also
    /// keeps compatibility with the upstream holopad Hologram shader.
    /// </summary>
    [DataField]
    public string ShaderName = "HologramProjection";

    /// <summary>
    /// Primary glow color for HologramProjection and holopad-compatible shaders.
    /// </summary>
    [DataField]
    public Color Color1 = Color.FromHex("#65b8e2");

    /// <summary>
    /// Secondary shadow color for HologramProjection and holopad-compatible shaders.
    /// </summary>
    [DataField]
    public Color Color2 = Color.FromHex("#3a6981");

    /// <summary>
    /// HSV hue used by Starlight-style projection shaders. 0.64 is blue/cyan.
    /// </summary>
    [DataField]
    public float Hue = 0.64f;

    /// <summary>
    /// Minimum saturation pushed into the projected sprite.
    /// </summary>
    [DataField]
    public float Saturation = 0.85f;

    /// <summary>
    /// Final transparency multiplier.
    /// </summary>
    [DataField]
    public float Alpha = 0.9f;

    /// <summary>
    /// Glow/brightness multiplier.
    /// </summary>
    [DataField]
    public float Intensity = 1.25f;

    /// <summary>
    /// How much the shader's color ramp replaces the original sprite colors.
    /// </summary>
    [DataField]
    public float ColorBlend = 0.45f;

    /// <summary>
    /// Strength of moving scanlines.
    /// </summary>
    [DataField]
    public float ScanlineOpacity = 0.32f;

    /// <summary>
    /// Strength of animated projection noise.
    /// </summary>
    [DataField]
    public float NoiseOpacity = 0.26f;

    /// <summary>
    /// Strength of alpha pulsing/flicker.
    /// </summary>
    [DataField]
    public float FlickerStrength = 0.16f;

    /// <summary>
    /// Scanline scroll speed for HologramProjection.
    /// </summary>
    [DataField]
    public float LineScrollSpeed = 0.12f;

    /// <summary>
    /// Time multiplier used by the upstream holopad-compatible Hologram shader.
    /// HologramProjection uses shader TIME directly, but this is kept for compatibility.
    /// </summary>
    [DataField]
    public float ScrollRate = 0.125f;

    /// <summary>
    /// Visibility used when the hologram has StealthComponent.
    /// </summary>
    [DataField]
    public float DefaultVisibility = 0.8f;

    /// <summary>
    /// Sprite offset used for the holopad-style projection treatment.
    /// </summary>
    [DataField]
    public Vector2 Offset = new(-0.02f, 0.45f);

    /// <summary>
    /// Holopad holograms are static and force south-facing/no-rotation.
    /// Normal hardlight hologram mobs should keep their movement direction, so this is opt-in.
    /// </summary>
    [DataField]
    public bool ForceHolopadFacing;
}
