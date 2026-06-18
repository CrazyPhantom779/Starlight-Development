using Robust.Shared.Audio;

namespace Content.Server._Starlight.FactionClothingBlockerSystem;

[RegisterComponent]
public sealed partial class AccessClothingBlockerComponent : Component
{
    [DataField(required: false)]
    public string? Access = null;

    [DataField]
    public SoundSpecifier BeepSound = new SoundPathSpecifier("/Audio/Effects/beep1.ogg");
}
