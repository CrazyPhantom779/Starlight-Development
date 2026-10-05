using System.Diagnostics.CodeAnalysis;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SpellGraphData = Content.Shared._Starlight.Wizard.SpellGraph.SpellGraph;

namespace Content.Server._Starlight.Wizard.SpellGraph;

/// <summary>
/// Creates woven spells from <see cref="SpellGraph"/>s and runs them when cast.
/// Effects are executed by raising the wrapped action events, so every existing spell handler keeps working.
/// </summary>
public sealed partial class SpellGraphSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    private static readonly EntProtoId _worldActionProto = "ActionSpellGraphWorld";
    private static readonly EntProtoId _instantActionProto = "ActionSpellGraphInstant";

    // Used when the caster has no SpellcraftComponent (admin/debug use).
    private const int DefaultMaxNodes = 6;
    private const int DefaultMaxSpells = 8;

    /// <summary>
    /// Validates the graph against what the performer is allowed to use, and if valid gives them a new action.
    /// </summary>
    public bool TryCreateSpell(EntityUid performer,
        SpellGraphData graph,
        [NotNullWhen(true)] out EntityUid? action,
        [NotNullWhen(false)] out string? error)
    {
        action = null;

        var maxNodes = DefaultMaxNodes;
        var maxSpells = DefaultMaxSpells;
        IReadOnlyCollection<ProtoId<SpellGlyphPrototype>>? known = null;
        if (TryComp<SpellcraftComponent>(performer, out var craft))
        {
            maxNodes = craft.MaxNodes;
            maxSpells = craft.MaxSpells;
            if (!craft.Unrestricted)
                known = craft.Glyphs;
        }

        if (!SpellGraphCompiler.TryCompile(_proto, graph, maxNodes, known, out var plan, out error))
            return false;

        var held = 0;
        foreach (var existing in _actions.GetActions(performer))
        {
            if (HasComp<SpellGraphActionComponent>(existing))
                held++;
        }

        if (held >= maxSpells)
        {
            error = Loc.GetString("spellcraft-error-too-many-spells");
            return false;
        }

        EntityUid? actionId = null;
        EntProtoId protoId = plan.TargetMode == SpellTargetMode.World ? _worldActionProto : _instantActionProto;
        if (!_actions.AddAction(performer, ref actionId, protoId))
        {
            error = Loc.GetString("spellcraft-error-no-actions");
            return false;
        }

        var graphComp = EnsureComp<SpellGraphActionComponent>(actionId.Value);
        graphComp.Graph = graph;
        graphComp.Plan = plan;

        var cost = EnsureComp<WindCostComponent>(actionId.Value);
        cost.Cost = plan.Cost;
        Dirty(actionId.Value, cost);

        _meta.SetEntityName(actionId.Value, plan.Name);
        _meta.SetEntityDescription(actionId.Value, Loc.GetString("spellcraft-action-description", ("cost", plan.Cost)));

        // Bigger spells take longer to recover from.
        _actions.SetUseDelay(actionId.Value, TimeSpan.FromSeconds(Math.Clamp(plan.Cost * 0.1f, 1f, 20f)));

        action = actionId;
        error = null;
        return true;
    }

    private SpellGraphPlan? GetPlan(EntityUid action)
    {
        if (!TryComp<SpellGraphActionComponent>(action, out var comp))
            return null;

        if (comp.Plan != null)
            return comp.Plan;

        if (!SpellGraphCompiler.TryCompile(_proto, comp.Graph, int.MaxValue, null, out var plan, out _))
            return null;

        comp.Plan = plan;
        return plan;
    }

    [SubscribeLocalEvent]
    private void OnWorldCast(SpellGraphWorldEvent ev)
    {
        if (ev.Handled || GetPlan(ev.Action.Owner) is not { } plan)
            return;

        ev.Handled = true;
        Cast(plan, ev.Performer, ev.Action, ev.Target, ev.Entity, instant: false);
    }

    [SubscribeLocalEvent]
    private void OnInstantCast(SpellGraphInstantEvent ev)
    {
        if (ev.Handled || GetPlan(ev.Action.Owner) is not { } plan)
            return;

        ev.Handled = true;
        Cast(plan, ev.Performer, ev.Action, default, null, instant: true);
    }

    private void Cast(SpellGraphPlan plan,
        EntityUid performer,
        Entity<ActionComponent> action,
        EntityCoordinates target,
        EntityUid? targetEntity,
        bool instant)
    {
        foreach (var step in plan.Steps)
        {
            for (var i = 0; i <= step.Repeats; i++)
            {
                var delay = step.Delay + (i * SpellGraphCompiler.RepeatIntervalSeconds);
                var glyph = step.Glyph;
                if (delay <= 0f)
                {
                    Execute(glyph, performer, action, target, targetEntity, instant);
                    continue;
                }

                Timer.Spawn(TimeSpan.FromSeconds(delay),
                    () => Execute(glyph, performer, action, target, targetEntity, instant));
            }
        }
    }

    private void Execute(SpellGlyphPrototype glyph,
        EntityUid performer,
        Entity<ActionComponent> action,
        EntityCoordinates target,
        EntityUid? targetEntity,
        bool instant)
    {
        // Delayed steps can outlive the caster or the spell.
        if (TerminatingOrDeleted(performer) || TerminatingOrDeleted(action.Owner))
            return;

        // Wrapped events are shared prototype data; refill every field right before raising, as PerformAction does.
        if (instant)
        {
            if (glyph.InstantEvent is not { } ev)
                return;

            ev.Performer = performer;
            ev.Action = action;
            ev.Handled = false;
            RaiseLocalEvent(performer, (object) ev, broadcast: true);
        }
        else
        {
            if (glyph.WorldEvent is not { } ev)
                return;

            ev.Performer = performer;
            ev.Action = action;
            ev.Target = target;
            ev.Entity = targetEntity;
            ev.Handled = false;
            RaiseLocalEvent(performer, (object) ev, broadcast: true);
        }
    }
}
