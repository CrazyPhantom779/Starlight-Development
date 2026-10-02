using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Flock.Components;

/// <summary>
/// Present on everything that belongs to a flock: mobs, traces, structures, converted entities.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlockMemberComponent : Component
{
    /// <summary>The flock entity (holder of <see cref="FlockComponent"/>) this entity belongs to.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Flock;
}

/// <summary>
/// Marks a station entity (door, wall, window, etc.) that has been converted by the flock.
/// Flock walls/doors/etc are destroyed by simply hitting them, and refund resources when scrapped.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FlockConvertedComponent : Component
{
    /// <summary>Resource value returned to the scrapper.</summary>
    [DataField]
    public int ScrapValue = 20;
}

/// <summary>
/// Added to a creature that the flock has designated as an enemy. Drones/turrets will target it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlockEnemyComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Flock;
}

/// <summary>Added to something the flock should leave alone (Designate Ignore).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlockIgnoredComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Flock;
}

/// <summary>Visible annotation icon over targets (priority tile / deconstruct / hazard).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlockAnnotatedComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<FlockAnnotation> Annotations = [];
}

/// <summary>
/// Entities that can't be marked for deconstruction by flockmind (eggs, rifts, drones, relay, flock tiles).
/// </summary>
[RegisterComponent]
public sealed partial class FlockDeconImmuneComponent : Component;
