using Content.Shared.Blob;
using Robust.Shared.Player;

namespace Content.Server.Blob;

public sealed partial class BlobSpawnerSystem : EntitySystem
{
    [Dependency] private BlobCoreSystem _blobCoreSystem = default!;

    [SubscribeLocalEvent]
    private void OnPlayerAttached(EntityUid uid, BlobSpawnerComponent component, PlayerAttachedEvent args)
    {
        var xform = Transform(uid);

        var core = Spawn(component.CoreBlobPrototype, xform.Coordinates);

        if (!TryComp<BlobCoreComponent>(core, out var blobCoreComponent))
            return;

        if (_blobCoreSystem.CreateBlobObserver(core, args.Player.UserId, blobCoreComponent))
            QueueDel(uid);
    }
}
