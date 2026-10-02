using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Flock.Roles;

/// <summary>Added to the mind role entity of the flockmind.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FlockmindRoleComponent : BaseMindRoleComponent;

/// <summary>Added to the mind role entity of a flocktrace.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FlocktraceRoleComponent : BaseMindRoleComponent;
