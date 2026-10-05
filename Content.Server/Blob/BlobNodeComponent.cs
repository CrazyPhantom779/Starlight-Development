namespace Content.Server.Blob;

[RegisterComponent]
public sealed partial class BlobNodeComponent : Component
{
    [DataField]
    public float PulseFrequency = 4;

    [DataField]
    public float PulseRadius = 3f;

    public TimeSpan NextPulse = TimeSpan.Zero;
}

public sealed class BlobTileGetPulseEvent : EntityEventArgs
{
    public bool Explain { get; set; }
}

public sealed class BlobMobGetPulseEvent : EntityEventArgs
{
}
