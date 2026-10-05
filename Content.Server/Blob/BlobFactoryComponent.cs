namespace Content.Server.Blob;

[RegisterComponent]
public sealed partial class BlobFactoryComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public float SpawnedCount = 0;

    [DataField("spawnLimit")]
    public float SpawnLimit = 3;

    [DataField]
    public float SpawnRate = 10;

    [DataField("blobSporeId")]
    public string Pod = "MobBlobPod";

    [DataField]
    public string BlobbernautId = "MobBlobBlobbernaut";

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Blobbernaut = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public List<EntityUid> BlobPods = [];

    public TimeSpan NextSpawn = TimeSpan.Zero;
}

public sealed class ProduceBlobbernautEvent : EntityEventArgs
{
}
