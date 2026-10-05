using System.Linq;
using System.Numerics;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Map.Components;

namespace Content.Server.Blob;

public sealed partial class BlobNodeSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;

    [SubscribeLocalEvent]
    private static void OnStartup(EntityUid uid, BlobNodeComponent component, ComponentStartup args)
    {
    }

    private void Pulse(EntityUid uid, BlobNodeComponent component)
    {
        var xform = Transform(uid);

        var radius = component.PulseRadius;
        var localPos = xform.Coordinates.Position;

        if (xform.GridUid == null)
            return;

        if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            return;

        if (!TryComp<BlobTileComponent>(uid, out var blobTileComponent) || blobTileComponent.Core == null)
            return;

        var innerTiles = _mapSystem.GetLocalTilesIntersecting(
            xform.GridUid.Value,
            grid,
            new Box2(
                localPos + new Vector2(-radius, -radius),
                localPos + new Vector2(radius, radius)),
            false).ToArray();

        _random.Shuffle(innerTiles);

        var explain = true;

        foreach (var tileRef in innerTiles)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tileRef.GridIndices))
            {
                if (!HasComp<BlobTileComponent>(ent))
                    continue;

                RaiseLocalEvent(ent, new BlobTileGetPulseEvent
                {
                    Explain = explain
                });

                explain = false;
            }
        }

        foreach (var lookupUid in _lookup.GetEntitiesInRange(xform.Coordinates, radius))
        {
            if (!HasComp<BlobMobComponent>(lookupUid))
                continue;

            RaiseLocalEvent(lookupUid, new BlobMobGetPulseEvent());
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var blobFactoryQuery = EntityQueryEnumerator<BlobNodeComponent>();
        while (blobFactoryQuery.MoveNext(out var ent, out var comp))
        {
            if (_gameTiming.CurTime < comp.NextPulse)
                continue;

            if (TryComp<BlobTileComponent>(ent, out var blobTileComponent) &&
                blobTileComponent.Core != null)
            {
                Pulse(ent, comp);
            }

            comp.NextPulse = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.PulseFrequency);
        }
    }
}
