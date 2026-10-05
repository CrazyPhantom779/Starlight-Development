using Robust.Shared.Audio;

namespace Content.Server.Blob.NPC.BlobPod;

[RegisterComponent]
public sealed partial class BlobPodComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsZombifying = false;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ZombifiedEntityUid = default!;

    [DataField]
    public float ZombifyDelay = 5.00f;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Core = null;

    [DataField]
    public SoundSpecifier ZombifySoundPath = new SoundPathSpecifier("/Audio/Effects/Fluids/blood1.ogg");

    [DataField]
    public SoundSpecifier ZombifyFinishSoundPath = new SoundPathSpecifier("/Audio/Effects/gib1.ogg");

    public EntityUid? ZombifyStingStream;
    public EntityUid? ZombifyTarget;
}
