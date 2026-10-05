using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Blob;

[RegisterComponent, NetworkedComponent]
public sealed partial class BlobCarrierComponent : Component
{
    [DataField]
    public float TransformationDelay = 240;

    [DataField]
    public float AlertInterval = 30f;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextAlert = TimeSpan.FromSeconds(0);

    [ViewVariables(VVAccess.ReadWrite)]
    public bool HasMind = false;

    [ViewVariables(VVAccess.ReadWrite)]
    public float TransformationTimer = 0;

    [DataField("corePrototype", customTypeSerializer: typeof(ProtoId<EntityPrototype>))]
    public string CoreBlobPrototype = "CoreBlobTile";

    [DataField(customTypeSerializer: typeof(ProtoId<EntityPrototype>))]
    public string CoreBlobGhostRolePrototype = "CoreBlobTileGhostRole";
}

public sealed partial class TransformToBlobActionEvent : InstantActionEvent
{
}
