using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared._Starlight.Flock.Components;
using Content.Shared.Item;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Content.Server._Starlight.Flock;
using System.Numerics;
using Content.Shared.Damage.Systems;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators._Starlight.Flock;

public enum FlockPickKind : byte
{
    /// <summary>A living enemy of the flock that is able to fight back (shoot it).</summary>
    Enemy,
    /// <summary>An enemy that's down: go cage it.</summary>
    DownedEnemy,
    /// <summary>A non-flock tile (or priority tile) to convert.</summary>
    ConvertTile,
    /// <summary>Resource caches / loose items to absorb.</summary>
    Harvest,
    /// <summary>A hurt flock unit.</summary>
    Repair,
    /// <summary>Something marked for deconstruction.</summary>
    Deconstruct,
}

/// <summary>Picks a target for a flock unit based on <see cref="Kind"/>, and (for movement kinds) pathfinds to it.</summary>
public sealed partial class FlockPickTargetOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    private PathfindingSystem _pathfinding = default!;
    private EntityLookupSystem _lookup = default!;
    private MobStateSystem _mobState = default!;
    private DamageableSystem _damageable = default!;
    private FlockSystem _flock = default!;
    private FlockConversionSystem _conv = default!;
    private SharedMapSystem _map = default!;

    [DataField(required: true)]
    public FlockPickKind Kind;

    [DataField]
    public float Range = 8f;

    [DataField]
    public string TargetEntity = "Target";

    [DataField]
    public string TargetKey = "TargetCoordinates";

    [DataField]
    public string PathfindKey = "TargetPathfind";

    /// <summary>If false the target is returned without a path (used for ranged attacks).</summary>
    [DataField]
    public bool Path = true;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _mobState = sysManager.GetEntitySystem<MobStateSystem>();
        _damageable = sysManager.GetEntitySystem<DamageableSystem>();
        _flock = sysManager.GetEntitySystem<FlockSystem>();
        _conv = sysManager.GetEntitySystem<FlockConversionSystem>();
        _map = sysManager.GetEntitySystem<SharedMapSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!blackboard.TryGetValue<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, out var coords, _entManager))
            return (false, null);
        if (!_flock.TryGetFlock(owner, out var flock))
            return (false, null);

        if (Kind == FlockPickKind.ConvertTile)
            return await PlanTile(owner, coords, flock, blackboard, cancelToken);

        var candidates = new List<(EntityUid Uid, float Dist)>();
        var ownerMap = _entManager.System<SharedTransformSystem>().GetMapCoordinates(owner);
        foreach (var e in _lookup.GetEntitiesInRange(coords, Range))
        {
            if (e == owner || !Qualifies(e, owner, flock))
                continue;
            var d = (_entManager.System<SharedTransformSystem>().GetMapCoordinates(e).Position - ownerMap.Position).Length();
            candidates.Add((e, d));
        }
        if (candidates.Count == 0)
            return (false, null);
        candidates.Sort((a, b) => a.Dist.CompareTo(b.Dist));

        foreach (var (target, _) in candidates.Take(3))
        {
            var xform = _entManager.GetComponent<TransformComponent>(target);
            var fx = new Dictionary<string, object>
            {
                { TargetEntity, target },
                { TargetKey, xform.Coordinates },
            };
            if (!Path)
                return (true, fx);
            var path = await _pathfinding.GetPath(owner, target, 1f, cancelToken, flags: _pathfinding.GetFlags(blackboard));
            if (path.Result != PathResult.Path)
                continue;
            fx[PathfindKey] = path;
            return (true, fx);
        }
        return (false, null);
    }

    private bool Qualifies(EntityUid e, EntityUid __, Entity<FlockComponent> flock)
    {
        switch (Kind)
        {
            case FlockPickKind.Enemy:
                return flock.Comp.Enemies.Contains(e) && !_mobState.IsDead(e) && !_mobState.IsCritical(e)
                    && !_entManager.HasComponent<KnockedDownComponent>(e);
            case FlockPickKind.DownedEnemy:
                return flock.Comp.Enemies.Contains(e) && !_mobState.IsDead(e) &&
                    (_mobState.IsCritical(e) || _entManager.HasComponent<KnockedDownComponent>(e) || _entManager.HasComponent<StunnedComponent>(e));
            case FlockPickKind.Harvest:
                if (_entManager.TryGetComponent<FlockCacheComponent>(e, out _))
                    return true;
                return _entManager.HasComponent<ItemComponent>(e) && !_entManager.HasComponent<MobStateComponent>(e)
                    && !_entManager.GetComponent<TransformComponent>(e).Anchored
                    && !_entManager.HasComponent<FlockMemberComponent>(e);
            case FlockPickKind.Repair:
                if (!_entManager.HasComponent<FlockMemberComponent>(e)) return false;
                if (_entManager.TryGetComponent<FlockDroneComponent>(e, out var d) && d.Dead) return false;
                return _entManager.TryGetComponent<DamageableComponent>(e, out var dmg) && _damageable.GetTotalDamage((e, dmg)) > 20;
            case FlockPickKind.Deconstruct:
                return flock.Comp.DeconstructMarks.Contains(e);
        }
        return false;
    }

    private async Task<(bool Valid, Dictionary<string, object>? Effects)> PlanTile(
        EntityUid owner, EntityCoordinates coords, Entity<FlockComponent> flock, NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var xform = _entManager.GetComponent<TransformComponent>(owner);
        if (xform.GridUid is not { } grid || !_entManager.TryGetComponent<MapGridComponent>(grid, out var gc))
            return (false, null);
        var origin = _map.TileIndicesFor(grid, gc, coords);
        var r = (int) Range;
        var options = new List<(Vector2i Idx, float Score)>();

        // Priority tiles first
        foreach (var (g, idx) in flock.Comp.PriorityTiles)
        {
            if (g != grid) continue;
            var d = (idx - origin).Length;
            if (d <= r * 3 && _conv.CanConvertTile(grid, gc, idx))
                options.Add((idx, d - 100f));
        }
        for (var x = -r; x <= r; x++)
        {
            for (var y = -r; y <= r; y++)
            {
                var idx = origin + new Vector2i(x, y);
                if (!_conv.CanConvertTile(grid, gc, idx))
                    continue;
                options.Add((idx, new Vector2(x, y).Length() + _random.NextFloat(0f, 2f)));
            }
        }
        if (options.Count == 0)
            return (false, null);
        options.Sort((a, b) => a.Score.CompareTo(b.Score));

        foreach (var (idx, _) in options.Take(4))
        {
            var target = _map.GridTileToLocal(grid, gc, idx);
            var path = await _pathfinding.GetPath(owner, coords, target, 0.9f, cancelToken, _pathfinding.GetFlags(blackboard));
            if (path.Result != PathResult.Path)
                continue;
            return (true, new Dictionary<string, object>
            {
                { TargetEntity, owner },
                { TargetKey, target },
                { PathfindKey, path },
            });
        }
        return (false, null);
    }
}
