using Robust.Shared.GameStates;

namespace Content.Shared.Blob;

[NetworkedComponent]
public abstract partial class SharedBlobbernautComponent : Component
{
    [DataField]
    public Color Color = Color.White;
}
