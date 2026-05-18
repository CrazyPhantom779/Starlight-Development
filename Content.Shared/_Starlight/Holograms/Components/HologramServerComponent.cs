namespace Content.Shared._Starlight.Holograms;

/// <summary>
/// Runtime holder for hologram projections owned by a station/server entity.
/// Kept shared because several existing systems reference this component from
/// shared hologram namespaces; all mutation should remain server-side.
/// </summary>
[RegisterComponent]
public sealed partial class HologramServerComponent : Component
{
    /// <summary>
    /// Active holograms owned by this server, keyed by the blade server that produced them.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<EntityUid, EntityUid> ActiveHolograms = [];

    /// <summary>
    /// Legacy single-hologram field kept so old maps/saves and older code paths do not explode.
    /// Remove after all server code has migrated to ActiveHolograms.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? LinkedHologram;
}
