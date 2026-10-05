using System.Linq;
using System.Numerics;
using Content.Server.Actions;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Destructible;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.ActionBlocker;
using Content.Shared.Blob;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.SubFloor;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Damage.Systems;
using Robust.Server.Audio;
using Content.Shared.Atmos.Components;
using Content.Shared.Interaction;
using Robust.Shared.Player;

namespace Content.Server.Blob;

public sealed partial class BlobObserverSystem : SharedBlobObserverSystem
{
    // Prototype IDs
    private const string HelpBlob = "HelpBlob";
    private const string SwapBlobChem = "SwapBlobChem";
    private const string TeleportBlobToCore = "TeleportBlobToCore";
    private const string TeleportBlobToNode = "TeleportBlobToNode";
    private const string CreateBlobFactory = "CreateBlobFactory";
    private const string CreateBlobResource = "CreateBlobResource";
    private const string CreateBlobNode = "CreateBlobNode";
    private const string CreateBlobbernaut = "CreateBlobbernaut";
    private const string SplitBlobCore = "SplitBlobCore";
    private const string SwapBlobCore = "SwapBlobCore";

    [Dependency] private ActionsSystem _action = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private BlobCoreSystem _blobCoreSystem = default!;
    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private ExplosionSystem _explosionSystem = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private EmpSystem _empSystem = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private TransformSystem _transformSystem = default!;

    [SubscribeLocalEvent]
    private void OnBlobSwapChem(EntityUid uid, BlobObserverComponent observerComponent,
        BlobSwapChemActionEvent args)
    {
        TryOpenUi(uid, args.Performer, observerComponent);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnChemSelected(EntityUid uid, BlobObserverComponent component, BlobChemSwapPrototypeSelectedMessage args)
    {
        if (component.Core == null || !TryComp<BlobCoreComponent>(component.Core.Value, out var blobCoreComponent))
            return;

        if (component.SelectedChemId == args.SelectedId)
            return;

        if (!_blobCoreSystem.TryUseAbility(uid, component.Core.Value, blobCoreComponent,
                blobCoreComponent.SwapChemCost))
            return;

        ChangeChem(uid, args.SelectedId, component);
    }

    private void ChangeChem(EntityUid uid, BlobChemType newChem, BlobObserverComponent component)
    {
        if (component.Core == null || !TryComp<BlobCoreComponent>(component.Core.Value, out var blobCoreComponent))
            return;
        component.SelectedChemId = newChem;
        _blobCoreSystem.ChangeChem(component.Core.Value, newChem, blobCoreComponent);

        _popup.PopupEntity(Loc.GetString("blob-spent-resource", ("point", blobCoreComponent.SwapChemCost)),
            uid,
            uid,
            PopupType.LargeCaution);

        UpdateUi(uid, component);
    }

    [SubscribeLocalEvent]
    private void GetState(EntityUid uid, BlobObserverComponent component, ref ComponentGetState args) =>
        args.State = new BlobChemSwapComponentState
    {
        SelectedChem = component.SelectedChemId
    };

    private void TryOpenUi(EntityUid uid, EntityUid user, BlobObserverComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!TryComp(user, out ActorComponent? actor))
            return;

        _uiSystem.TryToggleUi(uid, BlobChemSwapUiKey.Key, actor.PlayerSession);
    }

    public void UpdateUi(EntityUid uid, BlobObserverComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.Core == null || !TryComp<BlobCoreComponent>(component.Core.Value, out var blobCoreComponent))
            return;

        var state = new BlobChemSwapBoundUserInterfaceState(blobCoreComponent.ChemColors, component.SelectedChemId);

