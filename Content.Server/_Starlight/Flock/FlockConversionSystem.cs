using Content.Shared._Starlight.Flock;
using Content.Shared._Starlight.Flock.Components;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Flock;

/// <summary>Turns station tiles and entities into flock equivalents.</summary>
public sealed partial class FlockConversionSystem : EntitySystem
{
    [Dependency] private FlockSystem _flock = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private List<FlockConversionPrototype> _conversions = [];
    private const string FloorId = "FloorFlock";
    // private const string SettingsId = "FlockDefault";

    public override void Initialize()
    {
        base.Initialize();
        _proto.PrototypesReloaded += _ => LoadConversions();
        LoadConversions();
    }

    private void LoadConversions()
    {
        _conversions = [.. _proto.EnumeratePrototypes<FlockConversionPrototype>()];
        _conversions.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    public bool IsFlockTile(TileRef tile)
        => !tile.Tile.IsEmpty && _tileDefs[tile.Tile.TypeId].ID.StartsWith("FloorFlock");

    public bool IsFlockTile(EntityUid grid, MapGridComponent comp, Vector2i idx)
        => IsFlockTile(_map.GetTileRef(grid, comp, idx));

    public bool TryGetTile(EntityCoordinates coords, out EntityUid grid, out MapGridComponent comp, out Vector2i idx)
    {
        idx = default;
        comp = default!;
        grid = default;
        if (_xform.GetGrid(coords) is not { } g || !TryComp<MapGridComponent>(g, out var mg))
            return false;
        grid = g;
        comp = mg;
        idx = _map.TileIndicesFor(g, mg, coords);
        return true;
    }

    /// <summary>Can a tile be converted? Space tiles only if a neighbour is already solid ground (goon lets the flock "grow").</summary>
    public bool CanConvertTile(EntityUid grid, MapGridComponent comp, Vector2i idx)
    {
        var tile = _map.GetTileRef(grid, comp, idx);
        if (IsFlockTile(tile))
            return false;
        if (!_turf.IsSpace(tile))
            return true;
        foreach (var d in new[] { new Vector2i(1, 0), new Vector2i(-1, 0), new Vector2i(0, 1), new Vector2i(0, -1) })
        {
            var n = _map.GetTileRef(grid, comp, idx + d);
            if (IsFlockTile(n))
                return true;
        }
        return false;
    }

    public bool TryConvertTile(Entity<FlockComponent> flock, EntityUid grid, MapGridComponent comp, Vector2i idx)
    {
        if (!CanConvertTile(grid, comp, idx))
            return false;
        var tile = _map.GetTileRef(grid, comp, idx);
        var def = (ContentTileDefinition) _tileDefs[FloorId];
        if (_turf.IsSpace(tile))
            _map.SetTile(grid, comp, idx, new Tile(def.TileId, variant: _tile.PickVariant(def)));
        else
            _tile.ReplaceTile(tile, def);
        _flock.AddFlockTile(flock, grid, idx);

        var coords = _map.GridTileToLocal(grid, comp, idx);
        foreach (var e in _lookup.GetEntitiesInRange(coords, 0.4f, LookupFlags.Static | LookupFlags.Sundries))
            TryConvertEntity(flock, e);
        return true;
    }

    public FlockConversionPrototype? FindConversion(EntityUid target)
    {
        if (HasComp<FlockMemberComponent>(target) || HasComp<FlockConvertedComponent>(target))
            return null;
        foreach (var c in _conversions)
            if (_whitelist.IsWhitelistPass(c.Whitelist, target))
                return c;
        return null;
    }

    public bool TryConvertEntity(Entity<FlockComponent> flock, EntityUid target, out EntityUid? result)
    {
        result = null;
        var conv = FindConversion(target);
        if (conv == null)
            return false;
        var xform = Transform(target);
        var coords = xform.Coordinates;
        var rot = xform.LocalRotation;
        var anchored = xform.Anchored;
        QueueDel(target);
        if (conv.Replacement is { } repl)
        {
            var n = Spawn(repl, coords);
            var nx = Transform(n);
            _xform.SetLocalRotation(n, rot);
            if (anchored && !nx.Anchored)
                _xform.AnchorEntity((n, nx));
            EnsureComp<FlockConvertedComponent>(n);
            _flock.AddMember(flock, n);
            result = n;
        }
        return true;
    }

    public bool TryConvertEntity(Entity<FlockComponent> flock, EntityUid target) => TryConvertEntity(flock, target, out _);

    /// <summary>Convert everything convertible near a point. Used by bits, rift and relay. Returns number converted.</summary>
    public int ConvertArea(Entity<FlockComponent> flock, EntityCoordinates center, float radius, int max = 1)
    {
        var n = 0;
        foreach (var e in _lookup.GetEntitiesInRange(center, radius, LookupFlags.Static | LookupFlags.Sundries))
        {
            if (n >= max) return n;
            if (TryConvertEntity(flock, e)) n++;
        }
        if (n < max && TryGetTile(center, out var g, out var gc, out var idx))
        {
            if (TryConvertTile(flock, g, gc, idx)) n++;
        }
        return n;
    }
}
