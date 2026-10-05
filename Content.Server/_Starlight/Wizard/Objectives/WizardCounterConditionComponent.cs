namespace Content.Server._Starlight.Wizard.Objectives;

public enum WizardCounterKind : byte
{
    /// <summary>Any wizard spell cast.</summary>
    Cast,

    /// <summary>Only spells woven in the Spellweaving window.</summary>
    CastWoven,

    /// <summary>Counts each different spell once.</summary>
    CastDistinct,

    /// <summary>Times the wizard overcast (pushed Wind below zero).</summary>
    Overcast,
}

/// <summary>
/// Objective condition that counts wizard activity toward the objective's NumberObjective target.
/// </summary>
[RegisterComponent, Access(typeof(WizardCounterConditionSystem))]
public sealed partial class WizardCounterConditionComponent : Component
{
    [DataField(required: true)]
    public WizardCounterKind Kind;

    [ViewVariables]
    public int Count;

    /// <summary>Keys already counted, for <see cref="WizardCounterKind.CastDistinct"/>.</summary>
    [ViewVariables]
    public HashSet<string> Seen = [];
}
