using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Xenobiology.Potions;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimeNameChangePotionComponent : Component
{
    [DataField, AutoNetworkedField]
    public string AssignedName = string.Empty;
}
