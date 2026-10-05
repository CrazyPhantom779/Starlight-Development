using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Blob;

[RegisterComponent]
public sealed partial class BlobSpawnerComponent : Component
{
    [DataField("corePrototype", customTypeSerializer: typeof(ProtoId<EntityPrototype>))]
    public string CoreBlobPrototype = "CoreBlobTile";
}
