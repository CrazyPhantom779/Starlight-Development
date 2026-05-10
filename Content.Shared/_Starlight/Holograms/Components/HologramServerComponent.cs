namespace Content.Shared._Starlight.Holograms;

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
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? LinkedHologram;
}
