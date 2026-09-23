namespace Content.Server._Starlight.Holograms.Components;

/// <summary>
/// A console that lets you manage hologram blade servers and pick a projector to send a
/// hologram to. Works the same way whether it's bolted to a wall or riding around in a
/// briefcase - see IsPortable below for what actually changes between the two.
/// </summary>
[RegisterComponent]
public sealed partial class HologramConsoleComponent : Component
{
    /// <summary>
    /// Max holograms this console can have active at once.
    /// </summary>
    [DataField]
    public int MaxActiveHolograms = 2;

    /// <summary>
    /// Whether holograms can pick up and carry this console around.
    /// </summary>
    [DataField]
    public bool AllowHologramCarry;

    /// <summary>
    /// Declares this console as portable (e.g. a briefcase) instead of a fixed station computer.
    /// This is a plain yes/no flag rather than something inferred from what other components
    /// happen to be on the entity, so a console can't accidentally end up "portable" just
    /// because it has an unrelated item slot with a matching name.
    ///
    /// Portable consoles skip the projector map/selection entirely and only ever project from
    /// themselves - see HologramConsoleSystem.TryProjectSelected.
    /// </summary>
    [DataField]
    public bool IsPortable;

    /// <summary>
    /// Name of the item slot a portable console keeps its blade server in. Only read when
    /// IsPortable is true.
    /// </summary>
    [DataField]
    public string PortableSlotId = "blade_server_slot";

    /// <summary>
    /// Show the projector map and projector selection.
    /// </summary>
    [DataField]
    public bool ShowMap = true;

    /// <summary>
    /// Show the Project button.
    /// </summary>
    [DataField]
    public bool ShowProjectButton = true;

    /// <summary>
    /// Show the Recall button.
    /// </summary>
    [DataField]
    public bool ShowRecallButton = true;

    /// <summary>
    /// Show the blade server list sidebar.
    /// </summary>
    [DataField]
    public bool ShowBladeServerPanel = true;

    /// <summary>
    /// Show the settings panel (right now that's just the "allow hologram carry" checkbox).
    /// </summary>
    [DataField]
    public bool ShowSettingsPanel = true;

    /// <summary>
    /// Show the "N active / max" readout.
    /// </summary>
    [DataField]
    public bool ShowActiveCount = true;

    /// <summary>
    /// Show the panel summarizing the current blade server + projector selection.
    /// </summary>
    [DataField]
    public bool ShowSelectionPanel = true;

    /// <summary>
    /// Controls the header battery gauge. See <see cref="HologramBatteryDisplayMode"/>.
    /// </summary>
    [DataField]
    public HologramBatteryDisplayMode BatteryDisplay = HologramBatteryDisplayMode.ShownIfBatteryPowered;

    /// <summary>
    /// Power draw per active hologram, in watts.
    /// </summary>
    [DataField]
    public float PowerDrawPerHologram = 50f;

    /// <summary>
    /// Portable mode only: which active hologram belongs to which inserted blade server.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, EntityUid> ActiveHolograms = [];
}

/// <summary>
/// How a console's header battery gauge decides whether to show itself.
/// </summary>
public enum HologramBatteryDisplayMode : byte
{
    /// <summary>Never show it, even if the console does have a battery.</summary>
    Hidden,

    /// <summary>Always show it. If there's no battery to read, it just says so.</summary>
    Shown,

    /// <summary>Show it only if the console actually has a power cell slot to read from. The default.</summary>
    ShownIfBatteryPowered,
}
