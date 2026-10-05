using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Damage.Prototypes;

namespace Content.Shared.Blob;

[RegisterComponent]
public sealed partial class BlobCoreComponent : Component
{
    [DataField(customTypeSerializer: typeof(ProtoId<AntagPrototype>))]
    public string AntagBlobPrototypeId = "Blob";

    [DataField]
    public float AttackRate = 0.8f;

    [DataField]
    public float ReturnResourceOnRemove = 0.3f;

    [DataField]
    public bool CanSplit = true;

    [DataField]
    public SoundSpecifier AttackSound = new SoundPathSpecifier("/Audio/Animals/Blob/blobattack.ogg");

    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<BlobChemType, DamageSpecifier> ChemDamageDict { get; set; } = new()
    {
        {
            BlobChemType.BlazingOil, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Heat", 15 },
                    { "Structural", 150 },
                }
            }
        },
        {
            BlobChemType.ReactiveSpines, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Blunt", 8 },
                    { "Slash", 8 },
                    { "Piercing", 8 },
                    { "Structural", 150 },
                }
            }
        },
        {
            BlobChemType.ExplosiveLattice, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Heat", 5 },
                    { "Structural", 150 },
                }
            }
        },
        {
            BlobChemType.ElectromagneticWeb, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Structural", 150 },
                    { "Heat", 20 },
                },
            }
        },
        {
            BlobChemType.RegenerativeMateria, new DamageSpecifier()
            {
                DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                {
                    { "Structural", 150 },
                    { "Poison", 15 },
                }
            }
        },
    };

    [ViewVariables(VVAccess.ReadOnly)]
    public readonly Dictionary<BlobChemType, Color> ChemColors = new()
    {
        {BlobChemType.ReactiveSpines, Color.FromHex("#637b19")},
        {BlobChemType.BlazingOil, Color.FromHex("#937000")},
        {BlobChemType.RegenerativeMateria, Color.FromHex("#441e59")},
        {BlobChemType.ExplosiveLattice, Color.FromHex("#6e1900")},
        {BlobChemType.ElectromagneticWeb, Color.FromHex("#0d7777")},
    };

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public string BlobExplosive = "Blob";

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public BlobChemType DefaultChem = BlobChemType.ReactiveSpines;

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public BlobChemType CurrentChem = BlobChemType.ReactiveSpines;

    [DataField]
    public float FactoryRadiusLimit = 6f;

    [DataField]
    public float ResourceRadiusLimit = 3f;

    [DataField]
    public float NodeRadiusLimit = 4f;

    [DataField]
    public FixedPoint2 AttackCost = 2;

    [DataField]
    public FixedPoint2 FactoryBlobCost = 60;

    [DataField]
    public FixedPoint2 NormalBlobCost = 4;

    [DataField]
    public FixedPoint2 ResourceBlobCost = 40;

    [DataField]
    public FixedPoint2 NodeBlobCost = 50;

    [DataField]
    public FixedPoint2 BlobbernautCost = 60;

    [DataField]
    public FixedPoint2 StrongBlobCost = 15;

    [DataField]
    public FixedPoint2 ReflectiveBlobCost = 15;

    [DataField]
    public FixedPoint2 SplitCoreCost = 100;

    [DataField]
    public FixedPoint2 SwapCoreCost = 80;

    [DataField]
    public FixedPoint2 SwapChemCost = 40;

    [DataField]
    public string ReflectiveBlobTile = "ReflectiveBlobTile";

    [DataField]
    public string StrongBlobTile = "StrongBlobTile";

    [DataField]
    public string NormalBlobTile = "NormalBlobTile";

    [DataField]
    public string FactoryBlobTile = "FactoryBlobTile";

    [DataField]
    public string ResourceBlobTile = "ResourceBlobTile";

    [DataField]
    public string NodeBlobTile = "NodeBlobTile";

    [DataField]
    public string CoreBlobTile = "CoreBlobTileGhostRole";

    [DataField]
    public FixedPoint2 CoreBlobTotalHealth = 400;

    [DataField("ghostPrototype", customTypeSerializer: typeof(ProtoId<EntityPrototype>))]
    public string ObserverBlobPrototype = "MobObserverBlob";

    [DataField]
    public SoundSpecifier GreetSoundNotification = new SoundPathSpecifier("/Audio/Effects/clang.ogg");

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Observer = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public List<EntityUid> BlobTiles = [];

    public TimeSpan NextAction = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 Points = 0;
}

[Serializable, NetSerializable]
public enum BlobChemType : byte
{
    BlazingOil,
    ReactiveSpines,
    RegenerativeMateria,
    ExplosiveLattice,
    ElectromagneticWeb
}
