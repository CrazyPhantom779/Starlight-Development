using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Flock.Components;

/// <summary>Shared data for every flock structure (goon's /obj/flock_structure).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlockStructureComponent : Component
{
    /// <summary>Short in-game id used in the flock panel / tealprint menu.</summary>
    [DataField(required: true)]
    public string FlockId = string.Empty;

    [DataField]
    public string FlockDesc = string.Empty;

    /// <summary>Resources to build via tealprint.</summary>
    [DataField]
    public int ResourceCost;

    /// <summary>Compute the structure provides while online.</summary>
    [DataField]
    public int Compute;

    /// <summary>Compute the structure consumes while online (e.g. sentinel targeting).</summary>
    [DataField]
    public int OnlineComputeCost;

    /// <summary>Seconds a structure takes to complete itself (eggs/rifts/relay); 0 = instant.</summary>
    [DataField]
    public float BuildTime;

    [DataField, AutoNetworkedField]
    public FlockStructureState State = FlockStructureState.Online;

    /// <summary>True when the flock can afford the compute and the structure is functional.</summary>
    [DataField, AutoNetworkedField]
    public bool Powered = true;

    [DataField]
    public TimeSpan BuildStart;
}

/// <summary>A structure ghost showing where a drone should deposit resources.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlockTealprintComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Structure;

    [DataField, AutoNetworkedField]
    public int Required = 100;

    [DataField, AutoNetworkedField]
    public int Deposited;
}

[RegisterComponent]
public sealed partial class FlockEggComponent : Component
{
    /// <summary>What hatches. Drone egg or bit egg.</summary>
    [DataField]
    public EntProtoId Hatch = "MobFlockDrone";
    [DataField]
    public int Count = 1;
    [DataField]
    public TimeSpan HatchTime = TimeSpan.FromSeconds(6);
    public TimeSpan HatchAt;
}

[RegisterComponent]
public sealed partial class FlockRiftComponent : Component
{
    [DataField]
    public TimeSpan EntryTime = TimeSpan.FromSeconds(10);
    public TimeSpan OpenAt;
    [DataField]
    public int StartingDrones = 4;
    [DataField]
    public int StartingCaches = 4;
    [DataField]
    public int CacheResources = 40;
    [DataField]
    public int Sentinels = 2;
    [DataField]
    public EntProtoId DroneEgg = "FlockEgg";
    [DataField]
    public EntProtoId BitEgg = "FlockBitEgg";
    [DataField]
    public EntProtoId Cache = "FlockResourceCache";
    [DataField]
    public EntProtoId SentinelProto = "FlockSentinel";
}

/// <summary>Holds resources dropped by rifts / destroyed drones; consumed by drones.</summary>
[RegisterComponent]
public sealed partial class FlockCacheComponent : Component
{
    [DataField]
    public int Resources = 40;
}

[RegisterComponent]
public sealed partial class FlockCollectorComponent : Component
{
    [DataField]
    public int MaxRange = 4;
    [DataField]
    public int ComputePerTile = 5;
    [DataField]
    public TimeSpan CycleTime = TimeSpan.FromSeconds(20);
    public TimeSpan NextCycle;
    public int ConnectedTiles;
}

[RegisterComponent]
public sealed partial class FlockComputeNodeComponent : Component;

[RegisterComponent]
public sealed partial class FlockSentinelComponent : Component
{
    [DataField] public float ChargePerSecond = 10f;
    [DataField] public float Range = 4f;
    [DataField] public int ChainTargets = 2;
    [DataField] public float Wattage = 5000f;
    public float Charge;
    public float MaxCharge = 100f;
    public TimeSpan NextZap;
}

[RegisterComponent]
public sealed partial class FlockInterceptorComponent : Component
{
    [DataField] public float Radius = 2f;
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(1);
    public TimeSpan NextShot;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlockSapperComponent : Component
{
    [DataField, AutoNetworkedField]
    public FlockSapperMode Mode = FlockSapperMode.Drones;
    [DataField] public float Range = 5f;
    [DataField] public float PowerPerSecond = 500f;
}

[RegisterComponent]
public sealed partial class FlockGnesisTurretComponent : Component
{
    [DataField] public float Range = 8f;
    [DataField] public int FluidMax = 250;
    [DataField] public float FluidPerSecond = 5f;
    [DataField] public int FluidPerShot = 10;
    [DataField] public int Spikes = 4;
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(3);
    public float Fluid;
    public TimeSpan NextShot;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlockRelayComponent : Component
{
    [DataField] public TimeSpan ChargeTime = TimeSpan.FromSeconds(360);
    [DataField] public TimeSpan FinalChargeTime = TimeSpan.FromSeconds(18);
    [DataField] public int MaxConversionRadius = 15;
    [DataField, AutoNetworkedField] public int ConversionRadius = 1;
    [DataField, AutoNetworkedField] public TimeSpan ChargeStart;
    [DataField, AutoNetworkedField] public bool Charging;
    [DataField, AutoNetworkedField] public bool Fired;
    public TimeSpan NextSound;
    public TimeSpan NextConvert;
    [DataField] public TimeSpan SoundLength = TimeSpan.FromSeconds(27);
    [DataField] public TimeSpan BuildDelay = TimeSpan.FromSeconds(30);
    public TimeSpan BuildAt;
    [DataField] public int DestroyRadius = 20;
}

/// <summary>Energy cage that slowly consumes an imprisoned mob into drones.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlockCageComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Occupant;
    [DataField] public TimeSpan Interval = TimeSpan.FromSeconds(8);
    public TimeSpan NextEat;
    [DataField] public EntProtoId EggProto = "FlockEgg";
    [DataField] public TimeSpan EscapeTime = TimeSpan.FromSeconds(12);
}
