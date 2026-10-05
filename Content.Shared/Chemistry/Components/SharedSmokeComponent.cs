using Robust.Shared.GameStates;

namespace Content.Shared.Chemistry.Components;

[NetworkedComponent]
public abstract partial class SharedSmokeComponent : Component
{
    [DataField]
    public Color Color = Color.White;
}
