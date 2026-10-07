using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Wizard.Casting;

/// <summary>How much of one offering is lying on the circle.</summary>
[Serializable, NetSerializable]
public sealed class OfferingInfo(string label, int have, int need)
{
    public readonly string Label = label;
    public readonly int Have = have;
    public readonly int Need = need;
}

/// <summary>A rite as the Ritual tab shows it.</summary>
[Serializable, NetSerializable]
public sealed class RitualInfo(
    string id,
    string name,
    string description,
    List<OfferingInfo> offerings,
    bool satisfied,
    float windCost,
    float seconds)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly List<OfferingInfo> Offerings = offerings;
    public readonly bool Satisfied = satisfied;
    public readonly float WindCost = windCost;
    public readonly float Seconds = seconds;
}
