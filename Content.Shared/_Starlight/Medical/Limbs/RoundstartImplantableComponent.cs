using Robust.Shared.GameStates;
namespace Content.Shared.Starlight;

[RegisterComponent, NetworkedComponent]
public sealed partial class RoundstartImplantableComponent : Component
{
    [DataField(required: true)]
    public int Cost;
}
