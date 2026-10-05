using Content.Shared.Blob;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;

namespace Content.Server.Blob;

public sealed partial class BlobResourceSystem : EntitySystem
{
    [Dependency] private BlobCoreSystem _blobCoreSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [SubscribeLocalEvent]
    private void OnPulsed(EntityUid uid, BlobResourceComponent component, BlobTileGetPulseEvent args)
    {
        if (!TryComp<BlobTileComponent>(uid, out var blobTileComponent) || blobTileComponent.Core == null)
            return;
        if (!TryComp<BlobCoreComponent>(blobTileComponent.Core, out var blobCoreComponent) ||
            blobCoreComponent.Observer == null)
            return;
        _popup.PopupEntity(Loc.GetString("blob-get-resource", ("point", component.PointsPerPulsed)),
            uid,
            blobCoreComponent.Observer.Value,
            PopupType.LargeGreen);

        var points = component.PointsPerPulsed;

        if (blobCoreComponent.CurrentChem == BlobChemType.RegenerativeMateria)
        {
            points += FixedPoint2.New(1);
        }

        _blobCoreSystem.ChangeBlobPoint(blobTileComponent.Core.Value, points);
    }
}
