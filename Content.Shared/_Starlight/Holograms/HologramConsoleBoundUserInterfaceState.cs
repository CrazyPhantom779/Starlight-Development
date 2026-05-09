using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Holograms;

[Serializable, NetSerializable]
public sealed class HologramConsoleBoundUserInterfaceState(
    List<BladeServerInfo> bladeServers,
    NetEntity? activeHologram,
    List<ProjectorInfo> projectors,
    Dictionary<NetEntity, NetCoordinates> projectorCoordinates,
    bool isPortable = false,
    float? batteryPercent = null,
    bool allowCarry = false,
    int activeCount = 0,
    int maxActive = 0,
    int maxBladeServerSlots = 8,
    bool showMap = true,
    bool showProjectButton = true,
    bool showRecallButton = true,
    bool showBladeServerPanel = true,
    bool hasServer = true) : BoundUserInterfaceState
{
    public List<BladeServerInfo> BladeServers { get; init; } = bladeServers;
    public NetEntity? ActiveHologram { get; init; } = activeHologram;
    public List<ProjectorInfo> Projectors { get; init; } = projectors;
    public Dictionary<NetEntity, NetCoordinates> ProjectorCoordinates { get; init; } = projectorCoordinates;

    public bool IsPortable { get; init; } = isPortable;
    public float? BatteryPercent { get; init; } = batteryPercent;
    public bool AllowCarry { get; init; } = allowCarry;
    public int ActiveCount { get; init; } = activeCount;
    public int MaxActive { get; init; } = maxActive;
    public int MaxBladeServerSlots { get; init; } = maxBladeServerSlots;
    public bool ShowMap { get; init; } = showMap;
    public bool ShowProjectButton { get; init; } = showProjectButton;
    public bool ShowRecallButton { get; init; } = showRecallButton;
    public bool ShowBladeServerPanel { get; init; } = showBladeServerPanel;
    public bool HasServer { get; init; } = hasServer;
}

[Serializable, NetSerializable]
public sealed class BladeServerInfo
{
    public NetEntity Uid { get; init; }
    public string HologramName { get; init; }
    public bool IsActive { get; init; }
    public NetEntity? ActiveHologram { get; init; }
    public NetEntity? CurrentProjector { get; init; }

    public BladeServerInfo(
        NetEntity uid,
        string hologramName,
        bool isActive,
        NetEntity? activeHologram = null,
        NetEntity? currentProjector = null)
    {
        Uid = uid;
        HologramName = hologramName;
        IsActive = isActive;
        ActiveHologram = activeHologram;
        CurrentProjector = currentProjector;
    }
}

[Serializable, NetSerializable]
public sealed class ProjectorInfo
{
    public NetEntity Uid { get; init; }
    public string Name { get; init; }
    public string Location { get; init; }

    public ProjectorInfo(NetEntity uid, string name, string location)
    {
        Uid = uid;
        Name = name;
        Location = location;
    }
}

[Serializable, NetSerializable]
public sealed class HologramConsoleProjectHologramMessage(NetEntity bladeServerUid, NetEntity projectorUid) : BoundUserInterfaceMessage
{
    public NetEntity BladeServerUid { get; } = bladeServerUid;
    public NetEntity ProjectorUid { get; } = projectorUid;
}

[Serializable, NetSerializable]
public sealed class HologramConsoleRecallMessage(NetEntity? bladeServerUid = null) : BoundUserInterfaceMessage
{
    public NetEntity? BladeServerUid { get; } = bladeServerUid;
}

[Serializable, NetSerializable]
public sealed class HologramConsoleEjectBladeServerMessage : BoundUserInterfaceMessage
{
    public NetEntity BladeServerUid { get; }

    public HologramConsoleEjectBladeServerMessage(NetEntity bladeServerUid) =>
        BladeServerUid = bladeServerUid;
}

[Serializable, NetSerializable]
public sealed class HologramConsoleToggleCarryMessage(bool allowCarry) : BoundUserInterfaceMessage
{
    public bool AllowCarry { get; } = allowCarry;
}

[Serializable, NetSerializable]
public enum HologramConsoleUiKey : byte
{
    Key
}
