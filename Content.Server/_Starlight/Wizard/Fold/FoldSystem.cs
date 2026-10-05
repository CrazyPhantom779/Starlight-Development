using Content.Shared._Starlight.Wizard.Fold;
using Content.Shared.Mobs.Components;

namespace Content.Server._Starlight.Wizard.Fold;

public sealed partial class FoldSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private readonly HashSet<Entity<MobStateComponent>> _nearby = [];

    [SubscribeLocalEvent]
    private void OnFoldSwap(FoldSwapSpellEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;
        var casterCoords = Transform(performer).Coordinates;

        // Only fold with something on the same map and within reach.
        if (!casterCoords.TryDistance(EntityManager, args.Target, out var casterDistance)
            || casterDistance > args.MaxCasterDistance)
            return;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(args.Target, args.Range, _nearby);

        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var mob in _nearby)
        {
            if (mob.Owner == performer)
                continue;

            if (!args.Target.TryDistance(EntityManager, Transform(mob).Coordinates, out var distance))
                continue;

            if (distance >= bestDistance)
                continue;

            best = mob.Owner;
            bestDistance = distance;
        }

        // Nothing there: leave it unhandled so the spell isn't spent.
        if (best is not { } other)
            return;

        _xform.SwapPositions((performer, Transform(performer)), (other, Transform(other)));
        args.Handled = true;
    }
}
