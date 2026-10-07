using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Wizard.Casting;

/// <summary>
/// Something a spell does at a point in the world. Effect glyphs hold a list of these.
/// Every subclass is plain data; the server's GlyphEffectSystem knows how to carry each one out.
/// Unlike wrapped legacy spell events, these work with every Form (aimed, self, bolt, touch, burst, rune).
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class GlyphEffect;

/// <summary>Hurts creatures around the point.</summary>
public sealed partial class DamageGlyphEffect : GlyphEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public float Radius = 1.2f;

    [DataField]
    public bool AffectCaster;

    [DataField]
    public bool IgnoreResistances;
}

/// <summary>Heals creatures around the point (damage values should be negative).</summary>
public sealed partial class HealGlyphEffect : GlyphEffect
{
    [DataField(required: true)]
    public DamageSpecifier Heal = new();

    [DataField]
    public float Radius = 1.2f;

    [DataField]
    public bool AffectCaster = true;
}

public sealed partial class IgniteGlyphEffect : GlyphEffect
{
    [DataField]
    public float FireStacks = 2f;

    [DataField]
    public float Radius = 1.2f;

    [DataField]
    public bool AffectCaster;
}

public sealed partial class StunGlyphEffect : GlyphEffect
{
    [DataField]
    public float Seconds = 2f;

    /// <summary>Knock them down instead of fully paralysing them.</summary>
    [DataField]
    public bool KnockdownOnly = true;

    [DataField]
    public float Radius = 1.2f;

    [DataField]
    public bool AffectCaster;
}

/// <summary>Throws creatures and items away from (or toward) the point.</summary>
public sealed partial class PushGlyphEffect : GlyphEffect
{
    [DataField]
    public float Strength = 6f;

    [DataField]
    public float Radius = 3f;

    [DataField]
    public bool Pull;

    [DataField]
    public bool AffectCaster;
}

/// <summary>Spawns entities at (or scattered around) the point. Smoke, walls, runes, summons, light orbs.</summary>
public sealed partial class SpawnGlyphEffect : GlyphEffect
{
    [DataField(required: true)]
    public List<EntProtoId> Prototypes = [];

    [DataField]
    public int Amount = 1;

    /// <summary>Random offset in tiles around the point.</summary>
    [DataField]
    public float Scatter;

    /// <summary>Despawn after this many seconds, if set.</summary>
    [DataField]
    public float? Lifetime;

    [DataField]
    public bool SnapToGrid = true;

    [DataField]
    public bool PreventCollideWithCaster;

    /// <summary>Put the spawned entity in the caster's hand instead of at the point.</summary>
    [DataField]
    public bool InHand;
}

/// <summary>The caster steps to the point.</summary>
public sealed partial class TeleportGlyphEffect : GlyphEffect
{
    [DataField]
    public float MaxDistance = 12f;
}

/// <summary>The caster trades places with the nearest creature to the point.</summary>
public sealed partial class SwapGlyphEffect : GlyphEffect
{
    [DataField]
    public float Radius = 1.5f;
}

/// <summary>Damages creatures around the point and heals the caster by what was dealt.</summary>
public sealed partial class DrainGlyphEffect : GlyphEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public float Radius = 1.2f;

    /// <summary>Fraction of the damage dealt that is healed back.</summary>
    [DataField]
    public float HealFraction = 0.5f;
}

/// <summary>Moves Wind. Positive gives Wind to creatures around the point, negative drains it (to the caster).</summary>
public sealed partial class WindGlyphEffect : GlyphEffect
{
    [DataField]
    public float Amount = 15f;

    [DataField]
    public float Radius = 1.2f;

    /// <summary>When draining, the caster receives what was taken.</summary>
    [DataField]
    public bool ToCaster = true;
}

/// <summary>A small explosion at the point.</summary>
public sealed partial class ExplodeGlyphEffect : GlyphEffect
{
    [DataField]
    public string ExplosionType = "Default";

    [DataField]
    public float TotalIntensity = 40f;

    [DataField]
    public float Slope = 5f;

    [DataField]
    public float MaxIntensity = 10f;
}
