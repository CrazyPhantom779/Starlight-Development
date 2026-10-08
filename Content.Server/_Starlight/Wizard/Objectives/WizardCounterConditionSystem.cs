using System.Linq;
using Content.Server._Starlight.Wizard.Casting;
using Content.Server.Objectives.Systems;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Magic.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server._Starlight.Wizard.Objectives;

/// <summary>Counts wizard activity for <see cref="WizardCounterConditionComponent"/>.</summary>
public sealed partial class WizardCounterConditionSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private NumberObjectiveSystem _number = default!;

    /// <summary>Spells from the old spellbook and other prepared actions. Woven spells are counted by <see cref="OnCast"/>.</summary>
    [SubscribeLocalEvent]
    private void OnActionPerformed(Entity<ActionComponent> ent, ref ActionPerformedEvent args)
    {
        if (HasComp<SpellGraphActionComponent>(ent) || !HasComp<MagicComponent>(ent))
            return;

        var key = MetaData(ent).EntityPrototype?.ID ?? MetaData(ent).EntityName;
        Count(args.Performer, WizardCounterKind.Cast);
        CountOnce(args.Performer, WizardCounterKind.CastDistinct, key);
    }

    [SubscribeLocalEvent]
    private void OnCast(Entity<SpellcraftComponent> ent, ref SpellCastEvent args)
    {
        Count(ent, WizardCounterKind.Cast);
        Count(ent, WizardCounterKind.CastWoven);
        CountOnce(ent, WizardCounterKind.CastDistinct, "woven:" + string.Join(",", args.Plan.Steps.Select(s => s.Glyph.ID).OrderBy(id => id)));

        foreach (var school in args.Plan.Schools)
            CountOnce(ent, WizardCounterKind.Schools, school);
    }

    [SubscribeLocalEvent]
    private void OnHit(Entity<SpellcraftComponent> ent, ref SpellHitEvent args)
    {
        foreach (var creature in args.Hit)
            CountOnce(ent, WizardCounterKind.Creatures, creature.ToString());
    }

    [SubscribeLocalEvent]
    private void OnWindOverdrawn(ref WindOverdrawnEvent args)
        => Count(args.Performer, WizardCounterKind.Overcast);

    [SubscribeLocalEvent]
    private void OnRite(Entity<SpellcraftComponent> ent, ref RitePerformedEvent args)
        => Count(ent, WizardCounterKind.Rites);

    [SubscribeLocalEvent]
    private void OnStored(Entity<SpellcraftComponent> ent, ref SpellStoredEvent args)
        => Count(ent, WizardCounterKind.Stored);

    [SubscribeLocalEvent]
    private void OnItemUsed(Entity<SpellcraftComponent> ent, ref SpellItemUsedEvent args)
        => Count(ent, args.IsCard ? WizardCounterKind.Cards : WizardCounterKind.Scrolls);

    [SubscribeLocalEvent]
    private void OnErrand(Entity<SpellcraftComponent> ent, ref ErrandCompletedEvent args)
        => Count(ent, WizardCounterKind.Errands);

    [SubscribeLocalEvent]
    private void OnGetProgress(Entity<WizardCounterConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var target = Math.Max(1, _number.GetTarget(ent));

        if (ent.Comp.Kind == WizardCounterKind.MaxWind)
        {
            var max = args.Mind.OwnedEntity is { } body && TryComp<WindComponent>(body, out var wind) ? wind.Max : 0f;
            args.Progress = Math.Min(1f, max / target);
            return;
        }

        args.Progress = Math.Min(1f, ent.Comp.Count / (float) target);
    }

    private void Count(EntityUid performer, WizardCounterKind kind)
        => ForEachCounter(performer, kind, comp => comp.Count++);

    private void CountOnce(EntityUid performer, WizardCounterKind kind, string key)
        => ForEachCounter(performer, kind, comp =>
        {
            if (comp.Seen.Add(key))
                comp.Count++;
        });

    private void ForEachCounter(EntityUid performer, WizardCounterKind kind, Action<WizardCounterConditionComponent> apply)
    {
        if (!_mind.TryGetMind(performer, out _, out var mind))
            return;

        foreach (var objective in mind.Objectives)
        {
            if (TryComp<WizardCounterConditionComponent>(objective, out var counter) && counter.Kind == kind)
                apply(counter);
        }
    }
}
