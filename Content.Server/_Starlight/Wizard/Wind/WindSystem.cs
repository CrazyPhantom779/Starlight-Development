using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Actions.Events;
using Content.Shared.Alert;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Damage.Components;

namespace Content.Server._Starlight.Wizard.Wind;

public sealed partial class WindSystem : SharedWindSystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private static readonly TimeSpan _alertInterval = TimeSpan.FromSeconds(0.5);
    private const short MaxSeverity = 5;
    private const float CritThreshold = 100f;
    private const float MinHurtFactor = 0.2f;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<WindComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.LastUpdate = Timing.CurTime;
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnStartup(Entity<WindComponent> ent, ref ComponentStartup args)
        => UpdateAlert(ent);

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<WindComponent> ent, ref ComponentShutdown args)
        => _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);

    [SubscribeLocalEvent]
    private void OnActionPerformed(Entity<WindCostComponent> ent, ref ActionPerformedEvent args)
    {
        if (!TryComp<WindComponent>(args.Performer, out var wind))
            return;

        var performer = new Entity<WindComponent>(args.Performer, wind);
        var newValue = GetWind(performer) - GetCost(performer, ent.Comp.Cost);
        SetWind(performer, newValue);
        UpdateAlert(performer);

        if (newValue < 0f)
        {
            var ev = new WindOverdrawnEvent(args.Performer, -newValue);
            RaiseLocalEvent(args.Performer, ref ev);
        }
    }

    /// <summary>
    /// Wounds slow Wind recovery. Settle what has regenerated so far at the old rate, then switch to the new one.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnDamageChanged(Entity<WindComponent> ent, ref DamageChangedEvent args)
    {
        var threshold = CritThreshold;
        if (_thresholds.TryGetThresholdForState(ent, MobState.Critical, out var crit) && crit.Value > FixedPoint2.Zero)
            threshold = crit.Value.Float();

        var hurt = Math.Clamp(_damageable.GetTotalDamage((ent.Owner, args.Damageable)).Float() / threshold, 0f, 1f);
        var factor = MathHelper.Lerp(1f, MinHurtFactor, hurt);
        if (MathF.Abs(factor - ent.Comp.HurtFactor) < 0.02f)
            return;

        SetWind(ent, GetWind(ent));
        ent.Comp.HurtFactor = factor;
        Dirty(ent);
    }

    /// <summary>
    /// Spends Wind for something that isn't an action (wands, enchanted items, rituals).
    /// Casters without Wind pay nothing. Returns false if the cost cannot be afforded.
    /// </summary>
    public bool TrySpend(EntityUid performer, float baseCost)
    {
        if (!TryComp<WindComponent>(performer, out var wind))
            return true;

        var ent = new Entity<WindComponent>(performer, wind);
        if (!CanAfford(ent, baseCost))
            return false;

        var value = GetWind(ent) - GetCost(ent, baseCost);
        SetWind(ent, value);
        UpdateAlert(ent);

        if (value < 0f)
        {
            var ev = new WindOverdrawnEvent(performer, -value);
            RaiseLocalEvent(performer, ref ev);
        }

        return true;
    }

    private void UpdateAlert(Entity<WindComponent> ent)
    {
        var fraction = Math.Clamp(GetWind(ent) / MathF.Max(1f, ent.Comp.Max), 0f, 1f);
        var severity = (short) Math.Round(fraction * MaxSeverity);
        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert, severity);
        ent.Comp.NextAlertUpdate = Timing.CurTime + _alertInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WindComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (Timing.CurTime < comp.NextAlertUpdate)
                continue;

            UpdateAlert((uid, comp));
        }
    }
}
