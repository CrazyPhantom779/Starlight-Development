using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Starlight.Holograms.Components;

/// <summary>
/// Marks a hologram as an active projection that must stay connected to a projector.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HologramProjectedComponent : Component
{
    /// <summary>
    /// Whitelist used to restrict which projectors this hologram can use.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public EntityWhitelist ValidProjectorWhitelist = new();

    /// <summary>
    /// Grace time after leaving projector range or line of sight before the hologram is returned.
    /// Kept short so portals/teleports/walls snap the projection back quickly, but not instantly enough
    /// to punish brief door movement.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public TimeSpan GracePeriod = TimeSpan.FromSeconds(0.75);

    /// <summary>
    /// Short grace used when the current projector is still in range but line-of-sight is blocked.
    /// This makes walls and closed doors cut projections quickly without making range-edge movement harsh.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public TimeSpan OcclusionGracePeriod = TimeSpan.FromSeconds(0.15);

    /// <summary>
    /// How often the server revalidates projector connectivity.
    /// </summary>
    [DataField]
    public TimeSpan ValidationInterval = TimeSpan.FromSeconds(0.05);

    /// <summary>
    /// Next server-side time this projection should revalidate its projector.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextProjectorCheck = TimeSpan.Zero;

    /// <summary>
    /// Prototype of the client-side projection beam/effect entity.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    [AutoNetworkedField]
    public string? EffectPrototype;

    /// <summary>
    /// Whether the hologram's eye should snap to the projector it is emitted from.
    /// Walking hardlight bodies should leave this false.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool SetEyeTarget;

    /// <summary>
    /// Current projector this hologram is connected to.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
    public NetEntity? CurProjector;

    /// <summary>
    /// If set, the hologram can only be projected from this projector.
    /// Used by portable/briefcase-style projectors.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
    public NetEntity? ProjectorOverride;

    /// <summary>
    /// Whether the hologram is currently connected to a valid projector.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
    public bool CurrentlyInProjector;

    /// <summary>
    /// Server-side time when the hologram should return if it does not reconnect to a projector.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan VanishTime = TimeSpan.Zero;

    /// <summary>
    /// Client-side visual projection effect entity.
    /// </summary>
    public EntityUid? EffectEntity;
}
