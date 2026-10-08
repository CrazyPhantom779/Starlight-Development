using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server._Starlight.Wizard.Casting;
using Content.Server._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Casting;
using Content.Shared._Starlight.Wizard.Fate;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Wizard.Fate;

/// <summary>
/// Rolls a wizard's Fate (Aspects, Instability, starting glyphs, a tome) the first time they become a wizard.
/// Mirrors how WizardRoleSystem finds wizards so every spawn path (roundstart, ghost role, admin) is covered.
/// </summary>
public sealed partial class FateSystem : EntitySystem
{
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedWindSystem _wind = default!;
    [Dependency] private SpellGraphSystem _spells = default!;
    [Dependency] private TideSystem _tides = default!;
    [Dependency] private ErrandSystem _errands = default!;

    private static readonly EntProtoId _tome = "SpellweaverTome";
    private static readonly EntProtoId _wand = "SpellWandBlank";

    private static readonly string[] _basicForms = ["FormAimed", "FormSelf", "FormBolt", "FormTouch", "FormBurst"];
    private const int AspectCount = 2;
    // The strongest effects are earned, through rites and errands, not handed out.
    private const float StartingMaxCost = 20f;
    private const int StartingSchools = 2;
    private const int StartingSchoolEffects = 4;
    private const int StartingOtherEffects = 3;
    private const int StartingDisciplines = 2;
    private const int StartingRotes = 5;
    private const int StartingPrimer = 2;
    private const int StartingAugments = 2;

    [SubscribeLocalEvent]
    private void OnRoleAdded(RoleAddedEvent args)
    {
        if (!_roles.MindHasRole<WizardRoleComponent>(args.MindId))
            return;

        if (args.Mind.OwnedEntity is { } body)
            TryRollFate(body);
    }

    [SubscribeLocalEvent]
    private void OnMindAdded(Entity<MindContainerComponent> ent, ref MindAddedMessage args)
    {
        if (_roles.MindHasRole<WizardRoleComponent>(args.Mind.Owner))
            TryRollFate(ent.Owner);
    }

    /// <summary>Rolls Fate for this entity unless it already has one. Also usable by admin tooling.</summary>
    public bool TryRollFate(EntityUid body)
    {
        if (HasComp<FateComponent>(body) || TerminatingOrDeleted(body))
            return false;

        var aspects = RollAspects();
        var instability = (int) Math.Max(0, Math.Round(aspects.Sum(a => a.Instability)));

        var fate = EnsureComp<FateComponent>(body);
        fate.Aspects = aspects.Select(a => new ProtoId<AspectPrototype>(a.ID)).ToList();
        fate.Instability = instability;
        Dirty(body, fate);

        ApplyAspects(body, aspects, instability);
        GiveStartingKit(body);
        Announce(body, aspects, instability);
        return true;
    }

    private List<AspectPrototype> RollAspects()
    {
        var pool = _proto.EnumeratePrototypes<AspectPrototype>().Where(a => a.Weight > 0f).ToList();
        var picked = new List<AspectPrototype>();

        while (picked.Count < AspectCount && pool.Count > 0)
        {
            var total = pool.Sum(a => a.Weight);
            var roll = _random.NextFloat() * total;
            AspectPrototype? choice = null;
            foreach (var aspect in pool)
            {
                roll -= aspect.Weight;
                if (roll <= 0f)
                {
                    choice = aspect;
                    break;
                }
            }

            choice ??= pool[^1];
            picked.Add(choice);

            // Drop the choice and anything that conflicts with it, either way round.
            pool.RemoveAll(a => a.ID == choice.ID
                                || choice.Conflicts.Any(c => c.Id == a.ID)
                                || a.Conflicts.Any(c => c.Id == choice.ID));
        }

        return picked;
    }

    private void ApplyAspects(EntityUid body, List<AspectPrototype> aspects, int instability)
    {
        var wind = EnsureComp<WindComponent>(body);
        var craft = EnsureComp<SpellcraftComponent>(body);

        // Instability is the dial for "stronger but wilder".
        var maxWind = wind.Max * (1f + (0.1f * instability));
        var regen = wind.RegenMultiplier;
        var cost = wind.CostMultiplier;
        var gust = wind.GustChanceMultiplier * (1f + (0.25f * instability));
        var nodes = craft.MaxNodes + (instability / 2);
        var spells = craft.MaxSpells;

        foreach (var aspect in aspects)
        {
            maxWind *= aspect.MaxWindMultiplier;
            regen *= aspect.RegenMultiplier;
            cost *= aspect.CostMultiplier;
            gust *= aspect.GustChanceMultiplier;
            nodes += aspect.ExtraNodes;
            spells += aspect.ExtraSpells;
        }

        wind.Max = MathF.Max(20f, maxWind);
        wind.RegenMultiplier = regen;
        wind.CostMultiplier = cost;
        wind.GustChanceMultiplier = gust;
        _wind.SetWind((body, wind), wind.Max);

        craft.MaxNodes = Math.Max(3, nodes);
        craft.MaxSpells = Math.Max(2, spells);
        Dirty(body, craft);
    }

