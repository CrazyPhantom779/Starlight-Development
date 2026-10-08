using Content.Shared._Starlight.Wizard.Casting;

namespace Content.Server._Starlight.Wizard.Casting;

/// <summary>One errand a wizard is working on.</summary>
public sealed class ActiveErrand(ErrandPrototype proto)
{
    public readonly ErrandPrototype Proto = proto;
    public int Progress;

    /// <summary>Creatures already counted, for errands that count each one once.</summary>
    public readonly HashSet<EntityUid> Seen = [];
}

/// <summary>The errands a wizard is working on, and how many they have finished.</summary>
[RegisterComponent]
public sealed partial class ErrandComponent : Component
{
    [ViewVariables]
    public List<ActiveErrand> Active = [];

    [ViewVariables]
    public int Completed;
}
