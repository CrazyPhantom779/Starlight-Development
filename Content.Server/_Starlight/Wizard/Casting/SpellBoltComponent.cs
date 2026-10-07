using Content.Shared._Starlight.Wizard.SpellGraph;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Wizard.Casting;

/// <summary>A projectile carrying a woven spell. The spell takes effect where it hits or lands.</summary>
[RegisterComponent]
public sealed partial class SpellBoltComponent : Component, ISpellCarrier
{
    public SpellGraphPlan Plan { get; set; } = default!;

    public EntityUid Caster { get; set; }

    /// <summary>Where the caster aimed. The bolt takes effect here if nothing stops it first.</summary>
    public EntityCoordinates Aim;

    /// <summary>Distance to <see cref="Aim"/> at the last tick, used to spot a bolt flying past it.</summary>
    public float LastDistance = float.MaxValue;

    /// <summary>Whether the spell has already taken effect (a bolt only ever triggers once).</summary>
    public bool Spent;
}

/// <summary>A rune carrying a woven spell. It takes effect when something other than its caster steps on it.</summary>
[RegisterComponent]
public sealed partial class SpellRuneComponent : Component, ISpellCarrier
{
    public SpellGraphPlan Plan { get; set; } = default!;

    public EntityUid Caster { get; set; }

    public bool Spent;
}
