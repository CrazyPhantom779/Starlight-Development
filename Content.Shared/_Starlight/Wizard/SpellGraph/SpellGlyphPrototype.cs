using Content.Shared._Starlight.Wizard.Casting;
using Content.Shared.Actions;
using Content.Shared.Whitelist;
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

/// <summary>How a spell is delivered to the world.</summary>
public enum SpellDelivery : byte
{
    /// <summary>Instantly at the clicked point.</summary>
    Aimed,

    /// <summary>Centred on the caster. No aiming.</summary>
    Self,

    /// <summary>A visible projectile flies to the point and triggers where it lands or hits.</summary>
    Bolt,

    /// <summary>On the creature or item clicked, within arm's reach.</summary>
    Touch,

    /// <summary>A burst around the caster. No aiming.</summary>
    Burst,

    /// <summary>A rune placed at the clicked point that triggers when something steps on it.</summary>
    Rune,
}

public enum AugmentKind : byte
{
    /// <summary>Run the effect <c>Value</c> additional times, quickly.</summary>
    Repeat,

    /// <summary>Run the effect <c>Value</c> seconds later.</summary>
    Delay,

    /// <summary>Multiplies the whole spell's cost by <c>Value</c> (use below 1).</summary>
    Cheaper,

    /// <summary>Makes the effect stronger, at a higher cost.</summary>
    Amplify,

    /// <summary>Widens the effect's area by <c>Value</c> tiles.</summary>
    Widen,

    /// <summary>Bolt forms fire <c>Value</c> extra bolts in a fan.</summary>
    Split,

    /// <summary>After it takes effect, the effect jumps to <c>Value</c> more nearby creatures.</summary>
    Chain,

    /// <summary>Repeats <c>Value</c> times, once a second, at the same place.</summary>
    Linger,

    /// <summary>Reduces the spell's cooldown by <c>Value</c> (a fraction).</summary>
    Quicken,
}

/// <summary>
/// A single building block of a spell. Effect glyphs either carry universal <see cref="Effects"/> that work with
/// every Form, or wrap legacy spell action events for aimed / self casting.
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

    /// <summary>The path drawn on the 3x3 sigil grid, as dot indices 0-8 joined by dashes (e.g. "0-4-8").</summary>
    [DataField]
    public string? Sigil;

    /// <summary>Relative chance of being picked when a wizard is granted a random glyph.</summary>
    [DataField]
    public float Weight = 1f;

    // ---- Form ----
    [DataField]
    public SpellDelivery Delivery = SpellDelivery.Aimed;

    /// <summary>Bolt forms: the projectile that is fired.</summary>
    [DataField]
    public EntProtoId? Projectile;

    [DataField]
    public float ProjectileSpeed = 18f;

    /// <summary>Burst forms: how far the burst reaches (added to each effect's own radius).</summary>
    [DataField]
    public float Radius = 2.5f;

    /// <summary>Touch forms: how far away the target may be, in tiles.</summary>
    [DataField]
    public float Range = 2.5f;

    /// <summary>Rune forms: the rune entity placed.</summary>
    [DataField]
    public EntProtoId? Rune;

    public SpellTargetMode TargetMode =>
        Delivery is SpellDelivery.Self or SpellDelivery.Burst ? SpellTargetMode.Self : SpellTargetMode.World;

    // ---- Effect ----
    /// <summary>Universal effects. If any are present this glyph works with every Form.</summary>
    [DataField]
    public List<GlyphEffect> Effects = [];

    /// <summary>Legacy: wraps an existing aimed spell event. Works only with the Aimed form.</summary>
    [DataField]
    public WorldTargetActionEvent? WorldEvent;

    /// <summary>Legacy: wraps an existing self-centred spell event. Works with Self and Burst forms.</summary>
    [DataField]
    public InstantActionEvent? InstantEvent;

    /// <summary>An item the caster must hold in a hand when casting. It is consumed.</summary>
    [DataField]
    public EntityWhitelist? Reagent;

    [DataField]
    public LocId? ReagentName;

    // ---- Augment ----
    [DataField]
    public AugmentKind Augment;

    [DataField]
    public float Value = 1f;

    /// <summary>Whether this effect glyph can be used with the given delivery.</summary>
    public bool SupportsDelivery(SpellDelivery delivery)
    {
        if (Effects.Count > 0)
            return true;

        return delivery switch
        {
            SpellDelivery.Aimed => WorldEvent != null,
            SpellDelivery.Self or SpellDelivery.Burst => InstantEvent != null,
            _ => false,
        };
    }
}
