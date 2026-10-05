using System.Linq;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Wizard.Wind;

/// <summary>
/// Rolls and applies Gusts when a caster overcasts.
/// </summary>
public sealed partial class WindGustSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private AudioSystem _audio = default!;

    private readonly HashSet<Entity<MobStateComponent>> _nearby = [];

    [SubscribeLocalEvent]
    private void OnOverdrawn(Entity<WindComponent> ent, ref WindOverdrawnEvent args)
    {
        var limit = MathF.Max(1f, ent.Comp.OverdrawLimit);
        var chance = Math.Clamp(0.3f + (0.7f * (args.Deficit / limit)), 0f, 1f) * ent.Comp.GustChanceMultiplier;
        if (!_random.Prob(Math.Clamp(chance, 0f, 1f)))
            return;

        var gusts = _proto.EnumeratePrototypes<WindGustPrototype>().Where(g => g.Weight > 0f).ToList();
        if (gusts.Count == 0)
            return;

        var gust = PickWeighted(gusts);
        Apply(ent.Owner, gust);
    }

    private WindGustPrototype PickWeighted(List<WindGustPrototype> gusts)
    {
        var total = 0f;
        foreach (var gust in gusts)
            total += gust.Weight;

        var roll = _random.NextFloat() * total;
        foreach (var gust in gusts)
        {
            roll -= gust.Weight;
            if (roll <= 0f)
                return gust;
        }

        return gusts[^1];
    }

    private void Apply(EntityUid caster, WindGustPrototype gust)
    {
        if (gust.Message is { } message)
            _popup.PopupEntity(Loc.GetString(message), caster, caster, PopupType.MediumCaution);

        if (gust.Sound != null)
            _audio.PlayPvs(gust.Sound, caster);

        var coords = Transform(caster).Coordinates;
        foreach (var proto in gust.SpawnAtCaster)
            Spawn(proto, coords);

        if (gust.SwapWithNearby)
            TrySwap(caster, gust.SwapRange);
    }

    private void TrySwap(EntityUid caster, float range)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(Transform(caster).Coordinates, range, _nearby);
        _nearby.RemoveWhere(e => e.Owner == caster);
        if (_nearby.Count == 0)
            return;

        var other = _random.Pick(_nearby.ToList());
        _xform.SwapPositions((caster, Transform(caster)), (other.Owner, Transform(other.Owner)));
    }
}
