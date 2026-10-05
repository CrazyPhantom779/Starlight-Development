using System.Linq;
using Content.Server.Objectives.Systems;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Magic.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server._Starlight.Wizard.Objectives;

/// <summary>Counts wizard spell casts and overcasts for <see cref="WizardCounterConditionComponent"/>.</summary>
public sealed partial class WizardCounterConditionSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private NumberObjectiveSystem _number = default!;

    [SubscribeLocalEvent]
    private void OnActionPerformed(Entity<ActionComponent> ent, ref ActionPerformedEvent args)
    {
        var woven = HasComp<SpellGraphActionComponent>(ent);
        if (!woven && !HasComp<MagicComponent>(ent))
            return;

        var key = woven ? "woven:" + SignatureOf(ent) : (MetaData(ent).EntityPrototype?.ID ?? MetaData(ent).EntityName);

        ForEachCounter(args.Performer, (comp) =>
        {
            switch (comp.Kind)
            {
                case WizardCounterKind.Cast:
                    comp.Count++;
                    break;
                case WizardCounterKind.CastWoven:
                    if (woven)
                        comp.Count++;
                    break;
                case WizardCounterKind.CastDistinct:
                    if (comp.Seen.Add(key))
                        comp.Count++;
                    break;
            }
        });
    }

    [SubscribeLocalEvent]
    private void OnOverdrawn(ref WindOverdrawnEvent args)
        => ForEachCounter(args.Performer, comp =>
            {
            if (comp.Kind == WizardCounterKind.Overcast)
                comp.Count++;
        });

    [SubscribeLocalEvent]
    private void OnGetProgress(Entity<WizardCounterConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var target = Math.Max(1, _number.GetTarget(ent));
        args.Progress = Math.Min(1f, ent.Comp.Count / (float) target);
    }

    private void ForEachCounter(EntityUid performer, Action<WizardCounterConditionComponent> apply)
    {
        if (!_mind.TryGetMind(performer, out _, out var mind))
            return;

        foreach (var objective in mind.Objectives)
        {
            if (TryComp<WizardCounterConditionComponent>(objective, out var counter))
                apply(counter);
        }
    }

    private string SignatureOf(EntityUid action)
    {
        if (!TryComp<SpellGraphActionComponent>(action, out var woven))
            return string.Empty;

        return string.Join(",", woven.Graph.Nodes.Select(n => n.Glyph.Id).OrderBy(id => id));
    }
}
