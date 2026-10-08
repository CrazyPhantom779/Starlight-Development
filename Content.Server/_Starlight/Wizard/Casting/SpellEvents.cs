using Content.Shared._Starlight.Wizard.SpellGraph;

namespace Content.Server._Starlight.Wizard.Casting;

/// <summary>Raised on a caster once a spell has taken effect (or a bolt or rune has gone off for them).</summary>
/// <param name="Caster">Who cast it.</param>
/// <param name="Plan">The spell.</param>
/// <param name="Context">Where it happened and who it hit so far.</param>
[ByRefEvent]
public readonly record struct SpellCastEvent(EntityUid Caster, SpellGraphPlan Plan, SpellCastContext Context);

/// <summary>Raised on a wizard after they finish a rite.</summary>
[ByRefEvent]
public readonly record struct RitePerformedEvent(EntityUid Wizard);

/// <summary>Raised on a wizard whenever they put a spell into an object (wand, enchantment, scroll).</summary>
[ByRefEvent]
public readonly record struct SpellStoredEvent(EntityUid Wizard);

/// <summary>Raised on a caster whenever their spell has taken effect somewhere and hit these creatures so far.</summary>
[ByRefEvent]
public readonly record struct SpellHitEvent(EntityUid Caster, IReadOnlySet<EntityUid> Hit);

/// <summary>Raised on a user when a scroll or tarot card is used up.</summary>
[ByRefEvent]
public readonly record struct SpellItemUsedEvent(EntityUid User, bool IsCard);

/// <summary>Raised on a wizard when they finish an errand.</summary>
[ByRefEvent]
public readonly record struct ErrandCompletedEvent(EntityUid Wizard);
