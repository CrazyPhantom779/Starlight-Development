using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server._Starlight.Wizard.Casting;
using Content.Shared._Starlight.Wizard.Casting;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Actions;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SpellGraphData = Content.Shared._Starlight.Wizard.SpellGraph.SpellGraph;

namespace Content.Server._Starlight.Wizard.SpellGraph;

/// <summary>
/// Compiles woven spells, turns them into actions, and runs them when cast.
/// A cast is: pay reagents, deliver the spell according to its Form (aimed, self, bolt, touch, burst, rune),
/// and run each step's effect at the point it lands. Other systems (wands, enchanted items, rituals) call
/// <see cref="TryCastPlan"/> directly, so every discipline shares one casting path.
/// </summary>
public sealed partial class SpellGraphSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private GlyphEffectSystem _effects = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private static readonly EntProtoId _worldActionProto = "ActionSpellGraphWorld";
    private static readonly EntProtoId _instantActionProto = "ActionSpellGraphInstant";

    // Used when the caster has no SpellcraftComponent (admin/debug use).
    private const int DefaultMaxNodes = 6;
    private const int DefaultMaxSpells = 8;

    // Bonuses from how a spell was made.
    private const int CircuitExtraNodes = 2;
    private const float SigilCostFactor = 0.9f;
    private const float FanSpreadDegrees = 12f;
    private const float MaxRuneRange = 7f;
    private const float ChainRange = 6f;

    private readonly HashSet<Entity<MobStateComponent>> _nearby = [];

    // ---------------------------------------------------------------- compiling

    /// <summary>
    /// Compiles a graph for a caster, applying what they know, what they are attuned to, and how the spell is made.
    /// </summary>
    public bool TryCompile(EntityUid caster,
        SpellGraphData graph,
        SpellDiscipline discipline,
        bool ignoreKnown,
        [NotNullWhen(true)] out SpellGraphPlan? plan,
        [NotNullWhen(false)] out string? error)
    {
        var maxNodes = DefaultMaxNodes;
        IReadOnlyCollection<ProtoId<SpellGlyphPrototype>>? known = null;
        IReadOnlyCollection<string>? affinities = null;

        if (TryComp<SpellcraftComponent>(caster, out var craft))
        {
            maxNodes = craft.MaxNodes;
            affinities = craft.Schools;
            if (!craft.Unrestricted && !ignoreKnown)
                known = craft.Glyphs;
        }

        if (discipline == SpellDiscipline.Circuit)
            maxNodes += CircuitExtraNodes;

        if (ignoreKnown)
            maxNodes = Math.Max(maxNodes, 16);

        if (!SpellGraphCompiler.TryCompile(_proto, graph, maxNodes, known, affinities, out plan, out error))
            return false;

        if (discipline == SpellDiscipline.Sigil)
            plan.Cost = MathF.Max(1f, MathF.Round(plan.Cost * SigilCostFactor, 1));

        return true;
    }

    /// <summary>Whether a plan can be stored in an item (wand, enchanted object, card) rather than only cast as an action.</summary>
    public static bool IsStorable(SpellGraphPlan plan)
    {
        foreach (var step in plan.Steps)
        {
            if (step.Glyph.Effects.Count == 0)
                return false;
        }

        return true;
    }

    // ---------------------------------------------------------------- creating action spells

    public bool TryCreateSpell(EntityUid performer,
        SpellGraphData graph,
        [NotNullWhen(true)] out EntityUid? action,
        [NotNullWhen(false)] out string? error)
        => TryCreateSpell(performer, graph, SpellDiscipline.Glyphwork, ignoreKnown: false, out action, out error);

    /// <summary>
    /// Validates the graph against what the performer is allowed to use, and if valid gives them a new action.
    /// </summary>
    public bool TryCreateSpell(EntityUid performer,
        SpellGraphData graph,
        SpellDiscipline discipline,
        bool ignoreKnown,
        [NotNullWhen(true)] out EntityUid? action,
        [NotNullWhen(false)] out string? error)
    {
        action = null;
        if (!TryCompile(performer, graph, discipline, ignoreKnown, out var plan, out error))
            return false;

        var maxSpells = TryComp<SpellcraftComponent>(performer, out var craft) ? craft.MaxSpells : DefaultMaxSpells;
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
        _actions.SetUseDelay(actionId.Value, TimeSpan.FromSeconds(Math.Clamp(plan.Cost * 0.1f * plan.CooldownMultiplier, 1f, 20f)));

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

    // ---------------------------------------------------------------- casting from actions

    [SubscribeLocalEvent]
    private void OnWorldCast(SpellGraphWorldEvent ev)
    {
        if (ev.Handled || GetPlan(ev.Action.Owner) is not { } plan)
            return;

        ev.Handled = TryCastPlan(plan, ev.Performer, ev.Target, ev.Entity, ev.Action.Owner);
    }

    [SubscribeLocalEvent]
    private void OnInstantCast(SpellGraphInstantEvent ev)
    {
        if (ev.Handled || GetPlan(ev.Action.Owner) is not { } plan)
            return;

        ev.Handled = TryCastPlan(plan, ev.Performer, default, null, ev.Action.Owner);
    }

    // ---------------------------------------------------------------- the shared casting path

    /// <summary>
    /// Casts a plan. Returns false (and tells the caster why, where it can) if it could not be cast;
    /// in that case nothing was spent. Paying Wind is the caller's job.
    /// </summary>
    public bool TryCastPlan(SpellGraphPlan plan,
        EntityUid caster,
        EntityCoordinates target,
        EntityUid? targetEntity,
        EntityUid? action = null)
    {
        if (TerminatingOrDeleted(caster))
            return false;

        if (!TryFindReagents(plan, caster, out var reagents))
            return false;

        var ctx = new SpellCastContext
        {
            Caster = caster,
            Action = action,
            Target = targetEntity,
            Point = target,
        };

        var cast = false;
        switch (plan.Delivery)
        {
            case SpellDelivery.Aimed:
                RunPlan(plan, ctx);
                cast = true;
                break;
            case SpellDelivery.Self:
                ctx.Point = Transform(caster).Coordinates;
                ctx.Target = caster;
                RunPlan(plan, ctx);
                cast = true;
                break;
            case SpellDelivery.Burst:
                ctx.Point = Transform(caster).Coordinates;
                ctx.Target = caster;
                ctx.FormRadius = plan.Form.Radius;
                RunPlan(plan, ctx);
                cast = true;
                break;
            case SpellDelivery.Touch:
                if (PrepareTouch(plan, ctx))
                {
                    RunPlan(plan, ctx);
                    cast = true;
                }

                break;
            case SpellDelivery.Bolt:
                cast = FireBolts(plan, ctx);
                break;
            case SpellDelivery.Rune:
                cast = PlaceRune(plan, ctx);
                break;
        }

        if (cast)
        {
            foreach (var item in reagents)
                QueueDel(item);
        }

        return cast;
    }

    /// <summary>Runs every step of a plan at a context, honouring repeats and delays.</summary>
    public void RunPlan(SpellGraphPlan plan, SpellCastContext ctx)
    {
        foreach (var step in plan.Steps)
        {
            for (var i = 0; i <= step.Repeats; i++)
            {
                var delay = step.Delay + (i * step.RepeatInterval);
                var current = step;
                if (delay <= 0f)
                {
                    RunStep(current, ctx);
                    continue;
                }

                Timer.Spawn(TimeSpan.FromSeconds(delay), () => RunStep(current, ctx));
            }
        }
    }

    private void RunStep(SpellStep step, SpellCastContext ctx)
    {
        // Delayed steps can outlive the caster or the spell.
        if (TerminatingOrDeleted(ctx.Caster) || (ctx.Action is { } action && TerminatingOrDeleted(action)))
            return;

        _effects.Apply(step, ctx);

        var last = ctx;
        for (var i = 0; i < step.ChainJumps; i++)
        {
            if (FindChainTarget(last) is not { } next)
                break;

            var jump = new SpellCastContext
            {
                Caster = ctx.Caster,
                Action = ctx.Action,
                Target = next,
                Point = Transform(next).Coordinates,
                Hit = ctx.Hit,
            };

            ctx.Hit.Add(next);
            _effects.Apply(step, jump);
            last = jump;
        }
    }

    private EntityUid? FindChainTarget(SpellCastContext from)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(from.Point, ChainRange, _nearby);

        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var creature in _nearby)
        {
            if (creature.Owner == from.Caster
                || from.Hit.Contains(creature.Owner)
                || _mobState.IsDead(creature.Owner, creature.Comp))
                continue;

            if (!from.Point.TryDistance(EntityManager, Transform(creature.Owner).Coordinates, out var distance)
                || distance >= bestDistance)
                continue;

            best = creature.Owner;
            bestDistance = distance;
        }

        return best;
    }

    // ---------------------------------------------------------------- reagents

    private bool TryFindReagents(SpellGraphPlan plan, EntityUid caster, out List<EntityUid> items)
    {
        items = [];
        foreach (var glyph in plan.Reagents)
        {
            EntityUid? found = null;
            foreach (var held in _hands.EnumerateHeld(caster))
            {
                if (items.Contains(held) || !_whitelist.IsValid(glyph.Reagent!, held))
                    continue;

                found = held;
                break;
            }

            if (found is not { } reagent)
            {
                _popup.PopupEntity(Loc.GetString("spellcraft-error-reagent",
                        ("reagent", glyph.ReagentName is { } name ? Loc.GetString(name) : Loc.GetString(glyph.Name))),
                    caster,
                    caster);
                return false;
            }

            items.Add(reagent);
        }

        return true;
    }

    // ---------------------------------------------------------------- forms

    private bool PrepareTouch(SpellGraphPlan plan, SpellCastContext ctx)
    {
        var range = plan.Form.Range;

        if (ctx.Target is { } target)
        {
            if (!_interaction.InRangeUnobstructed(ctx.Caster, target, range))
            {
                _popup.PopupEntity(Loc.GetString("spellcraft-error-too-far"), ctx.Caster, ctx.Caster);
                return false;
            }

            ctx.Point = Transform(target).Coordinates;
            return true;
        }

        if (!ctx.Point.TryDistance(EntityManager, Transform(ctx.Caster).Coordinates, out var distance) || distance > range)
        {
            _popup.PopupEntity(Loc.GetString("spellcraft-error-too-far"), ctx.Caster, ctx.Caster);
            return false;
        }

        return true;
    }

    private bool FireBolts(SpellGraphPlan plan, SpellCastContext ctx)
    {
        if (plan.Form.Projectile is not { } projectile)
            return false;

        var caster = ctx.Caster;
        var from = _xform.ToMapCoordinates(Transform(caster).Coordinates);
        var aim = _xform.ToMapCoordinates(ctx.Point);
        var baseDirection = aim.Position - from.Position;
        if (baseDirection.LengthSquared() < 0.01f)
            baseDirection = _xform.GetWorldRotation(caster).ToWorldVec();

        var baseAngle = baseDirection.ToWorldAngle();
        var count = Math.Max(1, plan.ProjectileCount);
        var distance = baseDirection.Length();

        for (var i = 0; i < count; i++)
        {
            var offset = MathHelper.DegreesToRadians((i - ((count - 1) / 2f)) * FanSpreadDegrees);
            var direction = (baseAngle + offset).ToWorldVec();

            var bolt = Spawn(projectile, from);
            var carrier = EnsureComp<SpellBoltComponent>(bolt);
            carrier.Plan = plan;
            carrier.Caster = caster;
            carrier.Aim = _xform.ToCoordinates(new MapCoordinates(from.Position + (direction * distance), from.MapId));

            _gun.ShootProjectile(bolt, direction, Vector2.Zero, caster, caster, plan.Form.ProjectileSpeed);
        }

        return true;
    }

    private bool PlaceRune(SpellGraphPlan plan, SpellCastContext ctx)
    {
        if (plan.Form.Rune is not { } proto)
            return false;

        if (!ctx.Point.TryDistance(EntityManager, Transform(ctx.Caster).Coordinates, out var distance) || distance > MaxRuneRange)
        {
            _popup.PopupEntity(Loc.GetString("spellcraft-error-too-far"), ctx.Caster, ctx.Caster);
            return false;
        }

        var rune = Spawn(proto, ctx.Point.SnapToGrid(EntityManager));
        var carrier = EnsureComp<SpellRuneComponent>(rune);
        carrier.Plan = plan;
        carrier.Caster = ctx.Caster;
        return true;
    }

    /// <summary>Takes effect where a bolt landed or a rune was stepped on.</summary>
    public void Detonate(SpellGraphPlan plan, EntityUid caster, EntityCoordinates point, EntityUid? target)
    {
        var ctx = new SpellCastContext
        {
            Caster = caster,
            Point = point,
            Target = target,
            FromProjectile = true,
        };
        RunPlan(plan, ctx);
    }
}
