using System.Linq;
using Content.Shared.Movement.Events;
using Content.Shared._Starlight.Flock.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Flock;

/// <summary>
/// Flock cages: the occupant is slowly dissolved and the cage hatches an egg when they're consumed.
/// The occupant can try to break out by moving; it takes <see cref="FlockCageComponent.EscapeTime"/>.
/// </summary>
public sealed partial class FlockCageSystem : EntitySystem
{
    [Dependency] private FlockSystem _flock = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly Dictionary<EntityUid, (TimeSpan Start, TimeSpan Last)> _escape = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlockCageComponent, ContainerRelayMovementEntityEvent>(OnMove);
        SubscribeLocalEvent<FlockCageComponent, MapInitEvent>((u, c, _) => c.NextEat = _timing.CurTime + c.Interval);
    }

    private void OnMove(Entity<FlockCageComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        var now = _timing.CurTime;
        var (start, last) = _escape.TryGetValue(ent, out var v) ? v : (now, now);
        if (now - last > TimeSpan.FromSeconds(2))
            start = now;
        _escape[ent] = (start, now);
        if (now - start >= ent.Comp.EscapeTime)
        {
            _escape.Remove(ent);
            ent.Comp.Occupant = null;
            _storage.OpenStorage(ent);
            QueueDel(ent);
        }
        else if (now - start < TimeSpan.FromSeconds(0.3))
            _popup.PopupEntity(Loc.GetString("flock-cage-struggle"), args.Entity, args.Entity);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var q = EntityQueryEnumerator<FlockCageComponent, EntityStorageComponent>();
        while (q.MoveNext(out var uid, out var cage, out var storage))
        {
            if (now < cage.NextEat)
                continue;
            cage.NextEat = now + cage.Interval;

            var victim = storage.Contents.ContainedEntities.FirstOrDefault();
            if (victim == default)
            {
                // Empty cage evaporates.
                QueueDel(uid);
                continue;
            }

            if (_mobState.IsDead(victim))
            {
                Consume((uid, cage), victim);
                continue;
            }
            var dmg = new DamageSpecifier();
            dmg.DamageDict["Heat"] = FixedPoint2.New(6);
            _damageable.TryChangeDamage(victim, dmg, true, false);
        }
    }

    private void Consume(Entity<FlockCageComponent> cage, EntityUid victim)
    {
        var coords = Transform(cage).Coordinates;
        QueueDel(victim);
        var egg = Spawn(cage.Comp.EggProto, coords);
        if (_flock.TryGetFlock(cage, out var flock))
            _flock.AddMember(flock, egg);
        QueueDel(cage);
    }
}
