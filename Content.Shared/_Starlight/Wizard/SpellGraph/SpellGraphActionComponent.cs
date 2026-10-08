using Content.Shared._Starlight.Wizard.Casting;

namespace Content.Shared._Starlight.Wizard.SpellGraph;

/// <summary>
/// Lives on a woven spell's action entity. Server only: clients only need the action's name and cost.
/// </summary>
[RegisterComponent]
public sealed partial class SpellGraphActionComponent : Component
{
    [DataField]
    public SpellGraph Graph = new();

    /// <summary>How this spell was made. Used to reapply that discipline's cost bonus when tides shift.</summary>
    [DataField]
    public SpellDiscipline Discipline = SpellDiscipline.Glyphwork;

    /// <summary>Compiled form of <see cref="Graph"/>. Rebuilt on demand if missing.</summary>
    public SpellGraphPlan? Plan;
}
