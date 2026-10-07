using Content.Server._Starlight.Wizard.SpellGraph;
using Content.Shared.Trigger;

namespace Content.Server._Starlight.Wizard.Casting;

/// <summary>
/// Makes bolts and runes take effect. Both ride on the existing trigger-on-collide pipeline:
/// a bolt takes effect when it hits something or reaches the spot the caster aimed at, a rune when something steps on it.
/// </summary>
public sealed partial class SpellCarrierSystem : EntitySystem
{
    [Dependency] private SpellGraphSystem _spells = default!;

    private const float ArrivalDistance = 0.6f;

    [SubscribeLocalEvent]
    private void OnBoltTrigger(Entity<SpellBoltComponent> ent, ref TriggerEvent args)
    {
        // The caster never hits their own bolt as it leaves their hand.
        if (ent.Comp.Spent || args.User == ent.Comp.Caster)
            return;

        DetonateBolt(ent, args.User);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnRuneTrigger(Entity<SpellRuneComponent> ent, ref TriggerEvent args)
    {
        // Runes never trigger on their own caster.
        if (ent.Comp.Spent || args.User == null || args.User == ent.Comp.Caster)
            return;

        ent.Comp.Spent = true;
        _spells.Detonate(ent.Comp.Plan, ent.Comp.Caster, Transform(ent).Coordinates, args.User);
        QueueDel(ent);
        args.Handled = true;
    }

    private void DetonateBolt(Entity<SpellBoltComponent> ent, EntityUid? target)
    {
        ent.Comp.Spent = true;
        _spells.Detonate(ent.Comp.Plan, ent.Comp.Caster, Transform(ent).Coordinates, target);
        QueueDel(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // A bolt that reaches the spot it was aimed at takes effect there, even if it never hit anything.
        var query = EntityQueryEnumerator<SpellBoltComponent>();
        while (query.MoveNext(out var uid, out var bolt))
        {
            if (bolt.Spent || TerminatingOrDeleted(uid))
                continue;

            if (!Transform(uid).Coordinates.TryDistance(EntityManager, bolt.Aim, out var distance))
                continue;

            // Fast bolts can skip past the spot between ticks, so also detonate once the distance starts growing again.
            var passed = distance > bolt.LastDistance + 0.01f && bolt.LastDistance < (ArrivalDistance * 4f);
            bolt.LastDistance = distance;

            if (distance <= ArrivalDistance || passed)
                DetonateBolt((uid, bolt), null);
        }
    }
}
