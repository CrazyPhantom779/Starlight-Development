namespace Content.Shared._Starlight.Wizard.SpellGraph;

/// <summary>
/// Lives on a woven spell's action entity. Server only: clients only need the action's name and cost.
/// </summary>
[RegisterComponent]
public sealed partial class SpellGraphActionComponent : Component
{
    [DataField]
    public SpellGraph Graph = new();

    /// <summary>Compiled form of <see cref="Graph"/>. Rebuilt on demand if missing.</summary>
    public SpellGraphPlan? Plan;
}
