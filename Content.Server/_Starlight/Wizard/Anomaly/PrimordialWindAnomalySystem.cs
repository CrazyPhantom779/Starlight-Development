using System.Linq;
using Content.Server.GameTicking;
using Content.Shared.Anomaly.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Wizard.Anomaly;

public sealed partial class PrimordialWindAnomalySystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private GameTicker _ticker = default!;

    private readonly HashSet<Entity<MobStateComponent>> _nearby = [];

    private static readonly string[] _visions =
    [
        "primordial-vision-1",
        "primordial-vision-2",
        "primordial-vision-3",
        "primordial-vision-4",
    ];

    [SubscribeLocalEvent]
    private void OnPulse(Entity<PrimordialWindAnomalyComponent> ent, ref AnomalyPulseEvent args)
    {
        var coords = Transform(ent).Coordinates;
        Spawn(ent.Comp.PulseEffect, coords);

        _nearby.Clear();
        _lookup.GetEntitiesInRange(coords, ent.Comp.PulseRange, _nearby);

        foreach (var mob in _nearby)
        {
            if (!_random.Prob(ent.Comp.VisionChance))
                continue;

            _popup.PopupEntity(Loc.GetString(_random.Pick(_visions)), mob, mob, PopupType.MediumCaution);
        }

        // Fold space: two creatures trade places.
        if (_nearby.Count >= 2 && _random.Prob(ent.Comp.SwapChance))
        {
            var pair = _random.GetItems(_nearby.ToList(), 2, allowDuplicates: false);
            _xform.SwapPositions((pair[0].Owner, Transform(pair[0].Owner)), (pair[1].Owner, Transform(pair[1].Owner)));
        }
    }

    [SubscribeLocalEvent]
    private void OnSupercritical(Entity<PrimordialWindAnomalyComponent> ent, ref AnomalySupercriticalEvent args)
    {
        Spawn(ent.Comp.SupercriticalEffect, Transform(ent).Coordinates);

        if (ent.Comp.AwakeningRule is { } rule)
            _ticker.StartGameRule(rule);
    }
}
