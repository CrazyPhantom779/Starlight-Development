using Content.Server.Power.Components;
using Content.Shared._Starlight.Holograms;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Holograms.Systems;

/// <summary>
/// Keeps a projector's IsActive (power/on-off) and IsOnCooldown (damage) flags up to date, and
/// handles the damage-cooldown itself: how long it lasts, the sound, the one-time popup.
/// </summary>
public sealed partial class HologramProjectorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramProjectorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<HologramProjectorComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnPowerChanged(EntityUid uid, HologramProjectorComponent component, ref PowerChangedEvent args)
        => CheckState(uid, component);

    /// <summary>
    /// Damage starts (or extends) the cooldown, weighted by damage type - see
    /// CooldownDamageTypeMultipliers on the component. Ignores healing.
    /// </summary>
    private void OnDamageChanged(EntityUid uid, HologramProjectorComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is null)
            return;

        var weightedDamage = FixedPoint2.Zero;
        foreach (var (damageType, amount) in args.DamageDelta.DamageDict)
        {
            if (amount <= FixedPoint2.Zero)
                continue;

            var multiplier = component.CooldownDamageTypeMultipliers.GetValueOrDefault(damageType, 1f);
            weightedDamage += amount * multiplier;
        }

        if (weightedDamage <= FixedPoint2.Zero)
            return;

        var cooldownSeconds = MathF.Min(
            (float) weightedDamage * component.CooldownSecondsPerDamage,
            component.MaxCooldownSeconds);

        var newCooldownUntil = _timing.CurTime + TimeSpan.FromSeconds(cooldownSeconds);
        var wasOnCooldown = component.IsOnCooldown;

        // A follow-up hit while already down extends the cooldown rather than restarting a
        // shorter one under it.
        if (newCooldownUntil > component.CooldownUntil)
            component.CooldownUntil = newCooldownUntil;

        component.IsOnCooldown = true;
        Dirty(uid, component);

        _audio.PlayPvs(component.DamagedSound, uid);

        if (!wasOnCooldown)
            _popup.PopupEntity(Loc.GetString("hologram-projector-damaged-offline"), uid);
    }

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
            CheckCooldown(uid, projector);
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

    private void CheckCooldown(EntityUid projector, HologramProjectorComponent projComp)
    {
        if (!projComp.IsOnCooldown || _timing.CurTime < projComp.CooldownUntil)
            return;

        projComp.IsOnCooldown = false;
        Dirty(projector, projComp);
    }
}
