using Content.Shared.Blob;
using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Server.Objectives.Conditions;

[UsedImplicitly]
[DataDefinition]
public sealed partial class BlobCaptureCondition
{
    private EntityUid? _mind;
    private int _target;

    public static BlobCaptureCondition GetAssigned(EntityUid mind) =>
        new()
        {
        _mind = mind,
        _target = 400
    };

    public string Title => Loc.GetString("objective-condition-blob-capture-title");

    public string Description =>
        Loc.GetString("objective-condition-blob-capture-description", ("count", _target));

    public SpriteSpecifier Icon =>
        new SpriteSpecifier.Rsi(new ResPath("Mobs/Aliens/blob.rsi"), "blob_nuke_overlay");

    public float Progress
    {
        get
        {
            var entMan = IoCManager.Resolve<IEntityManager>();

            if (_target == 0)
                return 1f;

            if (_mind == null)
                return 0f;

            if (!entMan.TryGetComponent<BlobObserverComponent>(_mind.Value, out var observer))
                return 0f;

            if (!entMan.TryGetComponent<BlobCoreComponent>(observer.Core, out var core))
                return 0f;

            return (float)core.BlobTiles.Count / _target;
        }
    }

    public float Difficulty => 4.0f;

    public bool Equals(BlobCaptureCondition? other)
    {
        if (other == null)
            return false;

        return _mind == other._mind && _target == other._target;
    }

    public override bool Equals(object? obj) =>
        obj is BlobCaptureCondition other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(_mind, _target);
}
