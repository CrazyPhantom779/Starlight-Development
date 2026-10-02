using Robust.Shared.Map;

namespace Content.Shared._Starlight.Flock.Components;

/// <summary>
/// The "flock" datum from goonstation: a nullspace entity that owns the whole flock's state.
/// One of these exists per flockmind.
/// </summary>
[RegisterComponent]
public sealed partial class FlockComponent : Component
{
    /// <summary>The flockmind entity (the overmind).</summary>
    [DataField]
    public EntityUid? Flockmind;

    /// <summary>Set once the entry rift opened - dying before this resets the flockmind.</summary>
    [DataField]
    public bool Started;

    /// <summary>The flockmind gets a second chance if killed before 200 compute was ever reached.</summary>
    [DataField]
    public bool SecondChanceUsed;

    [DataField]
    public bool EverReachedSecondChanceCompute;

    public HashSet<EntityUid> Drones = [];
    public HashSet<EntityUid> Bits = [];
    public HashSet<EntityUid> Traces = [];
    public HashSet<EntityUid> Structures = [];
    public HashSet<EntityUid> Tealprints = [];

    public HashSet<EntityUid> Enemies = [];
    public HashSet<EntityUid> Ignored = [];
    public HashSet<EntityUid> DeconstructMarks = [];

    /// <summary>All flock floor tiles (grid, indices).</summary>
    public HashSet<(EntityUid Grid, Vector2i Indices)> FlockTiles = [];
    public HashSet<(EntityUid Grid, Vector2i Indices)> PriorityTiles = [];
    /// <summary>Tiles currently reserved by a drone, to stop several converting the same tile.</summary>
    public Dictionary<(EntityUid Grid, Vector2i Indices), EntityUid> ReservedTiles = [];

    /// <summary>Total compute provided, recomputed periodically.</summary>
    public int TotalCompute;
    public int UsedCompute;

    public int CurrentEggCost = FlockConsts.LayEggCost;

    public bool RelayInProgress;
    public bool RelayFinished;
    public EntityUid? Relay;
    public EntityCoordinates? RelayLocation;

    /// <summary>Structure prototype ids unlocked for tealprinting.</summary>
    public HashSet<string> UnlockedStructures = ["FlockCollector", "FlockSentinel", "FlockInterceptor", "FlockSapper", "FlockGnesisTurret"];

    public HashSet<EntityUid> Seen = [];

    /// <summary>Counters for the round-end statistics.</summary>
    public int StatDronesMade;
    public int StatBitsMade;
    public int StatStructuresMade;
    public int StatTilesConverted;
    public int StatResourcesGained;
    public int StatHumansCaged;
    public int StatDronesLost;
    public TimeSpan Start;
}
