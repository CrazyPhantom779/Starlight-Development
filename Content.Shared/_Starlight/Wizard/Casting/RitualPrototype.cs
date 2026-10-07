using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Wizard.Casting;

/// <summary>One thing that must lie on the circle for a rite to work.</summary>
[DataDefinition]
public sealed partial class RitualOffering
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public int Count = 1;

    /// <summary>What the offering is, in words ("a sheet of paper").</summary>
    [DataField(required: true)]
    public LocId Label;
}

/// <summary>A rite performed at a ritual circle by offering items. The result is what the rite does.</summary>
[Prototype]
public sealed partial class RitualPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public List<RitualOffering> Offerings = [];

    /// <summary>Wind the performer spends. Many rites cost none: the offerings pay.</summary>
    [DataField]
    public float WindCost;

    /// <summary>How long the rite takes. The performer must stay near the circle.</summary>
    [DataField]
    public float Seconds = 6f;

    [DataField(required: true)]
    public RitualResult Result = default!;
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class RitualResult;

/// <summary>Spawns items on the circle.</summary>
public sealed partial class SpawnRitualResult : RitualResult
{
    [DataField(required: true)]
    public List<EntProtoId> Prototypes = [];

    [DataField]
    public int Amount = 1;
}

/// <summary>Teaches the performer random glyphs they do not know yet.</summary>
public sealed partial class LearnGlyphRitualResult : RitualResult
{
    [DataField]
    public int Count = 1;

    /// <summary>Only glyphs of this school, if set.</summary>
    [DataField]
    public string? School;
}

/// <summary>Teaches the performer a Discipline they do not have yet.</summary>
public sealed partial class LearnDisciplineRitualResult : RitualResult;

/// <summary>Attunes the performer to a school they are not attuned to yet.</summary>
public sealed partial class AttuneRitualResult : RitualResult;

/// <summary>Teaches the performer random Rotes they do not know yet.</summary>
public sealed partial class LearnRoteRitualResult : RitualResult
{
    [DataField]
    public int Count = 1;
}

/// <summary>Casts a spell centred on the circle. The offerings pay for it, not the performer's Wind.</summary>
public sealed partial class CastRitualResult : RitualResult
{
    [DataField(required: true)]
    public List<ProtoId<SpellGlyphPrototype>> Chain = [];
}

public sealed partial class RestoreWindRitualResult : RitualResult
{
    [DataField]
    public float Amount = 60f;
}

/// <summary>Draws free tarot cards onto the circle.</summary>
public sealed partial class TarotRitualResult : RitualResult
{
    [DataField]
    public int Count = 3;
}
