using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CrewMonitoring;

[RegisterComponent, NetworkedComponent]
public sealed partial class CrewMonitoringFilterComponent : Component
{
    /// <summary>
    ///     List of departments which this console can see. If empty, unrestricted.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<string> ShownDepartments = [];

    /// <summary>
    ///     Always show crew with tracking implants in addition.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool AlwaysShowTrackingImplants = false;

    /// <summary>
    ///     Only show crew who are wounded or dead.
    /// <summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool OnlyShowWoundedOrDead = false;

    /// <summary>
    ///     List of factions the console can see.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<string> ShownFactions = ["crew"];
}
