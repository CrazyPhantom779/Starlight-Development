namespace Content.Shared._Starlight.Flock;

/// <summary>
/// Numeric constants ported 1:1 from goonstation's _std/macros/flock.dm.
/// </summary>
public static class FlockConsts
{
    public const int ConvertCost = 20;
    public const int BarricadeCost = 20;
    public const int LayEggCost = 100;
    public const int RepairCost = 10;
    public const int GhostDepositAmount = 10;
    /// <summary>Total compute required to unlock the relay.</summary>
    public const int RelayComputeCost = 500;
    /// <summary>Total flock turf required to unlock the relay.</summary>
    public const int RelayTileRequirement = 250;
    public const int FlocktraceComputeCost = 100;
    public const int DroneLimit = 75;
    public const int DroneCompute = 10;
    public const int DroneComputeHibernate = 15;
    /// <summary>How many times in a row a drone must wander before it hibernates.</summary>
    public const int DroneWanderPauseCount = 5;
    public const float RadioGarbleChance = 0.5f;
    /// <summary>Minimum compute (total) before the first second-chance no longer applies.</summary>
    public const int SecondChanceComputeThreshold = 200;
    public const int MinDesiredPop = 10;
    public const float AdditionalResourceReservationPerDrone = 7.5f;
}
