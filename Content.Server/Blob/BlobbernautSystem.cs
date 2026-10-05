using System.Linq;
using System.Numerics;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Blob;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Damage.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server.Blob;

public sealed partial class BlobbernautSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private ExplosionSystem _explosionSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EmpSystem _empSystem = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private MapSystem _mapSystem = default!;

    [SubscribeLocalEvent]
    private void OnMeleeHit(EntityUid uid, BlobbernautComponent component, MeleeHitEvent args)
    {
        if (args.HitEntities.Count < 1)
            return;

        var target = args.HitEntities[0];

        if (!TryComp<BlobTileComponent>(component.Factory, out var blobTileComponent))
            return;

        if (!TryComp<BlobCoreComponent>(blobTileComponent.Core, out var blobCoreComponent))
            return;

        if (blobCoreComponent.CurrentChem == BlobChemType.ExplosiveLattice)
        {
            _explosionSystem.QueueExplosion(
                target,
                blobCoreComponent.BlobExplosive,
                4f,
                1f,
                2f,
                maxTileBreak: 0);
        }

        if (blobCoreComponent.CurrentChem == BlobChemType.ElectromagneticWeb)
        {
            var coords = _transformSystem.GetMapCoordinates(target);

            if (_random.Prob(0.2f))
                _empSystem.EmpPulse(coords, 3f, 50f, TimeSpan.FromSeconds(3));
        }
    }

    [SubscribeLocalEvent]
    private void OnGetState(EntityUid uid, BlobbernautComponent component, ref ComponentGetState args) =>
        args.State = new BlobbernautComponentState()
        {
            Color = component.Color
        };

    [SubscribeLocalEvent]
    private void OnMobStateChanged(EntityUid uid, BlobbernautComponent component, MobStateChangedEvent args) =>
        component.IsDead = args.NewMobState switch
        {
            MobState.Dead => true,
            MobState.Alive => false,
            _ => component.IsDead
        };

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var blobFactoryQuery = EntityQueryEnumerator<BlobbernautComponent>();
        while (blobFactoryQuery.MoveNext(out var ent, out var comp))
        {
            if (comp.IsDead)
                continue;

            if (_gameTiming.CurTime < comp.NextDamage)
                continue;

            if (comp.Factory == null)
            {
                _popup.PopupEntity(Loc.GetString("blobberaut-factory-destroy"), ent, ent, PopupType.LargeCaution);
                _damageableSystem.TryChangeDamage(ent, comp.Damage);
                comp.NextDamage = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.DamageFrequency);
                continue;
            }

            var xform = Transform(ent);

            if (xform.GridUid == null)
                continue;

            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                continue;

            var radius = 1f;
            var localPos = xform.Coordinates.Position;

            var tiles = _mapSystem.GetLocalTilesIntersecting(
                xform.GridUid.Value,
                grid,
                new Box2(
                    localPos + new Vector2(-radius, -radius),
                    localPos + new Vector2(radius, radius)),
                false).ToArray();

            foreach (var tileRef in tiles)
            {
                foreach (var entOnTile in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tileRef.GridIndices))
                {
                    if (TryComp<BlobTileComponent>(entOnTile, out var blobTileComponent) &&
                        blobTileComponent.Core != null)
                    {
                        continue;
                    }
                }
            }

            _popup.PopupEntity(Loc.GetString("blobberaut-not-on-blob-tile"), ent, ent, PopupType.LargeCaution);
            _damageableSystem.TryChangeDamage(ent, comp.Damage);
            comp.NextDamage = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.DamageFrequency);
        }
    }
}
