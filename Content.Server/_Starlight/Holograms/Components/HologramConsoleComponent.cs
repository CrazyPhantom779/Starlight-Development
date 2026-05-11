namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// Console that allows hologram blade server management and projector selection.
/// Supports both stationary and portable modes.
/// </summary>
[RegisterComponent]
public sealed partial class HologramConsoleComponent : Component
{
    /// <summary>
    /// Maximum number of holograms that can be projected simultaneously.
    /// </summary>
    [DataField]
    public int MaxActiveHolograms = 2;

    /// <summary>
    /// Whether holograms are allowed to hold/carry this device.
    /// </summary>
    [DataField]
    public bool AllowHologramCarry;

    /// <summary>
    /// Whether to show the projector map and selection interface.
    /// </summary>
    [DataField]
    public bool ShowMap = true;

    /// <summary>
    /// Whether to show the project button.
    /// </summary>
    [DataField("showProjectButton")]
    public bool ShowProjectButton = true;

    /// <summary>
    /// Whether to show the recall button.
    /// </summary>
    [DataField]
    public bool ShowRecallButton = true;

    /// <summary>
    /// Whether to show the blade server panel sidebar.
    /// </summary>
    [DataField]
    public bool ShowBladeServerPanel = true;

    /// <summary>
    /// Power draw per active hologram in watts.
    /// </summary>
    [DataField]
    public float PowerDrawPerHologram = 50f;

    /// <summary>
    /// Portable mode only. Maps inserted blade server UIDs to their active holograms.
    /// </summary>
    [DataField("activeHolograms")]
    public Dictionary<EntityUid, EntityUid> ActiveHolograms = [];
}
