using Content.Shared.Actions;

namespace Content.Shared._Starlight.Wizard.Fold;

/// <summary>
/// Folds space so the caster trades places with the nearest creature to the clicked point.
/// </summary>
public sealed partial class FoldSwapSpellEvent : WorldTargetActionEvent
{
    /// <summary>How far from the clicked point to look for a creature.</summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>The caster may only fold with something this close to them.</summary>
    [DataField]
    public float MaxCasterDistance = 20f;
}
