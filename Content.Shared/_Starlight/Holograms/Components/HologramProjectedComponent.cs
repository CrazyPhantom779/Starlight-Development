using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Starlight.Holograms.Components;

/// <summary>
/// Marks that this Hologram is projected from cameras, or some other hologram projector source.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HologramProjectedComponent : Component
{
    /// <summary>
    /// A whitelist to check for on projectors, to determine if they're valid.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public EntityWhitelist ValidProjectorWhitelist = new();

    /// <summary>
    /// A timer for a grace period before the Holo is returned, to allow for moving through doors.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public TimeSpan GracePeriod = TimeSpan.FromSeconds(0.1f);

    /// <summary>
    /// The prototype of the effect to spawn for the Hologram's projection.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    [AutoNetworkedField]
    public string? EffectPrototype;

    /// <summary>
    /// Whether or not the Hologram's vision should snap to the projector they're projected from.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool SetEyeTarget;

    /// <summary>
    /// The current projector the hologram is connected to.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
    public NetEntity? CurProjector;

    /// <summary>
    /// If set, the Hologram will only be able to be projected from this projector.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
    public NetEntity? ProjectorOverride;

    /// <summary>
    /// Whether or not the Hologram is currently in the range of a projector.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
    public bool CurrentlyInProjector;

    /// <summary>
    /// The point at which a Hologram will be sent back to their last projector or killed.
    /// </summary>
    public TimeSpan VanishTime = TimeSpan.Zero;

    /// <summary>
    /// The UID of the entity for the Hologram's visual projection effect. Client side only.
    /// </summary>
    public EntityUid? EffectEntity;
}
