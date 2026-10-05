using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Wizard.Fate;

/// <summary>
/// A trait rolled at Awakening. Usually pairs a boon with a bane. All numeric fields are multipliers
/// (1 = no change) or flat additions (0 = no change) applied to the wizard's Wind and Spellcraft.
/// </summary>
[Prototype]
public sealed partial class AspectPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    /// <summary>Relative chance of being rolled.</summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>Aspects that cannot be rolled together with this one.</summary>
    [DataField]
    public List<ProtoId<AspectPrototype>> Conflicts = [];

    [DataField]
    public float MaxWindMultiplier = 1f;

    [DataField]
    public float RegenMultiplier = 1f;

    [DataField]
    public float CostMultiplier = 1f;

    [DataField]
    public float GustChanceMultiplier = 1f;

    [DataField]
    public int ExtraNodes;

    [DataField]
    public int ExtraSpells;

    /// <summary>
    /// How strongly this aspect counts toward the wizard's overall Instability (power and risk together).
    /// Positive = stronger and wilder.
    /// </summary>
    [DataField]
    public float Instability;
}
