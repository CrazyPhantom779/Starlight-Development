using Content.Shared.FixedPoint;

namespace Content.Server.Blob;

[RegisterComponent]
public sealed partial class BlobResourceComponent : Component
{
    [DataField]
    public FixedPoint2 PointsPerPulsed = 3;
}
