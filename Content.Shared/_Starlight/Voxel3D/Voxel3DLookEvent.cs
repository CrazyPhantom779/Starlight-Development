using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Voxel3D;

/// <summary>
/// Sent by a client in the first-person 3D view to say which way it is looking, so walking follows the view and the
/// character faces where the player looks. Yaw is in radians, relative to the grid the player stands on: 0 looks north
/// and positive turns toward the west. When <see cref="Active"/> is false the 3D view was switched off and everything
/// goes back to normal.
/// </summary>
[Serializable, NetSerializable]
public sealed class Voxel3DLookEvent : EntityEventArgs
{
    public readonly float Yaw;
    public readonly bool Active;

    public Voxel3DLookEvent(float yaw, bool active)
    {
        Yaw = yaw;
        Active = active;
    }
}
