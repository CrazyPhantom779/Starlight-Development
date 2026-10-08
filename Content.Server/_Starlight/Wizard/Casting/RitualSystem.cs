using System.Linq;
using Content.Server._Starlight.Wizard.Items;
using Content.Server._Starlight.Wizard.SpellGraph;
using Content.Server._Starlight.Wizard.Wind;
using Content.Shared._Starlight.Wizard.Casting;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Wizard.Casting;

/// <summary>
/// Ritual circles and the rites performed on them. A circle is drawn for Wind; a rite consumes items lying inside it
/// and does something the performer could not do alone: wands, new glyphs, new disciplines, big spells.
/// </summary>
public sealed partial class RitualSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private WindSystem _wind = default!;
    [Dependency] private SpellGraphSystem _spells = default!;
    [Dependency] private TarotSystem _tarot = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private static readonly EntProtoId _circleProto = "RitualCircle";
    private static readonly EntProtoId _flash = "EffectFlashBluespaceSimple";

    private const float DrawCost = 10f;
    private const float CircleReach = 3f;
    private const float StayWithin = 4.5f;
    private const float OfferingRadius = 1.6f;

    private readonly HashSet<Entity<ItemComponent>> _items = [];

    private bool CanPerform(EntityUid wizard)
        => !TryComp<SpellcraftComponent>(wizard, out var craft)
           || craft.Unrestricted
           || craft.Disciplines.Contains(SpellDiscipline.Ritual);

    // ---------------------------------------------------------------- circles

    public bool TryDrawCircle(EntityUid wizard)
    {
        if (!CanPerform(wizard))
            return false;

        if (!_wind.TrySpend(wizard, DrawCost))
        {
            _popup.PopupEntity(Loc.GetString("wind-not-enough"), wizard, wizard);
            return false;
        }

        // One circle at a time.
        var query = EntityQueryEnumerator<RitualCircleComponent>();
        while (query.MoveNext(out var uid, out var existing))
        {
            if (existing.Owner == wizard)
                QueueDel(uid);
        }

        var circle = Spawn(_circleProto, Transform(wizard).Coordinates.SnapToGrid(EntityManager));
        EnsureComp<RitualCircleComponent>(circle).Owner = wizard;
        _popup.PopupEntity(Loc.GetString("ritual-circle-drawn"), wizard, wizard);
        return true;
    }

    public bool TryFindCircle(EntityUid wizard, out Entity<RitualCircleComponent> circle)
    {
        circle = default;
        var wizardCoords = Transform(wizard).Coordinates;
        var best = float.MaxValue;

        var query = EntityQueryEnumerator<RitualCircleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Owner != wizard
                || !wizardCoords.TryDistance(EntityManager, Transform(uid).Coordinates, out var distance)
                || distance > CircleReach
                || distance >= best)
                continue;

            best = distance;
            circle = (uid, comp);
        }

        return best < float.MaxValue;
    }

    // ---------------------------------------------------------------- offerings

    private bool TryMatch(RitualPrototype ritual, EntityCoordinates center, List<OfferingInfo>? info, out List<EntityUid> consume)
    {
        consume = [];
        _items.Clear();
        _lookup.GetEntitiesInRange(center, OfferingRadius, _items);

        // Spell-holding objects and tomes are never valid offerings.
        var available = _items
            .Select(e => e.Owner)
            .Where(e => !HasComp<SpellItemComponent>(e) && !HasComp<SpellweaverTomeComponent>(e))
            .ToList();

        var satisfied = true;
        foreach (var offering in ritual.Offerings)
        {
            var taken = 0;
            foreach (var candidate in available.ToList())
            {
                if (taken >= offering.Count)
                    break;

                if (!_whitelist.IsValid(offering.Whitelist, candidate))
                    continue;

                consume.Add(candidate);
                available.Remove(candidate);
                taken++;
            }

            info?.Add(new OfferingInfo(Loc.GetString(offering.Label), taken, offering.Count));
            if (taken < offering.Count)
                satisfied = false;
        }

        return satisfied;
    }

    /// <summary>Everything the Ritual tab needs: each rite and how much of it is currently on the circle.</summary>
    public List<RitualInfo> GetInfos(EntityUid wizard)
    {
        var result = new List<RitualInfo>();
        var hasCircle = TryFindCircle(wizard, out var circle);
        var center = hasCircle ? Transform(circle).Coordinates : default;

        foreach (var ritual in _proto.EnumeratePrototypes<RitualPrototype>().OrderBy(r => Loc.GetString(r.Name)))
        {
            var offerings = new List<OfferingInfo>();
            var satisfied = false;

            if (hasCircle)
            {
                satisfied = TryMatch(ritual, center, offerings, out _);
            }
            else
            {
                foreach (var offering in ritual.Offerings)
                    offerings.Add(new OfferingInfo(Loc.GetString(offering.Label), 0, offering.Count));
            }

            result.Add(new RitualInfo(ritual.ID,
                Loc.GetString(ritual.Name),
                Loc.GetString(ritual.Description),
                offerings,
                satisfied,
                ritual.WindCost,
                ritual.Seconds));
        }

        return result;
    }

    // ---------------------------------------------------------------- performing

    public bool TryPerform(EntityUid wizard, string ritualId)
    {
        if (!CanPerform(wizard) || !_proto.TryIndex<RitualPrototype>(ritualId, out var ritual))
            return false;

        if (!TryFindCircle(wizard, out var circle))
        {
            _popup.PopupEntity(Loc.GetString("ritual-error-no-circle"), wizard, wizard);
            return false;
        }

        if (circle.Comp.Busy)
        {
            _popup.PopupEntity(Loc.GetString("ritual-error-busy"), wizard, wizard);
            return false;
        }

        if (!TryMatch(ritual, Transform(circle).Coordinates, null, out _))
        {
            _popup.PopupEntity(Loc.GetString("ritual-error-offerings"), wizard, wizard);
            return false;
        }

        if (ritual.WindCost > 0f && !_wind.TrySpend(wizard, ritual.WindCost))
        {
            _popup.PopupEntity(Loc.GetString("wind-not-enough"), wizard, wizard);
            return false;
        }

        circle.Comp.Busy = true;
        _popup.PopupEntity(Loc.GetString("ritual-begin"), circle, wizard, PopupType.Medium);

        var circleUid = circle.Owner;
        Timer.Spawn(TimeSpan.FromSeconds(ritual.Seconds), () => Finish(wizard, circleUid, ritual));
        return true;
    }

    private void Finish(EntityUid wizard, EntityUid circleUid, RitualPrototype ritual)
    {
        if (TerminatingOrDeleted(circleUid) || !TryComp<RitualCircleComponent>(circleUid, out var circle))
            return;

        circle.Busy = false;
        var center = Transform(circleUid).Coordinates;

        List<EntityUid> consume = [];
        var interrupted = TerminatingOrDeleted(wizard)
                          || !center.TryDistance(EntityManager, Transform(wizard).Coordinates, out var distance)
                          || distance > StayWithin;

        if (interrupted || !TryMatch(ritual, center, null, out consume))
        {
            if (!TerminatingOrDeleted(wizard))
                _popup.PopupEntity(Loc.GetString("ritual-error-interrupted"), wizard, wizard);

            return;
        }

        foreach (var item in consume)
            QueueDel(item);

        Spawn(_flash, center);
        Apply(wizard, circleUid, center, ritual.Result);

        var performed = new RitePerformedEvent(wizard);
        RaiseLocalEvent(wizard, ref performed);
    }

    private void Apply(EntityUid wizard, EntityUid _, EntityCoordinates center, RitualResult result)
    {
        switch (result)
        {
            case SpawnRitualResult spawn:
                for (var i = 0; i < spawn.Amount; i++)
                {
                    foreach (var proto in spawn.Prototypes)
                        Spawn(proto, center.Offset(new System.Numerics.Vector2(_random.NextFloat(-0.4f, 0.4f), _random.NextFloat(-0.4f, 0.4f))));
                }

                break;
            case LearnGlyphRitualResult glyph:
                LearnGlyphs(wizard, glyph.Count, glyph.School);
                break;
            case LearnDisciplineRitualResult:
                LearnDiscipline(wizard);
                break;
            case AttuneRitualResult:
                Attune(wizard);
                break;
            case LearnRoteRitualResult rote:
                LearnRotes(wizard, rote.Count);
                break;
            case RestoreWindRitualResult wind:
                if (TryComp<WindComponent>(wizard, out var comp))
                    _wind.AddWind((wizard, comp), wind.Amount);

                _popup.PopupEntity(Loc.GetString("ritual-wind"), wizard, wizard);
                break;
            case CastRitualResult cast:
                CastAtCircle(wizard, center, cast);
                break;
            case TarotRitualResult tarot:
                for (var i = 0; i < tarot.Count; i++)
                    _tarot.DrawCard(wizard);

                break;
        }
    }

    /// <summary>Teaches a wizard glyphs they do not know yet, optionally only from one school.</summary>
    public void LearnGlyphs(EntityUid wizard, int count, string? school = null)
    {
        if (!TryComp<SpellcraftComponent>(wizard, out var craft))
            return;

        for (var i = 0; i < count; i++)
        {
            var pool = _proto.EnumeratePrototypes<SpellGlyphPrototype>()
                .Where(g => !craft.Glyphs.Contains(g.ID) && (school == null || g.Schools.Contains(school)))
                .ToList();

            if (pool.Count == 0)
            {
                _popup.PopupEntity(Loc.GetString("ritual-learn-nothing"), wizard, wizard);
                return;
            }

            var glyph = PickWeighted(pool, g => g.Weight);
            craft.Glyphs.Add(glyph.ID);
            _popup.PopupEntity(Loc.GetString("ritual-learn-glyph", ("glyph", Loc.GetString(glyph.Name))), wizard, wizard, PopupType.Medium);
        }

        Dirty(wizard, craft);
    }

    private void LearnDiscipline(EntityUid wizard)
    {
        if (!TryComp<SpellcraftComponent>(wizard, out var craft))
            return;

        var pool = Enum.GetValues<SpellDiscipline>().Where(d => !craft.Disciplines.Contains(d)).ToList();
        if (pool.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("ritual-learn-nothing"), wizard, wizard);
            return;
        }

        var discipline = _random.Pick(pool);
        craft.Disciplines.Add(discipline);
        Dirty(wizard, craft);
        _popup.PopupEntity(Loc.GetString("ritual-learn-discipline", ("discipline", Loc.GetString($"spellcraft-discipline-{discipline.ToString().ToLowerInvariant()}"))), wizard, wizard, PopupType.Medium);
    }

    /// <summary>Attunes a wizard to a school they are not attuned to yet.</summary>
    public void Attune(EntityUid wizard)
    {
        if (!TryComp<SpellcraftComponent>(wizard, out var craft))
            return;

        var pool = _proto.EnumeratePrototypes<SpellGlyphPrototype>()
            .SelectMany(g => g.Schools)
            .Distinct()
            .Where(s => !craft.Schools.Contains(s))
            .ToList();

        if (pool.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("ritual-learn-nothing"), wizard, wizard);
            return;
        }

        var school = _random.Pick(pool);
        craft.Schools.Add(school);
        Dirty(wizard, craft);
        _popup.PopupEntity(Loc.GetString("ritual-attune", ("school", Loc.GetString($"spellcraft-school-{school.ToLowerInvariant()}"))), wizard, wizard, PopupType.Medium);
    }

    /// <summary>Teaches a wizard prepared spells they do not know yet.</summary>
    public void LearnRotes(EntityUid wizard, int count)
    {
        if (!TryComp<SpellcraftComponent>(wizard, out var craft))
            return;

        for (var i = 0; i < count; i++)
        {
            var pool = _proto.EnumeratePrototypes<RoteSpellPrototype>().Where(r => !craft.Rotes.Contains(r.ID)).ToList();
            if (pool.Count == 0)
            {
                _popup.PopupEntity(Loc.GetString("ritual-learn-nothing"), wizard, wizard);
                return;
            }

            var rote = PickWeighted(pool, r => r.Weight);
            craft.Rotes.Add(rote.ID);
            _popup.PopupEntity(Loc.GetString("ritual-learn-rote", ("rote", Loc.GetString(rote.Name))), wizard, wizard, PopupType.Medium);
        }

        Dirty(wizard, craft);
    }

    private void CastAtCircle(EntityUid wizard, EntityCoordinates center, CastRitualResult cast)
    {
        if (!SpellGraphCompiler.TryBuildChain(_proto, cast.Chain, out var graph, out _)
            || !_spells.TryCompile(wizard, graph, SpellDiscipline.Ritual, ignoreKnown: true, out var plan, out _))
            return;

        // A rite is centred on the circle, not on the performer.
        var ctx = new SpellCastContext
        {
            Caster = wizard,
            Point = center,
            FormRadius = plan.Form.Radius,
        };
        _spells.RunPlan(plan, ctx);
    }

    private T PickWeighted<T>(List<T> pool, Func<T, float> weight)
    {
        var total = 0f;
        foreach (var entry in pool)
            total += weight(entry);

        var roll = _random.NextFloat() * total;
        foreach (var entry in pool)
        {
            roll -= weight(entry);
            if (roll <= 0f)
                return entry;
        }

        return pool[^1];
    }
}
