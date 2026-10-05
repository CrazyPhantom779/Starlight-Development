using System.Linq;
using System.Numerics;
using Content.Server.Construction.Components;
using Content.Server.Destructible;
using Content.Server.Emp;
using Content.Shared.Blob;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Server.Audio;
using Content.Shared.Damage.Systems;
using Content.Shared.Flash;
using Robust.Shared.Map.Components;

namespace Content.Server.Blob;

public sealed partial class BlobTileSystem : SharedBlobTileSystem
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private BlobCoreSystem _blobCoreSystem = default!;
    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EmpSystem _empSystem = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;

    [SubscribeLocalEvent]
    private void OnFlashAttempt(EntityUid uid, BlobTileComponent component, FlashAttemptEvent args)
    {
        if (args.Used == null || MetaData(args.Used.Value).EntityPrototype?.ID != "GrenadeFlashBang")
            return;

        if (component.BlobTileType == BlobTileType.Normal)
            _damageableSystem.TryChangeDamage(uid, component.FlashDamage);
    }

    [SubscribeLocalEvent]
    private void OnDestruction(EntityUid uid, BlobTileComponent component, DestructionEventArgs args)
    {
        if (component.Core == null || !TryComp(component.Core.Value, out BlobCoreComponent? blobCoreComponent))
            return;

        if (!TryComp(uid, out TransformComponent? xform))
            return;

        if (blobCoreComponent.CurrentChem == BlobChemType.ElectromagneticWeb)
        {
            _empSystem.EmpPulse(xform.MapPosition, 3f, 50f, TimeSpan.FromSeconds(3f));
        }
    }

    [SubscribeLocalEvent]
    private void AddRemoveVerb(EntityUid uid, BlobTileComponent component, GetVerbsEvent<Verb> args)
    {
        if (!TryComp(args.User, out BlobObserverComponent? ghostBlobComponent))
            return;

        if (ghostBlobComponent.Core == null ||
            !TryComp(ghostBlobComponent.Core.Value, out BlobCoreComponent? blobCoreComponent))
            return;

        if (ghostBlobComponent.Core.Value != component.Core)
            return;

        if (TryComp(uid, out TransformComponent? transformComponent) && !transformComponent.Anchored)
            return;

        if (HasComp<BlobCoreComponent>(uid))
            return;

        args.Verbs.Add(new Verb
        {
            Act = () => TryRemove(uid, ghostBlobComponent.Core.Value, component, blobCoreComponent),
            Text = Loc.GetString("blob-verb-remove-blob-tile"),
        });
    }

    private void TryRemove(EntityUid target, EntityUid coreUid, BlobTileComponent tile, BlobCoreComponent core)
    {
        var xform = Transform(target);

        _entMan.DeleteEntity(target);

        FixedPoint2 returnCost = 0;

        if (tile.ReturnCost)
        {
            returnCost = tile.BlobTileType switch
            {
                BlobTileType.Normal => core.NormalBlobCost * core.ReturnResourceOnRemove,
                BlobTileType.Strong => core.StrongBlobCost * core.ReturnResourceOnRemove,
                BlobTileType.Factory => core.FactoryBlobCost * core.ReturnResourceOnRemove,
                BlobTileType.Resource => core.ResourceBlobCost * core.ReturnResourceOnRemove,
                BlobTileType.Reflective => core.ReflectiveBlobCost * core.ReturnResourceOnRemove,
                BlobTileType.Node => core.NodeBlobCost * core.ReturnResourceOnRemove,
                _ => 0
            };
        }

        if (returnCost > 0)
        {
            if (TryComp(tile.Core, out BlobCoreComponent? blobCoreComponent) &&
                blobCoreComponent.Observer != null)
            {
                _popup.PopupCoordinates(
                    Loc.GetString("blob-get-resource", ("point", returnCost)),
                    xform.Coordinates,
                    blobCoreComponent.Observer.Value,
                    PopupType.LargeGreen);
            }

            _blobCoreSystem.ChangeBlobPoint(coreUid, returnCost, core);
        }
    }

    [SubscribeLocalEvent]
    private void OnGetState(EntityUid uid, BlobTileComponent component, ref ComponentGetState args)
    => args.State = new BlobTileComponentState
    {
        Color = component.Color
    };

    [SubscribeLocalEvent]
    private void OnPulsed(EntityUid uid, BlobTileComponent component, BlobTileGetPulseEvent args)
    {
        if (!TryComp(uid, out BlobTileComponent? blobTileComponent) || blobTileComponent.Core == null ||
            !TryComp(blobTileComponent.Core.Value, out BlobCoreComponent? blobCoreComponent))
            return;

        if (blobCoreComponent.CurrentChem == BlobChemType.RegenerativeMateria)
        {
            var healCore = new DamageSpecifier();

            foreach (var kv in component.HealthOfPulse.DamageDict)
            {
                healCore.DamageDict.Add(kv.Key, kv.Value * 10);
            }

            _damageableSystem.TryChangeDamage(uid, healCore);
        }
        else
        {
            _damageableSystem.TryChangeDamage(uid, component.HealthOfPulse);
        }

        if (!args.Explain)
            return;

        if (!TryComp(uid, out TransformComponent? xform))
            return;

        if (!TryComp(xform.GridUid, out MapGridComponent? grid))
            return;

        // 'MapGridComponent' does not contain a definition for 'GetTileRef' and no accessible extension method 'GetTileRef' accepting a first argument of type 'MapGridComponent' could be found (are you missing a using directive or an assembly reference?)
        var mobTile = _mapSystem.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);

        var mobAdjacentTiles = new[]
        {
            mobTile.GridIndices.Offset(Direction.East),
            mobTile.GridIndices.Offset(Direction.West),
            mobTile.GridIndices.Offset(Direction.North),
            mobTile.GridIndices.Offset(Direction.South)
        };

        var localPos = xform.Coordinates.Position;
        var radius = 1.0f;

        var innerTiles = _mapSystem.GetLocalTilesIntersecting(
            xform.GridUid.Value,
            grid,
            new Box2(localPos + new Vector2(-radius, -radius),
                     localPos + new Vector2(radius, radius)), false)
            .ToArray();

        foreach (var innerTile in innerTiles)
        {
            if (!mobAdjacentTiles.Contains(innerTile.GridIndices))
                continue;

            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, innerTile.GridIndices))
            {
                if (!HasComp<DestructibleComponent>(ent) || !HasComp<ConstructionComponent>(ent))
                    continue;

                _damageableSystem.TryChangeDamage(ent, blobCoreComponent.ChemDamageDict[blobCoreComponent.CurrentChem]);
                _audioSystem.PlayPvs(blobCoreComponent.AttackSound, uid, AudioParams.Default);

                args.Explain = true;
                return;
            }

            var spawn = true;

            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, innerTile.GridIndices))
            {
                if (!HasComp<BlobTileComponent>(ent))
                    continue;

                spawn = false;
                break;
            }

            if (!spawn)
                continue;

            var coordinates = new EntityCoordinates(xform.GridUid.Value, innerTile.GridIndices);

            var cost = FixedPoint2.Zero;

            if (_blobCoreSystem.TransformBlobTile(
                    null,
                    blobTileComponent.Core.Value,
                    blobCoreComponent.NormalBlobTile,
                    coordinates,
                    blobCoreComponent,
                    cost))
            {
                return;
            }
        }
    }

    [SubscribeLocalEvent]
    private void AddUpgradeVerb(EntityUid uid, BlobTileComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp(args.User, out BlobObserverComponent? ghostBlobComponent))
            return;

        if (ghostBlobComponent.Core == null ||
            !TryComp(ghostBlobComponent.Core.Value, out BlobCoreComponent? blobCoreComponent))
            return;

        if (TryComp(uid, out TransformComponent? transformComponent) && !transformComponent.Anchored)
            return;

        var verbName = component.BlobTileType switch
        {
            BlobTileType.Normal => Loc.GetString("blob-verb-upgrade-to-strong"),
            BlobTileType.Strong => Loc.GetString("blob-verb-upgrade-to-reflective"),
            _ => "Upgrade"
        };

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TryUpgrade(uid, args.User, ghostBlobComponent.Core.Value, component, blobCoreComponent),
            Text = verbName
        });
    }

    private void TryUpgrade(EntityUid target, EntityUid user, EntityUid coreUid, BlobTileComponent tile, BlobCoreComponent core)
    {
        var xform = Transform(target);

        if (tile.BlobTileType == BlobTileType.Normal)
        {
            if (!_blobCoreSystem.TryUseAbility(user, coreUid, core, core.StrongBlobCost))
                return;

            _blobCoreSystem.TransformBlobTile(target, coreUid, core.StrongBlobTile, xform.Coordinates, core, transformCost: core.StrongBlobCost);
        }
        else if (tile.BlobTileType == BlobTileType.Strong)
        {
            if (!_blobCoreSystem.TryUseAbility(user, coreUid, core, core.ReflectiveBlobCost))
                return;

            _blobCoreSystem.TransformBlobTile(target, coreUid, core.ReflectiveBlobTile, xform.Coordinates, core, transformCost: core.ReflectiveBlobCost);
        }
    }

    /* This work very bad.
     I replace invisible
     wall to teleportation observer
     if he moving away from blob tile */

    // private void OnStartup(EntityUid uid, BlobCellComponent component, ComponentStartup args)
    // {
    //     var xform = Transform(uid);
    //     var radius = 2.5f;
    //     var wallSpacing = 1.5f; // Расстояние между стенами и центральной областью
    //
    //     if (!_map.TryGetGrid(xform.GridUid, out var grid))
    //     {
    //         return;
    //     }
    //
    //     var localpos = xform.Coordinates.Position;
    //
    //     // Получаем тайлы в области с радиусом 2.5
    //     var allTiles = grid.GetLocalTilesIntersecting(
    //         new Box2(localpos + new Vector2(-radius, -radius), localpos + new Vector2(radius, radius))).ToArray();
    //
    //     // Получаем тайлы в области с радиусом 1.5
    //     var innerTiles = grid.GetLocalTilesIntersecting(
    //         new Box2(localpos + new Vector2(-wallSpacing, -wallSpacing), localpos + new Vector2(wallSpacing, wallSpacing))).ToArray();
    //
    //     foreach (var tileref in innerTiles)
    //     {
    //         foreach (var ent in grid.GetAnchoredEntities(tileref.GridIndices))
    //         {
    //             if (HasComp<BlobBorderComponent>(ent))
    //                 QueueDel(ent);
    //             if (HasComp<BlobCellComponent>(ent))
    //             {
    //                 var blockTiles = grid.GetLocalTilesIntersecting(
    //                     new Box2(Transform(ent).Coordinates.Position + new Vector2(-wallSpacing, -wallSpacing),
    //                         Transform(ent).Coordinates.Position + new Vector2(wallSpacing, wallSpacing))).ToArray();
    //                 allTiles = allTiles.Except(blockTiles).ToArray();
    //             }
    //         }
    //     }
    //
    //     var outerTiles = allTiles.Except(innerTiles).ToArray();
    //
    //     foreach (var tileRef in outerTiles)
    //     {
    //         foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
    //         {
    //             if (HasComp<BlobCellComponent>(ent))
    //             {
    //                 var blockTiles = grid.GetLocalTilesIntersecting(
    //                     new Box2(Transform(ent).Coordinates.Position + new Vector2(-wallSpacing, -wallSpacing),
    //                         Transform(ent).Coordinates.Position + new Vector2(wallSpacing, wallSpacing))).ToArray();
    //                 outerTiles = outerTiles.Except(blockTiles).ToArray();
    //             }
    //         }
    //     }
    //
    //     foreach (var tileRef in outerTiles)
    //     {
    //         var spawn = true;
    //         foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
    //         {
    //             if (HasComp<BlobBorderComponent>(ent))
    //             {
    //                 spawn = false;
    //                 break;
    //             }
    //         }
    //         if (spawn)
    //             EntityManager.SpawnEntity("BlobBorder", tileRef.GridIndices.ToEntityCoordinates(xform.GridUid.Value, _map));
    //     }
    // }

    // private void OnDestruction(EntityUid uid, BlobTileComponent component, DestructionEventArgs args)
    // {
    //     var xform = Transform(uid);
    //     var radius = 1.0f;
    //
    //     if (!_map.TryGetGrid(xform.GridUid, out var grid))
    //     {
    //         return;
    //     }
    //
    //     var localPos = xform.Coordinates.Position;
    //
    //     var innerTiles = grid.GetLocalTilesIntersecting(
    //         new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)), false).ToArray();
    //
    //     var centerTile = grid.GetLocalTilesIntersecting(
    //         new Box2(localPos, localPos)).ToArray();
    //
    //     innerTiles = innerTiles.Except(centerTile).ToArray();
    //
    //     foreach (var tileref in innerTiles)
    //     {
    //         foreach (var ent in grid.GetAnchoredEntities(tileref.GridIndices))
    //         {
    //             if (!HasComp<BlobTileComponent>(ent))
    //                 continue;
    //             var blockTiles = grid.GetLocalTilesIntersecting(
    //                 new Box2(Transform(ent).Coordinates.Position + new Vector2(-radius, -radius),
    //                     Transform(ent).Coordinates.Position + new Vector2(radius, radius)), false).ToArray();
    //
    //             var tilesToRemove = new List<TileRef>();
    //
    //             foreach (var blockTile in blockTiles)
    //             {
    //                 tilesToRemove.Add(blockTile);
    //             }
    //
    //             innerTiles = innerTiles.Except(tilesToRemove).ToArray();
    //         }
    //     }
    //
    //     foreach (var tileRef in innerTiles)
    //     {
    //         foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
    //         {
    //             if (HasComp<BlobBorderComponent>(ent))
    //             {
    //                 QueueDel(ent);
    //             }
    //         }
    //     }
    //
    //     EntityManager.SpawnEntity(component.BlobBorder, xform.Coordinates);
    // }
    //
    // private void OnStartup(EntityUid uid, BlobTileComponent component, ComponentStartup args)
    // {
    //     var xform = Transform(uid);
    //     var wallSpacing = 1.0f;
    //
    //     if (!_map.TryGetGrid(xform.GridUid, out var grid))
    //     {
    //         return;
    //     }
    //
    //     var localPos = xform.Coordinates.Position;
    //
    //     var innerTiles = grid.GetLocalTilesIntersecting(
    //         new Box2(localPos + new Vector2(-wallSpacing, -wallSpacing), localPos + new Vector2(wallSpacing, wallSpacing)), false).ToArray();
    //
    //     var centerTile = grid.GetLocalTilesIntersecting(
    //         new Box2(localPos, localPos)).ToArray();
    //
    //     foreach (var tileRef in centerTile)
    //     {
    //         foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
    //         {
    //             if (HasComp<BlobBorderComponent>(ent))
    //                 QueueDel(ent);
    //         }
    //     }
    //     innerTiles = innerTiles.Except(centerTile).ToArray();
    //
    //     foreach (var tileref in innerTiles)
    //     {
    //         var spaceNear = false;
    //         var hasBlobTile = false;
    //         foreach (var ent in grid.GetAnchoredEntities(tileref.GridIndices))
    //         {
    //             if (!HasComp<BlobTileComponent>(ent))
    //                 continue;
    //             var blockTiles = grid.GetLocalTilesIntersecting(
    //                 new Box2(Transform(ent).Coordinates.Position + new Vector2(-wallSpacing, -wallSpacing),
    //                     Transform(ent).Coordinates.Position + new Vector2(wallSpacing, wallSpacing)), false).ToArray();
    //
    //             var tilesToRemove = new List<TileRef>();
    //
    //             foreach (var blockTile in blockTiles)
    //             {
    //                 if (blockTile.Tile.IsEmpty)
    //                 {
    //                     spaceNear = true;
    //                 }
    //                 else
    //                 {
    //                     tilesToRemove.Add(blockTile);
    //                 }
    //             }
    //
    //             innerTiles = innerTiles.Except(tilesToRemove).ToArray();
    //
    //             hasBlobTile = true;
    //         }
    //
    //         if (!hasBlobTile || spaceNear)
    //             continue;
    //         {
    //             foreach (var ent in grid.GetAnchoredEntities(tileref.GridIndices))
    //             {
    //                 if (HasComp<BlobBorderComponent>(ent))
    //                 {
    //                     QueueDel(ent);
    //                 }
    //             }
    //         }
    //     }
    //
    //     var spaceNearCenter = false;
    //
    //     foreach (var tileRef in innerTiles)
    //     {
    //         var spawn = true;
    //         if (tileRef.Tile.IsEmpty)
    //         {
    //             spaceNearCenter = true;
    //             spawn = false;
    //         }
    //         if (grid.GetAnchoredEntities(tileRef.GridIndices).Any(ent => HasComp<BlobBorderComponent>(ent)))
    //         {
    //             spawn = false;
    //         }
    //         if (spawn)
    //             EntityManager.SpawnEntity(component.BlobBorder, tileRef.GridIndices.ToEntityCoordinates(xform.GridUid.Value, _map));
    //     }
    //     if (spaceNearCenter)
    //     {
    //         EntityManager.SpawnEntity(component.BlobBorder, xform.Coordinates);
    //     }
    // }
}
