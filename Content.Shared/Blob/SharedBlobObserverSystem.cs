using Content.Shared.Movement.Events;

namespace Content.Shared.Blob;

public abstract partial class SharedBlobObserverSystem: EntitySystem
{
    [SubscribeLocalEvent]
    private void OnUpdateCanMove(EntityUid uid, BlobObserverComponent component, UpdateCanMoveEvent args)
    {
        if (component.CanMove)
            return;

        args.Cancel();
    }
}
