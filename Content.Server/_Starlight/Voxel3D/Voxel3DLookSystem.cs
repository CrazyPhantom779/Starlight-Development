using Content.Shared._Starlight.Voxel3D;
using Content.Shared.Movement.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Voxel3D;

/// <summary>
/// Listens for where a player in the 3D view is looking. The movement keys are turned to follow that direction (the same
/// mechanism the camera rotation keys use), and the character turns to face it.
/// </summary>
public sealed class Voxel3DLookSystem : EntitySystem
{
    [Dependency] private TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<Voxel3DLookEvent>(OnLook);
    }

    private void OnLook(Voxel3DLookEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        if (!float.IsFinite(msg.Yaw) || Math.Abs(msg.Yaw) > 1000f)
            return;

        if (!TryComp(uid, out InputMoverComponent? mover))
            return;

        var rotation = msg.Active ? new Angle(msg.Yaw) : Angle.Zero;
        mover.TargetRelativeRotation = rotation;
        mover.RelativeRotation = rotation;
        Dirty(uid, mover);

        // An entity at angle 0 faces south, and looking along "yaw" means facing yaw + 180 degrees.
        if (msg.Active)
            _transform.SetLocalRotation(uid, new Angle(msg.Yaw + Math.PI));
    }
}
