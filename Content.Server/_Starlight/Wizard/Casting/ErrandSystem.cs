using System.Linq;
using Content.Shared._Starlight.Wizard.Casting;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Wizard.Casting;

/// <summary>
/// Errands: small tasks the Winds set a wizard around the station. Each one finished gives a reward and a point of
/// Mastery, which makes the wizard permanently stronger. This is how wizards grow during a round, apart from objectives.
/// </summary>
public sealed partial class ErrandSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private RitualSystem _ritual = default!;

    /// <summary>How many errands a wizard has at once.</summary>
    public const int ActiveCount = 3;

    /// <summary>Mastery stops growing here: no more errands are set.</summary>
    public const int MaxPower = 12;

    // Each point of Mastery.
    private const float WindPerPower = 4f;
    private const float RegenPerPower = 0.06f;
    private const float VisitTick = 2f;

    private readonly HashSet<EntityUid> _nearby = [];
    private TimeSpan _nextTick;

    /// <summary>Makes sure a wizard has a full set of errands.</summary>
    public void EnsureErrands(EntityUid wizard)
    {
        var comp = EnsureComp<ErrandComponent>(wizard);
        Fill((wizard, comp));
    }

    private void Fill(Entity<ErrandComponent> ent)
    {
        if (TryComp<SpellcraftComponent>(ent, out var craft) && craft.Power >= MaxPower)
            return;

        var all = _proto.EnumeratePrototypes<ErrandPrototype>().Where(e => e.Weight > 0f).ToList();
        while (ent.Comp.Active.Count < ActiveCount)
        {
            var taken = ent.Comp.Active.Select(a => a.Proto.ID).ToHashSet();
            var groups = ent.Comp.Active.Select(a => a.Proto.Group).Where(g => g != string.Empty).ToHashSet();
            var pool = all.Where(e => !taken.Contains(e.ID) && (e.Group == string.Empty || !groups.Contains(e.Group))).ToList();
            if (pool.Count == 0)
                return;

            var total = pool.Sum(e => e.Weight);
            var roll = _random.NextFloat() * total;
            var pick = pool[^1];
            foreach (var candidate in pool)
            {
                roll -= candidate.Weight;
                if (roll > 0f)
                    continue;

                pick = candidate;
                break;
            }

            ent.Comp.Active.Add(new ActiveErrand(pick));
        }
    }

    // ---------------------------------------------------------------- progress

    [SubscribeLocalEvent]
    private void OnCast(Entity<ErrandComponent> ent, ref SpellCastEvent args)
    {
        foreach (var errand in ent.Comp.Active.ToList())
        {
            switch (errand.Proto.Condition)
            {
                case CastSchoolCondition school:
                    if (args.Plan.Schools.Contains(school.School))
                        errand.Progress++;

                    break;
                case CastDeliveryCondition delivery:
                    if (args.Plan.Delivery == delivery.Delivery)
                        errand.Progress++;

                    break;
                case CastNearCondition near:
                    if (IsNear(args.Context.Point, near.Whitelist, near.Range))
                        errand.Progress++;

                    break;
            }
        }

        CheckCompletion(ent);
    }

    [SubscribeLocalEvent]
    private void OnHit(Entity<ErrandComponent> ent, ref SpellHitEvent args)
    {
        foreach (var errand in ent.Comp.Active)
        {
            if (errand.Proto.Condition is not AffectCreaturesCondition)
                continue;

            foreach (var hit in args.Hit)
                errand.Seen.Add(hit);

            errand.Progress = errand.Seen.Count;
        }

        CheckCompletion(ent);
    }

    [SubscribeLocalEvent]
    private void OnRite(Entity<ErrandComponent> ent, ref RitePerformedEvent args)
    {
        foreach (var errand in ent.Comp.Active)
        {
            if (errand.Proto.Condition is PerformRitesCondition)
                errand.Progress++;
        }

        CheckCompletion(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextTick)
            return;

        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(VisitTick);

        var query = EntityQueryEnumerator<ErrandComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var changed = false;
            foreach (var errand in comp.Active)
            {
                if (errand.Proto.Condition is not VisitNearCondition visit
                    || !IsNear(Transform(uid).Coordinates, visit.Whitelist, visit.Range))
                    continue;

                errand.Progress += (int) VisitTick;
                changed = true;
            }

            if (changed)
                CheckCompletion((uid, comp));
        }
    }

    private bool IsNear(EntityCoordinates origin, EntityWhitelist whitelist, float range)
    {
        var map = _xform.ToMapCoordinates(origin);
        _nearby.Clear();
        _lookup.GetEntitiesInRange(map.MapId, map.Position, range, _nearby, flags: LookupFlags.Static | LookupFlags.Dynamic | LookupFlags.Sundries);

        foreach (var found in _nearby)
        {
            if (_whitelist.IsValid(whitelist, found))
                return true;
        }

        return false;
    }

    /// <summary>Debug: finishes a wizard's first errand straight away.</summary>
    public bool CompleteFirst(EntityUid wizard)
    {
        if (!TryComp<ErrandComponent>(wizard, out var comp) || comp.Active.Count == 0)
            return false;

        var errand = comp.Active[0];
        errand.Progress = errand.Proto.Condition.Goal;
        CheckCompletion((wizard, comp));
        return true;
    }

    // ---------------------------------------------------------------- completion and rewards

    private void CheckCompletion(Entity<ErrandComponent> ent)
    {
        var done = ent.Comp.Active.Where(e => e.Progress >= e.Proto.Condition.Goal).ToList();
        if (done.Count == 0)
            return;

        foreach (var errand in done)
        {
            ent.Comp.Active.Remove(errand);
            ent.Comp.Completed++;
            Complete(ent, errand);
        }

        Fill(ent);
    }

    private void Complete(Entity<ErrandComponent> ent, ActiveErrand errand)
    {
        var wizard = ent.Owner;
        var rewardText = ApplyReward(wizard, errand.Proto.Reward);

        if (TryComp<SpellcraftComponent>(wizard, out var craft))
        {
            craft.Power++;
            Dirty(wizard, craft);
        }

        if (TryComp<WindComponent>(wizard, out var wind))
        {
            wind.Max += WindPerPower;
            wind.RegenPerSecond += RegenPerPower;
            Dirty(wizard, wind);
        }

        _popup.PopupEntity(Loc.GetString("errand-complete", ("title", Loc.GetString(errand.Proto.Title)), ("reward", rewardText)),
            wizard,
            wizard,
            PopupType.LargeCaution);

        var completed = new ErrandCompletedEvent(wizard);
        RaiseLocalEvent(wizard, ref completed);
    }

    private string ApplyReward(EntityUid wizard, ErrandReward reward)
    {
        TryComp<WindComponent>(wizard, out var wind);
        TryComp<SpellcraftComponent>(wizard, out var craft);

        switch (reward)
        {
            case MaxWindReward max when wind != null:
                wind.Max += max.Amount;
                Dirty(wizard, wind);
                return Loc.GetString("errand-reward-maxwind", ("amount", max.Amount));
            case RegenReward regen when wind != null:
                wind.RegenPerSecond += regen.Amount;
                Dirty(wizard, wind);
                return Loc.GetString("errand-reward-regen", ("amount", regen.Amount));
            case NodesReward nodes when craft != null:
                craft.MaxNodes += nodes.Amount;
                Dirty(wizard, craft);
                return Loc.GetString("errand-reward-nodes", ("amount", nodes.Amount));
            case SlotsReward slots when craft != null:
                craft.MaxSpells += slots.Amount;
                Dirty(wizard, craft);
                return Loc.GetString("errand-reward-slots", ("amount", slots.Amount));
            case GlyphReward glyph:
                _ritual.LearnGlyphs(wizard, glyph.Count);
                return Loc.GetString("errand-reward-glyph");
            case RoteReward rote:
                _ritual.LearnRotes(wizard, rote.Count);
                return Loc.GetString("errand-reward-rote");
            case AttuneReward:
                _ritual.Attune(wizard);
                return Loc.GetString("errand-reward-attune");
        }

        return string.Empty;
    }

    /// <summary>Short description of a reward, for the window.</summary>
    public string DescribeReward(ErrandReward reward)
        => reward switch
        {
        MaxWindReward max => Loc.GetString("errand-reward-maxwind", ("amount", max.Amount)),
        RegenReward regen => Loc.GetString("errand-reward-regen", ("amount", regen.Amount)),
        NodesReward nodes => Loc.GetString("errand-reward-nodes", ("amount", nodes.Amount)),
        SlotsReward slots => Loc.GetString("errand-reward-slots", ("amount", slots.Amount)),
        GlyphReward => Loc.GetString("errand-reward-glyph"),
        RoteReward => Loc.GetString("errand-reward-rote"),
        AttuneReward => Loc.GetString("errand-reward-attune"),
        _ => string.Empty,
    };

    /// <summary>What the window shows: each errand and how far along it is.</summary>
    public List<ErrandInfo> GetInfos(EntityUid wizard)
    {
        if (!TryComp<ErrandComponent>(wizard, out var comp))
            return [];

        return comp.Active
            .Select(e => new ErrandInfo(Loc.GetString(e.Proto.Title),
                Loc.GetString(e.Proto.Description),
                Math.Min(e.Progress, e.Proto.Condition.Goal),
                e.Proto.Condition.Goal,
                DescribeReward(e.Proto.Reward)))
            .ToList();
    }
}
