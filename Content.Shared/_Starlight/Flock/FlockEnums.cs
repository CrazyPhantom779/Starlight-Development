using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Flock;

/// <summary>What a flock drone's nanite spray is currently going to do.</summary>
[Serializable, NetSerializable]
public enum FlockSprayMode : byte
{
    Convert,
    Repair,
    Barricade,
    Scrap,
}

[Serializable, NetSerializable]
public enum FlockSapperMode : byte
{
    Bits,
    Drones,
    Structures,
}

[Serializable, NetSerializable]
public enum FlockVisuals : byte
{
    State,
    Hibernating,
    Charging,
    Health,
}

[Serializable, NetSerializable]
public enum FlockStructureState : byte
{
    Building,
    Online,
    Offline,
    Broken,
}

[Serializable, NetSerializable]
public enum FlockUiKey : byte
{
    Panel,
}

/// <summary>Generic marker for annotation icons (goon's FLOCK_ANNOTATION_*).</summary>
[Serializable, NetSerializable]
public enum FlockAnnotation : byte
{
    Deconstruct,
    Hazard,
    Priority,
    Reserved,
    Ignore,
}