        _uiSystem.SetUiState(uid, BlobChemSwapUiKey.Key, state);
    }

    // TODO: This is very bad, but it is clearly better than invisible walls, let someone do better.
    [SubscribeLocalEvent]
    private void OnMoveEvent(EntityUid uid, BlobObserverComponent observerComponent, ref MoveEvent args)
    {
        if (observerComponent.IsProcessingMoveEvent)
            return;

        observerComponent.IsProcessingMoveEvent = true;

        if (observerComponent.Core == null)
        {
            observerComponent.IsProcessingMoveEvent = false;
            return;
        }

        var xform = Transform(observerComponent.Core.Value);
        var corePos = xform.Coordinates;

        var (nearestEntityUid, nearestDistance) = CalculateNearestBlobTileDistance(args.NewPosition);

        if (nearestEntityUid == null)
            return;

        if (nearestDistance > 5f)
        {
            _transform.SetCoordinates(uid, corePos);

            observerComponent.IsProcessingMoveEvent = false;
            return;
        }

        if (nearestDistance > 3f)
        {
            observerComponent.CanMove = true;
            _blocker.UpdateCanMove(uid);
            var direction = Transform(nearestEntityUid.Value).Coordinates.Position - args.NewPosition.Position;
            var newPosition = args.NewPosition.Offset(direction * 0.1f);

            _transform.SetCoordinates(uid, newPosition);
        }

        observerComponent.IsProcessingMoveEvent = false;
    }

    private (EntityUid? nearestEntityUid, float nearestDistance) CalculateNearestBlobTileDistance(EntityCoordinates position)
    {
        var nearestDistance = float.MaxValue;
        EntityUid? nearestEntityUid = null;

        foreach (var lookupUid in _lookup.GetEntitiesInRange(position, 5f))
        {
            if (!HasComp<BlobTileComponent>(lookupUid))
                continue;
            var tileCords = Transform(lookupUid).Coordinates;
            var distance = Vector2.Distance(position.Position, tileCords.Position);

            if (!(distance < nearestDistance))
                continue;
            nearestDistance = distance;
            nearestEntityUid = lookupUid;
        }

        return (nearestEntityUid, nearestDistance);
    }

    [SubscribeLocalEvent]
    private void OnBlobHelp(EntityUid uid, BlobObserverComponent observerComponent,
        BlobHelpActionEvent args)
    {
        _popup.PopupEntity(Loc.GetString("blob-help"), uid, uid, PopupType.Large);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnSplitCore(EntityUid uid, BlobObserverComponent observerComponent,
        BlobSplitCoreActionEvent args)
    {
        if (args.Handled)
            return;

        if (observerComponent.Core == null || !TryComp<BlobCoreComponent>(observerComponent.Core.Value, out var blobCoreComponent))
            return;

        if (!blobCoreComponent.CanSplit)
        {
            _popup.PopupEntity(Loc.GetString("blob-cant-split"), uid, uid, PopupType.Large);
            return;
        }

        var gridUid = _transform.GetGrid(args.Target);

        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return;

        var centerTile = _mapSystem.GetLocalTilesIntersecting(
            gridUid.Value,
            grid,
            new Box2(args.Target.Position, args.Target.Position)).ToArray();

        EntityUid? blobTile = null;

        foreach (var tileref in centerTile)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid.Value, grid, tileref.GridIndices))
            {
                if (!TryComp<BlobTileComponent>(ent, out _))
                    continue;
                blobTile = ent;
                break;
            }
        }

        if (blobTile == null || !TryComp<BlobNodeComponent>(blobTile, out var _))
        {
            _popup.PopupEntity(Loc.GetString("blob-target-node-blob-invalid"), uid, uid, PopupType.Large);
            args.Handled = true;
            return;
        }

        if (!_blobCoreSystem.TryUseAbility(uid, observerComponent.Core.Value, blobCoreComponent,
                blobCoreComponent.SplitCoreCost))
        {
            args.Handled = true;
            return;
        }

        QueueDel(blobTile.Value);
        var newCore = Spawn(blobCoreComponent.CoreBlobTile, args.Target);
        blobCoreComponent.CanSplit = false;
        if (TryComp<BlobCoreComponent>(newCore, out var newBlobCoreComponent))
            newBlobCoreComponent.CanSplit = false;

        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnSwapCore(EntityUid uid, BlobObserverComponent observerComponent,
        BlobSwapCoreActionEvent args)
    {
        if (args.Handled)
            return;

        if (observerComponent.Core == null || !TryComp<BlobCoreComponent>(observerComponent.Core.Value, out var blobCoreComponent))
            return;

        var gridUid = _transform.GetGrid(args.Target);

        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return;

        var centerTile = _mapSystem.GetLocalTilesIntersecting(
            gridUid.Value,
            grid,
            new Box2(args.Target.Position, args.Target.Position)).ToArray();

        EntityUid? blobTile = null;

        foreach (var tileRef in centerTile)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid.Value, grid, tileRef.GridIndices))
            {
                if (!TryComp<BlobTileComponent>(ent, out var blobTileComponent))
                    continue;
                blobTile = ent;
                break;
            }
        }

        if (blobTile == null || !TryComp<BlobNodeComponent>(blobTile, out var blobNodeComponent))
        {
            _popup.PopupEntity(Loc.GetString("blob-target-node-blob-invalid"), uid, uid, PopupType.Large);
            args.Handled = true;
            return;
        }

        if (!_blobCoreSystem.TryUseAbility(uid, observerComponent.Core.Value, blobCoreComponent,
                blobCoreComponent.SwapCoreCost))
        {
            args.Handled = true;
            return;
        }

        var nodePos = Transform(blobTile.Value).Coordinates;
        var corePos = Transform(observerComponent.Core.Value).Coordinates;
        _transform.SetCoordinates(observerComponent.Core.Value, nodePos.SnapToGrid());
        _transform.SetCoordinates(blobTile.Value, corePos.SnapToGrid());
        var xformCore = Transform(observerComponent.Core.Value);
        if (!xformCore.Anchored)
        {
            _transform.AnchorEntity(observerComponent.Core.Value, xformCore);
        }
        var xformNode = Transform(blobTile.Value);
        if (!xformNode.Anchored)
        {
            _transform.AnchorEntity(blobTile.Value, xformNode);
        }
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnBlobToNode(EntityUid uid, BlobObserverComponent observerComponent,
        BlobToNodeActionEvent args)
    {
        if (args.Handled)
            return;

        if (observerComponent.Core == null || !TryComp<BlobCoreComponent>(observerComponent.Core.Value, out var blobCoreComponent))
            return;

        var blobNodes = new List<EntityUid>();

        var blobNodeQuery = EntityQueryEnumerator<BlobNodeComponent, BlobTileComponent>();
        while (blobNodeQuery.MoveNext(out var ent, out var node, out var tile))
        {
            if (tile.Core == observerComponent.Core.Value && !HasComp<BlobCoreComponent>(ent))
                blobNodes.Add(ent);
        }

        if (blobNodes.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("blob-not-have-nodes"), uid, uid, PopupType.Large);
            args.Handled = true;
            return;
        }

        _transform.SetCoordinates(uid, Transform(_random.Pick(blobNodes)).Coordinates);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnCreateBlobbernaut(EntityUid uid, BlobObserverComponent observerComponent,
        BlobCreateBlobbernautActionEvent args)
    {
        if (args.Handled)
            return;

        if (observerComponent.Core == null ||
            !TryComp<BlobCoreComponent>(observerComponent.Core.Value, out var blobCoreComponent))
            return;

        var gridUid = _transform.GetGrid(args.Target);

        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return;

        var centerTile = _mapSystem.GetLocalTilesIntersecting(
            gridUid.Value,
            grid,
            new Box2(args.Target.Position, args.Target.Position)).ToArray();

        EntityUid? blobTile = null;

        foreach (var tileRef in centerTile)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid.Value, grid, tileRef.GridIndices))
            {
                if (!HasComp<BlobFactoryComponent>(ent))
                    continue;
                blobTile = ent;
                break;
            }
        }

        if (blobTile == null || !TryComp<BlobFactoryComponent>(blobTile, out var blobFactoryComponent))
        {
            _popup.PopupEntity(Loc.GetString("blob-target-factory-blob-invalid"), uid, uid, PopupType.LargeCaution);
            return;
        }

        if (blobFactoryComponent.Blobbernaut != null)
        {
            _popup.PopupEntity(Loc.GetString("blob-target-already-produce-blobbernaut"), uid, uid, PopupType.LargeCaution);
            return;
        }

        if (!_blobCoreSystem.TryUseAbility(uid, observerComponent.Core.Value, blobCoreComponent, blobCoreComponent.BlobbernautCost))
            return;

        var ev = new ProduceBlobbernautEvent();
        RaiseLocalEvent(blobTile.Value, ev);

        _popup.PopupEntity(Loc.GetString("blob-spent-resource", ("point", blobCoreComponent.BlobbernautCost)),
            blobTile.Value,
            uid,
            PopupType.LargeCaution);

        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnBlobToCore(EntityUid uid, BlobObserverComponent observerComponent,
        BlobToCoreActionEvent args)
    {
        if (args.Handled)
            return;
        if (observerComponent.Core == null ||
            !TryComp<BlobCoreComponent>(observerComponent.Core.Value, out _))
            return;

        _transform.SetCoordinates(uid, Transform(observerComponent.Core.Value).Coordinates);
    }

    [SubscribeLocalEvent]
    private void OnCreateNode(EntityUid uid, BlobObserverComponent observerComponent,
        BlobCreateNodeActionEvent args)
    {
        if (args.Handled)
            return;

        if (observerComponent.Core == null ||
            !TryComp<BlobCoreComponent>(observerComponent.Core.Value, out var blobCoreComponent))
            return;

        var gridUid = _transform.GetGrid(args.Target);

        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return;

        var centerTile = _mapSystem.GetLocalTilesIntersecting(
            gridUid.Value,
            grid,
            new Box2(args.Target.Position, args.Target.Position)).ToArray();

        var blobTileType = BlobTileType.None;
        EntityUid? blobTile = null;

        foreach (var tileRef in centerTile)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid.Value, grid,
            tileRef.GridIndices))
            {
                if (!TryComp<BlobTileComponent>(ent, out var blobTileComponent))
                    continue;
                blobTileType = blobTileComponent.BlobTileType;
                blobTile = ent;
                break;
            }
        }

        if (blobTileType is not BlobTileType.Normal ||
            blobTile == null)
        {
            _popup.PopupEntity(Loc.GetString("blob-target-normal-blob-invalid"), uid, uid, PopupType.Large);
            return;
        }

        var xform = Transform(blobTile.Value);

        var localPos = xform.Coordinates.Position;

        var radius = blobCoreComponent.NodeRadiusLimit;

        var innerTiles = _mapSystem.GetLocalTilesIntersecting(
            gridUid.Value,
            grid,
            new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)), false).ToArray();

        foreach (var tileRef in innerTiles)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid.Value, grid, tileRef.GridIndices))
            {
                if (!HasComp<BlobNodeComponent>(ent))
                    continue;
                _popup.PopupEntity(Loc.GetString("blob-target-close-to-node"), uid, uid, PopupType.Large);
                return;
            }
        }

        if (!_blobCoreSystem.TryUseAbility(uid, observerComponent.Core.Value, blobCoreComponent, blobCoreComponent.NodeBlobCost))
            return;

        if (!_blobCoreSystem.TransformBlobTile(blobTile.Value,
                observerComponent.Core.Value,
                blobCoreComponent.NodeBlobTile,
                args.Target,
                blobCoreComponent,
                transformCost: blobCoreComponent.NodeBlobCost))
            return;

        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnCreateResource(EntityUid uid, BlobObserverComponent observerComponent,
        BlobCreateResourceActionEvent args)
    {
        if (args.Handled)
            return;

        if (observerComponent.Core == null ||
            !TryComp<BlobCoreComponent>(observerComponent.Core.Value, out var blobCoreComponent))
            return;

        var gridUid = _transform.GetGrid(args.Target);

        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return;

        var centerTile = _mapSystem.GetLocalTilesIntersecting(
            gridUid.Value,
            grid,
            new Box2(args.Target.Position, args.Target.Position)).ToArray();

        var blobTileType = BlobTileType.None;
        EntityUid? blobTile = null;

        foreach (var tileref in centerTile)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid.Value, grid, tileref.GridIndices))
            {
                if (!TryComp<BlobTileComponent>(ent, out var blobTileComponent))
                    continue;
                blobTileType = blobTileComponent.BlobTileType;
                blobTile = ent;
                break;
            }
        }

        if (blobTileType is not BlobTileType.Normal ||
            blobTile == null)
        {
            _popup.PopupEntity(Loc.GetString("blob-target-normal-blob-invalid"), uid, uid, PopupType.Large);
            return;
        }

        var xform = Transform(blobTile.Value);

        var localPos = xform.Coordinates.Position;

        var radius = blobCoreComponent.ResourceRadiusLimit;

        var innerTiles = _mapSystem.GetLocalTilesIntersecting(
            gridUid.Value,
            grid,
            new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)), false).ToArray();

        foreach (var tileRef in innerTiles)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid.Value, grid, tileRef.GridIndices))
            {
                if (!HasComp<BlobResourceComponent>(ent) || HasComp<BlobCoreComponent>(ent))
                    continue;
                _popup.PopupEntity(Loc.GetString("blob-target-close-to-resource"), uid, uid, PopupType.Large);
                return;
            }
        }

        if (!_blobCoreSystem.CheckNearNode(uid, xform.Coordinates, gridUid.Value, grid, blobCoreComponent))
            return;

        if (!_blobCoreSystem.TryUseAbility(uid,
                observerComponent.Core.Value,
                blobCoreComponent,
                blobCoreComponent.ResourceBlobCost))
            return;

        if (!_blobCoreSystem.TransformBlobTile(blobTile.Value,
                observerComponent.Core.Value,
                blobCoreComponent.ResourceBlobTile,
                args.Target,
                blobCoreComponent,
                transformCost: blobCoreComponent.ResourceBlobCost))
            return;

        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnInteract(EntityUid uid, BlobObserverComponent observerComponent, InteractHandEvent args)
    {
        if (args.Target == args.User)
            return;

        if (observerComponent.Core == null ||
            !TryComp<BlobCoreComponent>(observerComponent.Core.Value, out var blobCoreComponent))
            return;

        if (args.Target == null)
            return;

        var location = Transform(args.Target).Coordinates;
        if (!location.IsValid(EntityManager))
            return;

        var gridId = _transform.GetGrid(args.Target);
        if (!HasComp<MapGridComponent>(gridId))
        {
            location = location.AlignWithClosestGridTile();
            gridId = _transform.GetGrid(args.Target);
            if (!HasComp<MapGridComponent>(gridId))
                return;
        }

        var xform = Transform(args.Target);
        var gridUid = xform.GridUid;

        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return;

        if (args.Target != null &&
            !HasComp<BlobTileComponent>(args.Target) &&
            !HasComp<BlobMobComponent>(args.Target))
        {
            var target = args.Target;

            // Check if the target is adjacent to a tile with BlobCellComponent horizontally or vertically
            var targetXform = Transform(args.Target);
            var mobTile = _mapSystem.GetTileRef(gridUid.Value, grid, xform.Coordinates);

            var mobAdjacentTiles = new[]
            {
                mobTile.GridIndices.Offset(Direction.East),
                mobTile.GridIndices.Offset(Direction.West),
                mobTile.GridIndices.Offset(Direction.North),
                mobTile.GridIndices.Offset(Direction.South)
            };
            if (mobAdjacentTiles.Any(indices => _mapSystem.GetAnchoredEntities(gridUid.Value, grid, indices).Any(ent => HasComp<BlobTileComponent>(ent))))
            {
                if (HasComp<DestructibleComponent>(target) && !HasComp<ItemComponent>(target)&& !HasComp<SubFloorHideComponent>(target))
                {
                    if (_blobCoreSystem.TryUseAbility(uid, observerComponent.Core.Value, blobCoreComponent, blobCoreComponent.AttackCost))
                    {
                        if (_gameTiming.CurTime < blobCoreComponent.NextAction)
                            return;
                        if (blobCoreComponent.Observer != null)
                        {
                            _popup.PopupCoordinates(Loc.GetString("blob-spent-resource", ("point", blobCoreComponent.AttackCost)),
                                Transform(args.Target).Coordinates,
                                blobCoreComponent.Observer.Value,
                                PopupType.LargeCaution);
                        }
                        _damageableSystem.TryChangeDamage(target, blobCoreComponent.ChemDamageDict[blobCoreComponent.CurrentChem]);

                        if (blobCoreComponent.CurrentChem == BlobChemType.ExplosiveLattice)
                        {
                            _explosionSystem.QueueExplosion(target, blobCoreComponent.BlobExplosive, 4, 1, 6, maxTileBreak: 0);
                        }

                        if (blobCoreComponent.CurrentChem == BlobChemType.ElectromagneticWeb)
                        {
                            if (_random.Prob(0.2f))
                            // 'TransformComponent.MapPosition' is obsolete: 'Use TransformSystem.GetMapCoordinates'
                                _empSystem.EmpPulse(_transformSystem.GetMapCoordinates(args.Target), 3f, 50f, TimeSpan.FromSeconds(3f));
                        }

                        if (blobCoreComponent.CurrentChem == BlobChemType.BlazingOil)
                        {
                            if (TryComp<FlammableComponent>(target, out var flammable))
                            {
                                flammable.FireStacks += 2;
                                _flammable.Ignite(target, uid, flammable);
                            }
                        }
                        blobCoreComponent.NextAction =
                            _gameTiming.CurTime + TimeSpan.FromSeconds(blobCoreComponent.AttackRate);
                        _audioSystem.PlayPvs(blobCoreComponent.AttackSound, uid, AudioParams.Default);
                        return;
                    }
                }
            }
        }

        var centerTile = _mapSystem.GetLocalTilesIntersecting(
            gridUid.Value,
            grid,
            new Box2(location.Position, location.Position), false).ToArray();

        var targetTileEmplty = false;
        foreach (var tileRef in centerTile)
        {
            if (tileRef.Tile.IsEmpty)
            {
                targetTileEmplty = true;
            }

            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid.Value, grid, tileRef.GridIndices))
            {
                if (HasComp<BlobTileComponent>(ent))
                    return;
            }

            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid.Value, grid, tileRef.GridIndices))
            {
                if (HasComp<MobStateComponent>(ent) && !HasComp<BlobMobComponent>(ent))
                    return;
            }
        }

        var targetTile = _mapSystem.GetTileRef(gridUid.Value, grid, location);

        var adjacentTiles = new[]
        {
            targetTile.GridIndices.Offset(Direction.East),
            targetTile.GridIndices.Offset(Direction.West),
            targetTile.GridIndices.Offset(Direction.North),
            targetTile.GridIndices.Offset(Direction.South)
        };

        if (!adjacentTiles.Any(indices =>
                _mapSystem.GetAnchoredEntities(gridUid.Value, grid, indices).Any(ent => HasComp<BlobTileComponent>(ent))))
            return;
        var cost = blobCoreComponent.NormalBlobCost;
        if (targetTileEmplty)
        {
            cost *= 2;
        }

        if (!_blobCoreSystem.TryUseAbility(uid, observerComponent.Core.Value, blobCoreComponent, cost))
            return;

        if (targetTileEmplty)
        {
            var plating = _tileDefinitionManager["Plating"];
            var platingTile = new Tile(plating.TileId);
            _mapSystem.SetTile(gridUid.Value, grid, location, platingTile);
        }

        _blobCoreSystem.TransformBlobTile(null,
            observerComponent.Core.Value,
            blobCoreComponent.NormalBlobTile,
            location,
            blobCoreComponent,
            transformCost: cost);
    }

    [SubscribeLocalEvent]
    private void OnStartup(EntityUid uid, BlobObserverComponent component, ComponentStartup args)
    {
        _action.AddAction(uid, HelpBlob);
        _action.AddAction(uid, SwapBlobChem);
        _action.AddAction(uid, TeleportBlobToCore);
        _action.AddAction(uid, TeleportBlobToNode);

        _action.AddAction(uid, CreateBlobFactory);
        _action.AddAction(uid, CreateBlobResource);
        _action.AddAction(uid, CreateBlobNode);
        _action.AddAction(uid, CreateBlobbernaut);

        _action.AddAction(uid, SplitBlobCore);
        _action.AddAction(uid, SwapBlobCore);
    }

    [SubscribeLocalEvent]
    private void OnCreateFactory(EntityUid uid, BlobObserverComponent observerComponent, BlobCreateFactoryActionEvent args)
    {
        if (args.Handled)
            return;

        if (observerComponent.Core == null ||
            !TryComp<BlobCoreComponent>(observerComponent.Core.Value, out var blobCoreComponent))
            return;

        var gridUid = _transformSystem.GetGrid(args.Target);

        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return;

        var centerTile = _mapSystem.GetLocalTilesIntersecting(
            gridUid.Value,
            grid,
            new Box2(args.Target.Position, args.Target.Position)).ToArray();

        var blobTileType = BlobTileType.None;
        EntityUid? blobTile = null;

        foreach (var tileRef in centerTile)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid.Value, grid, tileRef.GridIndices))
            {
                if (!TryComp<BlobTileComponent>(ent, out var blobTileComponent))
                    continue;
                blobTileType = blobTileComponent.BlobTileType;
                blobTile = ent;
                break;
            }
        }

        if (blobTileType is not BlobTileType.Normal ||
            blobTile == null)
        {
            _popup.PopupEntity(Loc.GetString("blob-target-normal-blob-invalid"), uid, uid, PopupType.Large);
            return;
        }

        var xform = Transform(blobTile.Value);

        var localPos = xform.Coordinates.Position;

        var radius = blobCoreComponent.FactoryRadiusLimit;

        var innerTiles = _mapSystem.GetLocalTilesIntersecting(gridUid.Value, grid, new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)), false).ToArray();

        foreach (var tileRef in innerTiles)
        {
            foreach (var ent in _mapSystem.GetAnchoredEntities(gridUid.Value, grid, tileRef.GridIndices))
            {
                if (!HasComp<BlobFactoryComponent>(ent))
                    continue;
                _popup.PopupEntity(Loc.GetString("Слишком близко к другой фабрике"), uid, uid, PopupType.Large);
                return;
            }
        }

        if (!_blobCoreSystem.CheckNearNode(uid, xform.Coordinates, gridUid.Value, grid, blobCoreComponent))
            return;

        if (!_blobCoreSystem.TryUseAbility(uid, observerComponent.Core.Value, blobCoreComponent,
                blobCoreComponent.FactoryBlobCost))
        {
            args.Handled = true;
            return;
        }

        if (!_blobCoreSystem.TransformBlobTile(null,
                observerComponent.Core.Value,
                blobCoreComponent.FactoryBlobTile,
                args.Target,
                blobCoreComponent,
                transformCost: blobCoreComponent.FactoryBlobCost))
            return;

        args.Handled = true;
    }
}
