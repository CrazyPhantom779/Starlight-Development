namespace Content.Server._Starlight.Wizard.Casting;

/// <summary>A circle drawn by a wizard. Rites are performed by laying offerings inside it.</summary>
[RegisterComponent]
public sealed partial class RitualCircleComponent : Component
{
    [ViewVariables]
    public new EntityUid Owner;

    /// <summary>A rite is already under way.</summary>
    [ViewVariables]
    public bool Busy;
}
