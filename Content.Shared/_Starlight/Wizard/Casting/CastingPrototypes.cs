using Content.Shared._Starlight.Wizard.SpellGraph;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Wizard.Casting;

/// <summary>A prepared spell: a fixed glyph chain a wizard can cast straight from memory (Rote).</summary>
[Prototype]
public sealed partial class RoteSpellPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    [DataField(required: true)]
    public List<ProtoId<SpellGlyphPrototype>> Chain = [];

    /// <summary>Relative chance of being given to a wizard as part of their starting Primer.</summary>
    [DataField]
    public float Weight = 1f;
}

/// <summary>A tarot card. Drawing one gives a single-use spell, upright or reversed.</summary>
[Prototype]
public sealed partial class TarotCardPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Numeral = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Flavor;

    /// <summary>The card item that is handed over when this card is drawn.</summary>
    [DataField(required: true)]
    public EntProtoId Entity;

    [DataField(required: true)]
    public List<ProtoId<SpellGlyphPrototype>> Upright = [];

    [DataField(required: true)]
    public List<ProtoId<SpellGlyphPrototype>> Reversed = [];

    [DataField]
    public float Weight = 1f;
}
