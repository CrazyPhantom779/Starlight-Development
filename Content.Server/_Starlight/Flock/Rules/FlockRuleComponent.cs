using Robust.Shared.Map;

namespace Content.Server._Starlight.Flock.Rules;

/// <summary>Game rule for the Flockmind midround antagonist.</summary>
[RegisterComponent]
public sealed partial class FlockRuleComponent : Component
{
    /// <summary>Where the flockmind appears: the centre of the target station's largest grid.</summary>
    [DataField]
    public MapCoordinates? Coords;

    [DataField]
    public EntityUid? Flock;

    /// <summary>The relay was built (for the round-end text).</summary>
    [DataField]
    public bool Won;
}
