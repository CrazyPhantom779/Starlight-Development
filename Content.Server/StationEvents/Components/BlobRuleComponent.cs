using Content.Server.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(BlobSpawnRule))]
public sealed partial class BlobSpawnRuleComponent : Component
{
    [DataField(required: true, customTypeSerializer: typeof(ProtoId<EntityPrototype>))]
    public List<string> CarrierBlobProtos =
    [
        "MobMouse",
        "MobMouse1",
        "MobMouse2"
    ];

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public int PlayersPerCarrierBlob = 30;

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public int MaxCarrierBlob = 3;
}
