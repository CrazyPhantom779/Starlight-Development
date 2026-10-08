using Content.Shared._Starlight.Wizard.SpellGraph;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Wizard.Casting;

/// <summary>Where and by whom a spell is being cast. Passed to every effect.</summary>
public sealed class SpellCastContext
{
    public EntityUid Caster;

    /// <summary>The point the effect takes place at.</summary>
    public EntityCoordinates Point;

    /// <summary>The creature or item at the point, if the Form targeted one.</summary>
    public EntityUid? Target;

    /// <summary>Extra reach added by the Form (a Burst's radius).</summary>
    public float FormRadius;

    /// <summary>The action entity this cast came from, when there is one (needed for wrapped legacy events).</summary>
    public EntityUid? Action;

    /// <summary>True when the point is where a bolt or rune triggered (it may be inside a wall).</summary>
    public bool FromProjectile;

    /// <summary>Random strength variation for this step. 1 is exactly as woven.</summary>
    public float Jitter = 1f;

    /// <summary>How wide the variation is, from the caster's Fate. Set once per cast.</summary>
    public float Variance;

    /// <summary>When set, creatures already in <see cref="Hit"/> are skipped, so a line never hits anyone twice.</summary>
    public bool DedupeHits;

    /// <summary>Creatures already affected by this cast, so Chain never hits the same one twice.</summary>
    public HashSet<EntityUid> Hit = [];
}

/// <summary>A cast waiting to detonate: a bolt in flight or a rune on the floor.</summary>
public interface ISpellCarrier
{
    SpellGraphPlan Plan { get; }
    EntityUid Caster { get; }
}
