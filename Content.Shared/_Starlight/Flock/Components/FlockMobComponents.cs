using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Flock.Components;

/// <summary>The overmind. Incorporeal, player controlled, lives and dies by its drones.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlockmindComponent : Component
{
    /// <summary>The first ability: spawn the entry rift. Removed after use.</summary>
    [DataField]
    public EntProtoId RiftAction = "ActionFlockSpawnRift";

    [DataField, AutoNetworkedField]
    public EntityUid? RiftActionEntity;

    /// <summary>Abilities granted after the rift is placed.</summary>
    [DataField]
    public List<EntProtoId> Actions = new()
    {
        "ActionFlockPing",
        "ActionFlockPanel",
        "ActionFlockDesignateTile",
        "ActionFlockDesignateEnemy",
        "ActionFlockDesignateIgnore",
        "ActionFlockPartitionMind",
        "ActionFlockDiffract",
        "ActionFlockGatecrash",
        "ActionFlockRepairBurst",
        "ActionFlockRadioStun",
        "ActionFlockNarrowbeam",
        "ActionFlockTealprint",
        "ActionFlockMarkDeconstruct",
    };

    public List<EntityUid> ActionEntities = [];

    /// <summary>The rift has been placed.</summary>
    [DataField, AutoNetworkedField]
    public bool RiftPlaced;

    [DataField]
    public TimeSpan PartitionCooldown = TimeSpan.FromSeconds(60);

    /// <summary>Currently possessed drone (the mind lives in the drone while this is set).</summary>
    [DataField]
    public EntityUid? Possessing;
}

/// <summary>A partition of the flockmind. Can only ping, designate enemies and pilot drones.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FlocktraceComponent : Component
{
    [DataField]
    public List<EntProtoId> Actions = new()
    {
        "ActionFlockPing",
        "ActionFlockDesignateEnemy",
    };

    public List<EntityUid> ActionEntities = [];

    [DataField]
    public EntityUid? Possessing;

    /// <summary>Goon's AFK threshold; if the controlling player is idle for this long the trace is released.</summary>
    [DataField]
    public TimeSpan AfkThreshold = TimeSpan.FromSeconds(180);
}

/// <summary>
/// Flockdrone: the corporeal worker unit. Player (flockmind/trace) or HTN controlled.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlockDroneComponent : Component
{
    /// <summary>Resources carried (gnesis from consumed items).</summary>
    [DataField, AutoNetworkedField]
    public int Resources;

    [DataField, AutoNetworkedField]
    public FlockSprayMode SprayMode = FlockSprayMode.Convert;

    /// <summary>Incapacitor charge, 0-100. Each shot costs <see cref="IncapacitorCost"/>.</summary>
    [DataField, AutoNetworkedField]
    public float Charge = 100f;

    [DataField]
    public float MaxCharge = 100f;

    [DataField]
    public float IncapacitorCost = 20f;

    /// <summary>Charge regained per second off flock tiles. Doubled on flock tiles (goon behaviour).</summary>
    [DataField]
    public float ChargePerSecond = 3f;

    [DataField]
    public TimeSpan IncapacitorCooldown = TimeSpan.FromSeconds(1.2);

    public TimeSpan NextShot;

    /// <summary>Items are absorbed in the reclaimer: health removed per second.</summary>
    [DataField]
    public float HealthAbsorbRate = 2f;

    [DataField]
    public int ResourcesPerHealth = 5;

    [DataField, AutoNetworkedField]
    public bool Hibernating;

    [DataField, AutoNetworkedField]
    public bool FloorRunning;

    /// <summary>Consecutive wander ticks, drone hibernates at <see cref="FlockConsts.DroneWanderPauseCount"/>.</summary>
    public int WanderCount;

    /// <summary>Selected by flockmind for orders.</summary>
    [DataField, AutoNetworkedField]
    public bool Selected;

    /// <summary>Terminally broken: can't be repaired, can be butchered.</summary>
    [DataField, AutoNetworkedField]
    public bool Dead;

    [DataField]
    public List<EntProtoId> Actions = new()
    {
        "ActionFlockSpray",
        "ActionFlockSprayMode",
        "ActionFlockIncapacitor",
        "ActionFlockCage",
        "ActionFlockLayEgg",
        "ActionFlockFloorRun",
    };

    public List<EntityUid> ActionEntities = [];

    public TimeSpan NextAiThink;
    public float BaseWalk;
    public float BaseSprint;
}

/// <summary>Flockbit: tiny AI-only converter. Can't attack or be possessed.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FlockBitComponent : Component
{
    [DataField]
    public TimeSpan ConvertDelay = TimeSpan.FromSeconds(3);
    public TimeSpan NextConvert;
    /// <summary>Sapper speed-up multiplier on conversion delay (1 = none).</summary>
    public float SapperBoost = 1f;
}

/// <summary>Marks a corpse produced when a drone dies (goon's flockdrone_debris/odd crystal).</summary>
[RegisterComponent]
public sealed partial class FlockDroneCoreComponent : Component
{
    [DataField]
    public int Resources;
}
