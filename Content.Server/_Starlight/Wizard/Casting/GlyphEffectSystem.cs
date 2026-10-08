using System.Numerics;
using System.Linq;
using Content.Server._Starlight.Wizard.Wind;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Lightning;
using Content.Server.Polymorph.Systems;
using Content.Shared._Starlight.Wizard.Casting;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Coordinates.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server._Starlight.Wizard.Casting;

/// <summary>
/// Carries out the effect of one spell step at a point in the world.
/// Universal effects (damage, spawn, push, ...) work with every Form. Legacy glyphs raise the wrapped spell event.
/// </summary>
public sealed partial class GlyphEffectSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private WindSystem _wind = default!;
    [Dependency] private LightningSystem _lightning = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private MovementModStatusSystem _movement = default!;

    private static readonly ProtoId<DamageTypePrototype> _blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> _heat = "Heat";

    private readonly HashSet<Entity<MobStateComponent>> _creatures = [];
    private readonly HashSet<EntityUid> _bodies = [];

    /// <summary>Carries out a step. Returns false if it could not take effect at all.</summary>
    public bool Apply(SpellStep step, SpellCastContext ctx)
    {
        var glyph = step.Glyph;
        if (glyph.Effects.Count == 0)
            return ApplyLegacy(glyph, ctx);

        foreach (var effect in glyph.Effects)
        {
            switch (effect)
            {
                case DamageGlyphEffect e: Damage(e, step, ctx); break;
                case HealGlyphEffect e: Heal(e, step, ctx); break;
                case IgniteGlyphEffect e: Ignite(e, step, ctx); break;
                case StunGlyphEffect e: Stun(e, step, ctx); break;
                case PushGlyphEffect e: Push(e, step, ctx); break;
                case SpawnGlyphEffect e: SpawnThings(e, step, ctx); break;
                case TeleportGlyphEffect e: Teleport(e, step, ctx); break;
                case SwapGlyphEffect e: Swap(e, step, ctx); break;
                case DrainGlyphEffect e: Drain(e, step, ctx); break;
                case WindGlyphEffect e: MoveWind(e, step, ctx); break;
                case ExplodeGlyphEffect e: Explode(e, step, ctx); break;
                case LightningGlyphEffect e: Lightning(e, step, ctx); break;
                case PolymorphGlyphEffect e: Polymorph(e, step, ctx); break;
                case LeapGlyphEffect e: Leap(e, step, ctx); break;
                case SlowGlyphEffect e: Slow(e, step, ctx); break;
                case HasteGlyphEffect e: Haste(e, step, ctx); break;
            }
        }

        return true;
    }

    private bool ApplyLegacy(SpellGlyphPrototype glyph, SpellCastContext ctx)
    {
        // Wrapped events need a real action entity to hang their prerequisite checks on.
        if (ctx.Action is not { } actionUid || !TryComp<ActionComponent>(actionUid, out var action))
            return false;

        var actionEnt = new Entity<ActionComponent>(actionUid, action);

        // The wrapped events are shared prototype data; refill every field right before raising, as PerformAction does.
        if (glyph.InstantEvent is { } instant && glyph.WorldEvent == null)
        {
            instant.Performer = ctx.Caster;
            instant.Action = actionEnt;
            instant.Handled = false;
            RaiseLocalEvent(ctx.Caster, (object) instant, broadcast: true);
            return instant.Handled;
        }

        if (glyph.WorldEvent is { } world)
        {
            world.Performer = ctx.Caster;
            world.Action = actionEnt;
            world.Target = ctx.Point;
            world.Entity = ctx.Target;
            world.Handled = false;
            RaiseLocalEvent(ctx.Caster, (object) world, broadcast: true);
            return world.Handled;
        }

        return false;
    }

    /// <summary>The step's strength this time: as woven, plus the caster's random variation.</summary>
    private static float Strength(SpellStep step, SpellCastContext ctx)
        => step.Magnitude * ctx.Jitter;

    private float Area(float radius, SpellStep step, SpellCastContext ctx)
        => radius + step.RadiusBonus + ctx.FormRadius;

    private void FindCreatures(SpellCastContext ctx, float radius, bool includeCaster, bool includeDead = false)
    {
        _creatures.Clear();
        _lookup.GetEntitiesInRange(ctx.Point, radius, _creatures);

        if (!includeCaster)
            _creatures.RemoveWhere(e => e.Owner == ctx.Caster);

        if (!includeDead)
            _creatures.RemoveWhere(e => _mobState.IsDead(e.Owner, e.Comp));

        if (ctx.DedupeHits)
            _creatures.RemoveWhere(e => ctx.Hit.Contains(e.Owner));
    }

    private void Damage(DamageGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        FindCreatures(ctx, Area(e.Radius, step, ctx), e.AffectCaster);
        var damage = e.Damage * Strength(step, ctx);
        foreach (var creature in _creatures.ToList())
        {
            _damageable.TryChangeDamage(creature.Owner, damage, e.IgnoreResistances, origin: ctx.Caster);
            ctx.Hit.Add(creature.Owner);
        }
    }

    private void Heal(HealGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        FindCreatures(ctx, Area(e.Radius, step, ctx), e.AffectCaster, includeDead: true);
        var heal = e.Heal * Strength(step, ctx);
        foreach (var creature in _creatures.ToList())
        {
            _damageable.TryChangeDamage(creature.Owner, heal, ignoreResistances: true, origin: ctx.Caster);
            ctx.Hit.Add(creature.Owner);
        }
    }

    private void Ignite(IgniteGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        FindCreatures(ctx, Area(e.Radius, step, ctx), e.AffectCaster);
        foreach (var creature in _creatures.ToList())
        {
            _flammable.AdjustFireStacks(creature.Owner, e.FireStacks * Strength(step, ctx), ignite: true);
            ctx.Hit.Add(creature.Owner);
        }
    }

    private void Stun(StunGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        FindCreatures(ctx, Area(e.Radius, step, ctx), e.AffectCaster);
        var time = TimeSpan.FromSeconds(e.Seconds * Strength(step, ctx));
        foreach (var creature in _creatures.ToList())
        {
            if (e.KnockdownOnly)
                _stun.TryKnockdown(creature.Owner, time, force: true);
            else
                _stun.TryAddParalyzeDuration(creature.Owner, time);

            ctx.Hit.Add(creature.Owner);
        }
    }

    private void Push(PushGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        var radius = Area(e.Radius, step, ctx);
        var map = _xform.ToMapCoordinates(ctx.Point);
        var origin = map.Position;

        _bodies.Clear();
        _lookup.GetEntitiesInRange(map.MapId, origin, radius, _bodies, flags: LookupFlags.Dynamic | LookupFlags.Sundries);

        foreach (var target in _bodies.ToList())
        {
            if (target == ctx.Caster && !e.AffectCaster)
                continue;

            if (!HasComp<MobStateComponent>(target) && !HasComp<ItemComponent>(target))
                continue;

            var direction = _xform.GetWorldPosition(target) - origin;
            if (direction == Vector2.Zero)
                continue;

            var throwDirection = e.Pull
                ? -direction
                : direction.Normalized() * Math.Max(1f, radius - direction.Length());

            _throwing.TryThrow(target, throwDirection, e.Strength * Strength(step, ctx), ctx.Caster, recoil: false, compensateFriction: true);
        }
    }

    private void SpawnThings(SpawnGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        var amount = Math.Max(1, (int) MathF.Round(e.Amount * Strength(step, ctx)));
        var scatter = e.Scatter + (step.RadiusBonus * 0.5f);

        for (var i = 0; i < amount; i++)
        {
            var position = e.InHand ? Transform(ctx.Caster).Coordinates : ctx.Point;
            if (scatter > 0f)
                position = position.Offset(new Vector2(_random.NextFloat(-scatter, scatter), _random.NextFloat(-scatter, scatter)));

            if (e.SnapToGrid)
                position = position.SnapToGrid(EntityManager);

            foreach (var proto in e.Prototypes)
            {
                var spawned = Spawn(proto, position);

                if (e.Lifetime is { } lifetime)
                    EnsureComp<TimedDespawnComponent>(spawned).Lifetime = lifetime;

                if (e.PreventCollideWithCaster)
                    EnsureComp<PreventCollideComponent>(spawned).Uid = ctx.Caster;

                if (e.InHand)
                    _hands.TryPickupAnyHand(ctx.Caster, spawned);
            }
        }
    }

    private void Teleport(TeleportGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        var caster = ctx.Caster;
        var xform = Transform(caster);
        var destination = ctx.Point;
        var maxDistance = e.MaxDistance + (step.RadiusBonus * 2f);

        if (xform.MapID != _xform.GetMapId(destination))
            return;

        // A bolt that stopped at a wall lands inside it. Step back toward the caster so we arrive on the near side.
        if (ctx.FromProjectile && destination.TryDistance(EntityManager, xform.Coordinates, out var gap) && gap > 1f)
        {
            var toCaster = _xform.GetWorldPosition(caster) - _xform.ToMapCoordinates(destination).Position;
            destination = destination.Offset(toCaster.Normalized() * 0.7f);
        }

        if (!destination.TryDistance(EntityManager, xform.Coordinates, out var distance) || distance > maxDistance)
            return;

        if (!_interaction.InRangeUnobstructed(caster, destination, range: maxDistance + 2f, collisionMask: CollisionGroup.Opaque, popup: false))
            return;

        _xform.SetCoordinates(caster, destination);
        _xform.AttachToGridOrMap(caster, xform);
    }

    private void Swap(SwapGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        FindCreatures(ctx, Area(e.Radius, step, ctx), includeCaster: false, includeDead: true);

        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var creature in _creatures)
        {
            if (!ctx.Point.TryDistance(EntityManager, Transform(creature.Owner).Coordinates, out var distance) || distance >= bestDistance)
                continue;

            best = creature.Owner;
            bestDistance = distance;
        }

        if (best is not { } other)
            return;

        _xform.SwapPositions((ctx.Caster, Transform(ctx.Caster)), (other, Transform(other)));
        ctx.Hit.Add(other);
    }

    private void Drain(DrainGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        FindCreatures(ctx, Area(e.Radius, step, ctx), includeCaster: false);
        var damage = e.Damage * Strength(step, ctx);
        var dealt = FixedPoint2.Zero;

        foreach (var creature in _creatures.ToList())
        {
            if (_damageable.TryChangeDamage(creature.Owner, damage, out var applied, origin: ctx.Caster))
                dealt += applied.GetTotal();

            ctx.Hit.Add(creature.Owner);
        }

        var healAmount = dealt.Float() * e.HealFraction;
        if (healAmount <= 0f)
            return;

        var heal = new DamageSpecifier();
        heal.DamageDict[_blunt] = FixedPoint2.New(-healAmount / 2f);
        heal.DamageDict[_heat] = FixedPoint2.New(-healAmount / 2f);
        _damageable.TryChangeDamage(ctx.Caster, heal, ignoreResistances: true, origin: ctx.Caster);
    }

    private void MoveWind(WindGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        var amount = e.Amount * Strength(step, ctx);
        FindCreatures(ctx, Area(e.Radius, step, ctx), includeCaster: amount > 0f && e.AffectCaster, includeDead: true);

        foreach (var creature in _creatures.ToList())
        {
            if (!TryComp<WindComponent>(creature.Owner, out var wind))
                continue;

            var holder = new Entity<WindComponent>(creature.Owner, wind);
            if (amount >= 0f)
            {
                _wind.AddWind(holder, amount);
                continue;
            }

            var taken = MathF.Min(-amount, MathF.Max(0f, _wind.GetWind(holder)));
            _wind.AddWind(holder, -taken);

            if (e.ToCaster && TryComp<WindComponent>(ctx.Caster, out var casterWind))
                _wind.AddWind((ctx.Caster, casterWind), taken);

            ctx.Hit.Add(creature.Owner);
        }
    }

    private void Explode(ExplodeGlyphEffect e, SpellStep step, SpellCastContext ctx)
        => _explosion.QueueExplosion(_xform.ToMapCoordinates(ctx.Point),
            e.ExplosionType,
            e.TotalIntensity * Strength(step, ctx),
            e.Slope,
            e.MaxIntensity,
            ctx.Caster,
            maxTileBreak: 0);

    private EntityUid? Nearest(SpellCastContext ctx)
    {
        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var creature in _creatures)
        {
            if (!ctx.Point.TryDistance(EntityManager, Transform(creature.Owner).Coordinates, out var distance) || distance >= bestDistance)
                continue;

            best = creature.Owner;
            bestDistance = distance;
        }

        return best;
    }

    private void Lightning(LightningGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        FindCreatures(ctx, Area(e.Range, step, ctx), includeCaster: false);
        if (Nearest(ctx) is not { } target)
            return;

        _lightning.ShootLightning(ctx.Caster, target, e.Prototype);
        ctx.Hit.Add(target);
    }

    private void Polymorph(PolymorphGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        FindCreatures(ctx, Area(e.Radius, step, ctx), includeCaster: false);
        if (e.Options.Count == 0 || Nearest(ctx) is not { } target)
            return;

        _polymorph.PolymorphEntity(target, _random.Pick(e.Options));
        ctx.Hit.Add(target);
    }

    private void Leap(LeapGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        var caster = ctx.Caster;
        var direction = _xform.ToMapCoordinates(ctx.Point).Position - _xform.GetWorldPosition(caster);
        if (direction.LengthSquared() < 0.01f)
            return;

        var length = MathF.Min(direction.Length(), e.MaxDistance + step.RadiusBonus);
        _throwing.TryThrow(caster, direction.Normalized() * length, e.Strength, caster, recoil: false, compensateFriction: true);
    }

    private void Slow(SlowGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        FindCreatures(ctx, Area(e.Radius, step, ctx), e.AffectCaster);
        var time = TimeSpan.FromSeconds(e.Seconds * Strength(step, ctx));
        foreach (var creature in _creatures.ToList())
        {
            _movement.TryAddMovementSpeedModDuration(creature.Owner, MovementModStatusSystem.FlashSlowdown, time, e.Multiplier);
            ctx.Hit.Add(creature.Owner);
        }
    }

    private void Haste(HasteGlyphEffect e, SpellStep step, SpellCastContext ctx)
    {
        FindCreatures(ctx, Area(e.Radius, step, ctx), includeCaster: true);
        var time = TimeSpan.FromSeconds(e.Seconds * Strength(step, ctx));
        foreach (var creature in _creatures.ToList())
        {
            _movement.TryAddMovementSpeedModDuration(creature.Owner, MovementModStatusSystem.ReagentSpeed, time, e.Multiplier);
            ctx.Hit.Add(creature.Owner);
        }
    }
}
