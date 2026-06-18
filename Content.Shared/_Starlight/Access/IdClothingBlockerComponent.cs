using Content.Shared.Access;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Access;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IdClothingBlockerComponent : Component
{
    [DataField] [AutoNetworkedField]
    public bool IsBlocked = false;

    [DataField]
    public List<ProtoId<AccessLevelPrototype>>? AllowedAccesses = [];

    [DataField]
    public SoundSpecifier BeepSound = new SoundPathSpecifier("/Audio/Effects/beep1.ogg");

    [DataField]
    public bool FreezeUser = true;
}
