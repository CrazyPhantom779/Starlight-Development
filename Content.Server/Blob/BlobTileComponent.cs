using Content.Shared.Blob;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server.Blob;

[RegisterComponent]
public sealed partial class BlobTileComponent : SharedBlobTileComponent
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Core = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool ReturnCost = true;

    [ViewVariables(VVAccess.ReadOnly), DataField("tileType")]
    public BlobTileType BlobTileType = BlobTileType.Normal;

    [ViewVariables(VVAccess.ReadOnly), DataField("healthOfPulse")]
    public DamageSpecifier HealthOfPulse = new()
    {
        DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            { "Blunt", -4 },
            { "Slash", -4 },
            { "Piercing", -4 },
            { "Heat", -4 },
            { "Cold", -4 },
            { "Shock", -4 },
        }
    };

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public DamageSpecifier FlashDamage = new()
    {
        DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            { "Heat", 100 },
        }
    };
}

[Serializable]
public enum BlobTileType : byte
{
    Normal,
    Strong,
    Reflective,
    Resource,
    Storage,
    Node,
    Factory,
    Core,
    None,
}
