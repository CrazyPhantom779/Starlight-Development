namespace Content.Server._Starlight.Wizard.Objectives;

public enum WizardCounterKind : byte
{
    /// <summary>Any spell cast.</summary>
    Cast,

    /// <summary>Only spells woven in the Spellweaving window.</summary>
    CastWoven,

    /// <summary>Counts each different spell once.</summary>
    CastDistinct,

    /// <summary>Times the wizard overcast (pushed Wind below zero).</summary>
    Overcast,

    /// <summary>Rites performed at a ritual circle.</summary>
    Rites,

    /// <summary>Tarot cards played.</summary>
    Cards,

    /// <summary>Scrolls read.</summary>
    Scrolls,

    /// <summary>Spells put into wands, objects or scrolls.</summary>
    Stored,

    /// <summary>Different creatures affected by spells.</summary>
    Creatures,

    /// <summary>Different schools of magic used.</summary>
    Schools,

    /// <summary>Errands finished.</summary>
    Errands,

    /// <summary>Reach this much maximum Wind. Not counted: read from the wizard.</summary>
    MaxWind,
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

    /// <summary>Keys already counted, for the kinds that count each thing once.</summary>
    [ViewVariables]
    public HashSet<string> Seen = [];
}
