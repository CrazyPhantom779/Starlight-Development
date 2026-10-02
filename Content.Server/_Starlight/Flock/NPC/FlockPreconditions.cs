using Content.Shared._Starlight.Flock;
using Content.Shared._Starlight.Flock.Components;
using Content.Server._Starlight.Flock;

namespace Content.Server.NPC.HTN.Preconditions._Starlight.Flock;

/// <summary>Drone has enough resources for the current egg price and the flock isn't at its drone cap.</summary>
public sealed partial class FlockCanLayEggPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;
    private FlockSystem _flock = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _flock = sysManager.GetEntitySystem<FlockSystem>();
    }

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        return _entManager.TryGetComponent<FlockDroneComponent>(owner, out var drone)
            && _flock.TryGetFlock(owner, out var flock)
            && drone.Resources >= flock.Comp.CurrentEggCost
            && flock.Comp.Drones.Count < FlockConsts.DroneLimit;
    }
}

/// <summary>Drone carries at least <see cref="Amount"/> resources (or, inverted, fewer than that).</summary>
public sealed partial class FlockHasResourcesPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    [DataField]
    public int Amount = FlockConsts.ConvertCost;

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.TryGetComponent<FlockDroneComponent>(owner, out var drone))
            return Invert;
        var has = drone.Resources >= Amount;
        return Invert ? !has : has;
    }
}

/// <summary>Drone has enough charge to fire its incapacitor.</summary>
public sealed partial class FlockHasChargePrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        return _entManager.TryGetComponent<FlockDroneComponent>(owner, out var d) && d.Charge >= d.IncapacitorCost;
    }
}
