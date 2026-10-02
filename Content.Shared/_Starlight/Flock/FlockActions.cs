using Robust.Shared.Map;
using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Flock;

// ---- Flockmind / Flocktrace abilities ----
public sealed partial class FlockSpawnRiftEvent : WorldTargetActionEvent;
public sealed partial class FlockPingEvent : WorldTargetActionEvent;
public sealed partial class FlockOpenPanelEvent : InstantActionEvent;
public sealed partial class FlockDesignateTileEvent : WorldTargetActionEvent;
public sealed partial class FlockDesignateEnemyEvent : EntityTargetActionEvent;
public sealed partial class FlockDesignateIgnoreEvent : EntityTargetActionEvent;
public sealed partial class FlockPartitionMindEvent : InstantActionEvent;
public sealed partial class FlockDiffractEvent : EntityTargetActionEvent;
public sealed partial class FlockGatecrashEvent : InstantActionEvent;
public sealed partial class FlockRepairBurstEvent : WorldTargetActionEvent;
public sealed partial class FlockRadioStunEvent : WorldTargetActionEvent;
public sealed partial class FlockNarrowbeamEvent : EntityTargetActionEvent;
public sealed partial class FlockTealprintEvent : WorldTargetActionEvent
{
    /// <summary>Structure prototype chosen in the radial; filled by the system through the BUI.</summary>
    [DataField]
    public string? Structure;
}
public sealed partial class FlockMarkDeconstructEvent : EntityTargetActionEvent;

// ---- Drone abilities ----
public sealed partial class FlockLayEggEvent : InstantActionEvent;
public sealed partial class FlockEjectEvent : InstantActionEvent;
public sealed partial class FlockFloorRunEvent : InstantActionEvent;
public sealed partial class FlockSprayModeEvent : InstantActionEvent;
public sealed partial class FlockIncapacitorEvent : WorldTargetActionEvent;
public sealed partial class FlockSprayEvent : WorldTargetActionEvent;
public sealed partial class FlockCageEvent : WorldTargetActionEvent;
/// <summary>Clicking a drone with a flockmind to take control.</summary>
public sealed partial class FlockTakeControlEvent : EntityTargetActionEvent;

// ---- AI / HTN raised events ----
/// <summary>Raised by HTN on a drone to perform whatever the planner decided.</summary>
[Serializable, DataDefinition]
public sealed partial class FlockDroneAiActEvent : EntityEventArgs
{
    [DataField]
    public FlockAiAct Act;
    public FlockDroneAiActEvent() { }
    public FlockDroneAiActEvent(FlockAiAct act) => Act = act;
}

[Serializable, NetSerializable]
public enum FlockAiAct : byte
{
    Cage,
    Shoot,
    LayEgg,
    Repair,
    Convert,
    Harvest,
    OpenContainer,
    Hibernate,
    Wander,
}

// ---- UI ----
[Serializable, NetSerializable]
public sealed class FlockPanelState : BoundUserInterfaceState
{
    public int TotalCompute;
    public int UsedCompute;
    public int FlockTiles;
    public int RelayCompute;
    public int RelayTiles;
    public bool RelayUnlocked;
    public bool RelayBuilt;
    public int EggCost;
    public List<FlockPanelEntry> Drones = new();
    public List<FlockPanelEntry> Traces = new();
    public List<FlockPanelEntry> Structures = new();
    public List<FlockPanelEntry> Enemies = new();
}

[Serializable, NetSerializable]
public sealed class FlockPanelEntry
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public string Extra = string.Empty;
    public float Health = 1f;
    public int Resources;
}

[Serializable, NetSerializable]
public sealed class FlockPanelJumpMessage(NetEntity target) : BoundUserInterfaceMessage
{
    public NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class FlockPanelReleaseTraceMessage(NetEntity target) : BoundUserInterfaceMessage
{
    public NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class FlockPanelControlMessage(NetEntity target) : BoundUserInterfaceMessage
{
    public NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class FlockTealprintChoiceMessage(string structure, NetCoordinates at) : BoundUserInterfaceMessage
{
    public string Structure = structure;
    public NetCoordinates At = at;
}

[Serializable, NetSerializable]
public sealed class FlockTealprintMenuState : BoundUserInterfaceState
{
    public List<(string Proto, string Name, int Cost)> Available = new();
    public NetCoordinates At;
}

[Serializable, NetSerializable]
public sealed class FlockSapperModeMessage(FlockSapperMode mode) : BoundUserInterfaceMessage
{
    public FlockSapperMode Mode = mode;
}
