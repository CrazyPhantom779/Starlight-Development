using Content.Shared.Actions.Events;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Wizard.Wind;

public abstract partial class SharedWindSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    /// <summary>Gets the live Wind value, including passive regeneration since the last write.</summary>
    public float GetWind(Entity<WindComponent> ent)
    {
        var comp = ent.Comp;
        var elapsed = (float) (Timing.CurTime - comp.LastUpdate).TotalSeconds;
        if (elapsed < 0f)
            elapsed = 0f;

        var value = comp.Current;
        // Regeneration only ever fills up to Max; it never pulls a value that is above Max back down.
        if (value < comp.Max)
            value = MathF.Min(comp.Max, value + (elapsed * comp.RegenPerSecond * comp.RegenMultiplier));

        return value;
    }

    /// <summary>Sets Wind to an exact value (clamped to the allowed range).</summary>
    public void SetWind(Entity<WindComponent> ent, float value)
    {
        ent.Comp.Current = Math.Clamp(value, -ent.Comp.OverdrawLimit, ent.Comp.Max);
        ent.Comp.LastUpdate = Timing.CurTime;
        Dirty(ent);
    }

    public void AddWind(Entity<WindComponent> ent, float amount)
        => SetWind(ent, GetWind(ent) + amount);

    /// <summary>The Wind this performer would pay for a spell with the given base cost.</summary>
    public float GetCost(Entity<WindComponent> ent, float baseCost)
        => MathF.Max(0f, baseCost * ent.Comp.CostMultiplier);

    /// <summary>Whether the performer can pay this cost without exceeding their overdraw limit.</summary>
    public bool CanAfford(Entity<WindComponent> ent, float baseCost)
        => GetWind(ent) - GetCost(ent, baseCost) >= -ent.Comp.OverdrawLimit;

    [SubscribeLocalEvent]
    private void OnActionAttempt(Entity<WindCostComponent> ent, ref ActionAttemptEvent args)
    {
        // Performers without Wind (admins, non-wizards holding a spell) cast for free.
        if (!TryComp<WindComponent>(args.User, out var wind))
            return;

        if (CanAfford((args.User, wind), ent.Comp.Cost))
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("wind-not-enough"), args.User, args.User);
    }
}
