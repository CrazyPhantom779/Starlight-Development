using System.Numerics;
using Content.Shared._Starlight.Holograms;
using Content.Shared._Starlight.Holograms.Components;
using Content.Shared._Starlight.Holograms.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;

namespace Content.Client._Starlight.Holograms;

public sealed partial class HologramSystem : SharedHologramSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private EyeSystem _eye = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramProjectedComponent, ComponentShutdown>(OnProjectedShutdown);
    }

    public override void Update(float frameTime)
    {
        UpdateLocalEyeTarget();
        UpdateProjectedEffects();
    }

    private void OnProjectedShutdown(EntityUid uid, HologramProjectedComponent component, ComponentShutdown args)
    {
        DeleteEffect(component);
        ClearEyeTarget(uid, component);
    }

    private void UpdateLocalEyeTarget()
    {
        if (_player.LocalSession?.AttachedEntity is not { } player ||
            !TryComp<HologramProjectedComponent>(player, out var projected))
            return;

        UpdateEyeTarget(player, projected);
    }

    private void UpdateEyeTarget(EntityUid uid, HologramProjectedComponent projected)
    {
        if (!TryComp<EyeComponent>(uid, out var eye))
            return;

        if (!projected.SetEyeTarget ||
            projected.CurProjector is not { } projectorNet ||
            !TryGetEntity(projectorNet, out var projector) ||
            projector is not { } projectorUid ||
            !Exists(projectorUid))
        {
            _eye.SetTarget(uid, null, eye);
            return;
        }

        _eye.SetTarget(uid, projectorUid, eye);
    }

    private void ClearEyeTarget(EntityUid uid, HologramProjectedComponent projected)
    {
        if (!projected.SetEyeTarget || !TryComp<EyeComponent>(uid, out var eye))
            return;

        _eye.SetTarget(uid, null, eye);
    }

    private void UpdateProjectedEffects()
    {
        var query = EntityQueryEnumerator<HologramProjectedComponent>();
        while (query.MoveNext(out var hologram, out var projected))
        {
            if (!TryGetEffectData(hologram, projected, out var coords, out var rotation, out var distance))
            {
                DeleteEffect(projected);
                continue;
            }

            EnsureEffect(projected, coords, rotation, distance);
        }
    }

    private bool TryGetEffectData(
        EntityUid hologram,
        HologramProjectedComponent projected,
        out EntityCoordinates effectCoords,
        out Angle rotation,
        out float distance)
    {
        effectCoords = default;
        rotation = default;
        distance = default;

        if (projected.EffectPrototype == null)
            return false;

        if (projected.CurProjector is not { } projectorNet ||
            !TryGetEntity(projectorNet, out var projector) ||
            projector is not { } projectorUid ||
            !Exists(projectorUid))
            return false;

        var hologramXform = Transform(hologram);
        var hologramCoords = _transform.GetMoverCoordinates(hologram, hologramXform);

        var projectorXform = Transform(projectorUid);
        var projectorCoords = _transform.GetMoverCoordinates(projectorUid, projectorXform);

        if (hologramCoords.EntityId != projectorCoords.EntityId)
            return false;

        var origin = projectorCoords.Position + GetProjectorEffectOffset(projectorUid, projectorXform);
        var delta = hologramCoords.Position - origin;
        distance = MathF.Max(delta.Length(), 0.05f);

        effectCoords = new EntityCoordinates(hologramCoords.EntityId, (hologramCoords.Position + origin) / 2f);
        if (!effectCoords.IsValid(EntityManager))
            return false;

        rotation = delta.ToAngle() - MathHelper.PiOver2;
        return true;
    }

    private Vector2 GetProjectorEffectOffset(EntityUid projectorUid, TransformComponent projectorXform)
    {
        if (!TryComp(projectorUid, out HologramProjectorComponent? projector))
            return Vector2.Zero;

        Direction? opposite = projectorXform.LocalRotation.GetCardinalDir() switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            _ => null,
        };

        return opposite is { } direction && projector.EffectOffsets.TryGetValue(direction, out var offset)
            ? offset
            : Vector2.Zero;
    }

    private void EnsureEffect(HologramProjectedComponent projected, EntityCoordinates coords, Angle rotation, float distance)
    {
        if (projected.EffectPrototype == null)
            return;

        if (projected.EffectEntity is not { } effect || !Exists(effect))
        {
            effect = Spawn(projected.EffectPrototype, coords);
            projected.EffectEntity = effect;
        }
        else
        {
            _transform.SetCoordinates(effect, coords);
        }

        _transform.SetLocalRotation(effect, rotation);
        _sprite.SetScale(effect, new Vector2(1f, distance));
    }

    private void DeleteEffect(HologramProjectedComponent projected)
    {
        if (projected.EffectEntity is { } effect && Exists(effect))
            QueueDel(effect);

        projected.EffectEntity = null;
    }
}
