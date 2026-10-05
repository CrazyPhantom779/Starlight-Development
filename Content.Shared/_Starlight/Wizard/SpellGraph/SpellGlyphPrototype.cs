using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Wizard.SpellGraph;

public enum GlyphCategory : byte
{
    /// <summary>How the spell reaches the world. Exactly one per spell.</summary>
    Form,

    /// <summary>What the spell does.</summary>
    Effect,

    /// <summary>Modifies a Form (all effects) or one Effect.</summary>
    Augment,
}

public enum SpellTargetMode : byte
{
    /// <summary>The caster clicks a point in the world.</summary>
    World,

    /// <summary>The spell happens on/around the caster, no aiming.</summary>
    Self,
}

public enum AugmentKind : byte
{
    /// <summary>Run the effect <c>Value</c> additional times, spaced out.</summary>
    Repeat,

    /// <summary>Run the effect <c>Value</c> seconds later.</summary>
    Delay,

    /// <summary>Multiplies the whole spell's cost by <c>Value</c> (use below 1).</summary>
    Cheaper,
}

/// <summary>
/// A single building block of a spell. Effect glyphs wrap existing spell action events, so any spell the game
/// already supports can become a glyph by copying its event into a prototype.
/// </summary>
[Prototype]
public sealed partial class SpellGlyphPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public GlyphCategory Category;

    /// <summary>Localisation id of the glyph's name.</summary>
    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    /// <summary>Base Wind cost this glyph adds.</summary>
    [DataField]
    public float Cost = 5f;

    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>Schools of magic this glyph belongs to. Used for affinities.</summary>
    [DataField]
    public List<string> Schools = [];

    // Form
    [DataField]
    public SpellTargetMode TargetMode = SpellTargetMode.World;

    // Effect: which events this effect supports. A glyph may support one or both target modes.
    [DataField]
    public WorldTargetActionEvent? WorldEvent;

    [DataField]
    public InstantActionEvent? InstantEvent;

    // Augment
    [DataField]
    public AugmentKind Augment;

    [DataField]
    public float Value = 1f;
}
