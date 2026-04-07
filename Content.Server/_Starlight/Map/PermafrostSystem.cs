using System;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;
using Content.Server.GameTicking.Events;
using Content.Shared.Maps;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.EntitySerialization.Systems;
using Content.Server.Maps;

namespace Content.Server._Starlight.Map;

public sealed partial class PermafrostSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IGameMapManager _gameMapManager = default!;

    private readonly ResPath _secondaryMapPath = new("Maps/_Starlight/Nonstations/PermafrostSpace.yml");
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("starlight.permafrost");
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {
        // Only proceed if the selected game map is Permafrost
        var selectedMap = _gameMapManager.GetSelectedMap();
        if (selectedMap is null || selectedMap.ID != "StarlightPermafrost")
            return;

        // Load secondary map
        if (!_mapLoader.TryLoadMap(_secondaryMapPath, out var mapEntity, out var error))
        {
            _sawmill.Error($"[PermafrostSystem] Failed to load secondary map: {error}");
            return;
        }

        // Ensure map entity is not null and extract MapId
        if (!TryComp<MapComponent>(mapEntity, out var mapComp))
        {
            _sawmill.Error("[PermafrostSystem] Loaded map entity has no MapComponent.");
            return;
        }

        var mapId = mapComp.MapId;

        _sawmill.Info($"[PermafrostSystem] Loaded secondary map: {mapId}");

        // Unpause the map
        _map.SetPaused(mapId, false);
    }
}
