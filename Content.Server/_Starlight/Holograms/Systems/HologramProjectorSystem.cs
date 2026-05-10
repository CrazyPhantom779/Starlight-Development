using Content.Server.Power.Components;
using Content.Shared._Starlight.Holograms;
using Content.Shared.Power;
using Content.Shared.SurveillanceCamera.Components;

namespace Content.Server._Starlight.Holograms.Systems;

public sealed class HologramProjectorSystem : EntitySystem
{
    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramProjectorComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnPowerChanged(EntityUid uid, HologramProjectorComponent component, ref PowerChangedEvent args)
        => CheckState(uid, component);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        _accumulator = 0f;

        var query = EntityQueryEnumerator<HologramProjectorComponent>();
        while (query.MoveNext(out var uid, out var projector))
        {
            CheckState(uid, projector);
        }
    }

    public void CheckState(EntityUid projector, HologramProjectorComponent? projComp = null)
    {
        if (!Resolve(projector, ref projComp))
            return;

        var shouldBeActive = !((TryComp<ApcPowerReceiverComponent>(projector, out var powerComp) && !powerComp.Powered) ||
                               (TryComp<SurveillanceCameraComponent>(projector, out var cameraComp) && !cameraComp.Active));

        if (projComp.IsActive == shouldBeActive)
            return;

        projComp.IsActive = shouldBeActive;
        Dirty(projector, projComp);
    }
}
