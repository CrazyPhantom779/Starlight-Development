namespace Content.Shared._Starlight.Combat.Ranged.Pierce;

[RegisterComponent]
public sealed partial class RicochetableComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public float Chance = 1f;
}
