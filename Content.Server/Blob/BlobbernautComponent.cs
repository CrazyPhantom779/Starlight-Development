using Content.Shared.Blob;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server.Blob;

[RegisterComponent]
public sealed partial class BlobbernautComponent : SharedBlobbernautComponent
{
    [DataField]
    public float DamageFrequency = 5;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextDamage = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            { "Piercing", 25 },
        }
    };

    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsDead = false;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Factory = default!;
}
