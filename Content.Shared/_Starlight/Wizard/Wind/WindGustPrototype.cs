using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Wizard.Wind;

/// <summary>
/// A short, random misfortune that can happen when a caster overcasts (pushes Wind below zero).
/// </summary>
[Prototype]
public sealed partial class WindGustPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Relative chance of being picked.</summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>Popup shown to the caster.</summary>
    [DataField]
    public LocId? Message;

    /// <summary>Entities spawned on the caster.</summary>
    [DataField]
    public List<EntProtoId> SpawnAtCaster = [];

    /// <summary>Swap places with a random nearby creature.</summary>
    [DataField]
    public bool SwapWithNearby;

    [DataField]
    public float SwapRange = 7f;

    [DataField]
    public SoundSpecifier? Sound;
}