    private void GiveStartingKit(EntityUid body)
    {
        var craft = EnsureComp<SpellcraftComponent>(body);
        var glyphs = _proto.EnumeratePrototypes<SpellGlyphPrototype>().ToList();

        // Everyone learns the basic Forms. One extra, rarer Form is rolled.
        foreach (var form in _basicForms)
            craft.Glyphs.Add(form);

        var extraForms = glyphs.Where(g => g.Category == GlyphCategory.Form && !craft.Glyphs.Contains(g.ID)).ToList();
        if (extraForms.Count > 0)
            craft.Glyphs.Add(_random.Pick(extraForms).ID);

        // Two schools they are attuned to: glyphs of these cost less.
        var schools = glyphs.SelectMany(g => g.Schools).Distinct().ToList();
        _random.Shuffle(schools);
        foreach (var school in schools.Take(StartingSchools))
            craft.Schools.Add(school);

        // Effects: a few from their schools, a few from anywhere.
        var effects = glyphs.Where(g => g.Category == GlyphCategory.Effect && g.Cost <= StartingMaxCost).ToList();
        _random.Shuffle(effects);
        foreach (var effect in effects.Where(e => e.Schools.Any(craft.Schools.Contains)).Take(StartingSchoolEffects))
            craft.Glyphs.Add(effect.ID);

        foreach (var effect in effects.Where(e => !craft.Glyphs.Contains(e.ID)).Take(StartingOtherEffects))
            craft.Glyphs.Add(effect.ID);

        var augments = glyphs.Where(g => g.Category == GlyphCategory.Augment).ToList();
        _random.Shuffle(augments);
        foreach (var augment in augments.Take(StartingAugments))
            craft.Glyphs.Add(augment.ID);

        // Two ways of working magic beyond the basics.
        var disciplines = Enum.GetValues<SpellDiscipline>().Where(d => !craft.Disciplines.Contains(d)).ToList();
        _random.Shuffle(disciplines);
        foreach (var discipline in disciplines.Take(StartingDisciplines))
            craft.Disciplines.Add(discipline);

        // Prepared spells.
        var rotes = _proto.EnumeratePrototypes<RoteSpellPrototype>().ToList();
        _random.Shuffle(rotes);
        foreach (var rote in rotes.Take(StartingRotes))
            craft.Rotes.Add(rote.ID);

        _tides.EnsureTides(body, craft);
        _errands.EnsureErrands(body);
        Dirty(body, craft);

        var tome = Spawn(_tome, Transform(body).Coordinates);
        _hands.TryPickupAnyHand(body, tome);

        if (craft.Disciplines.Contains(SpellDiscipline.Wandwright))
            _hands.TryPickupAnyHand(body, Spawn(_wand, Transform(body).Coordinates));

        WeavePrimer(body, craft);
    }

    /// <summary>
    /// Gives a couple of ready-to-use spells so a new wizard can cast immediately without opening the tome.
    /// These are two of their prepared spells.
    /// </summary>
    private void WeavePrimer(EntityUid body, SpellcraftComponent craft)
    {
        var known = craft.Rotes.Select(id => _proto.Index(id)).ToList();
        _random.Shuffle(known);

        foreach (var rote in known.Take(StartingPrimer))
        {
            if (SpellGraphCompiler.TryBuildChain(_proto, rote.Chain, out var graph, out _))
                _spells.TryCreateSpell(body, graph, SpellDiscipline.Rote, ignoreKnown: true, out _, out _);
        }
    }

    private void Announce(EntityUid body, List<AspectPrototype> aspects, int instability)
    {
        var lines = aspects.Select(a => $"- {Loc.GetString(a.Name)}: {Loc.GetString(a.Description)}");
        var message = Loc.GetString("fate-announce", ("instability", instability), ("aspects", string.Join("\n", lines)));
        if (TryComp<SpellcraftComponent>(body, out var craft))
        {
            var disciplines = string.Join(", ", craft.Disciplines.Select(d => Loc.GetString($"spellcraft-discipline-{d.ToString().ToLowerInvariant()}")));
            var schools = string.Join(", ", craft.Schools.Select(s => Loc.GetString($"spellcraft-school-{s.ToLowerInvariant()}")));
            message += "\n" + Loc.GetString("fate-announce-kit", ("disciplines", disciplines), ("schools", schools));
            message += "\n" + Loc.GetString("fate-announce-errands");
        }

        _popup.PopupEntity(Loc.GetString("fate-popup"), body, body, PopupType.LargeCaution);
        if (TryComp<ActorComponent>(body, out var actor))
            _chat.DispatchServerMessage(actor.PlayerSession, message);
    }
}
