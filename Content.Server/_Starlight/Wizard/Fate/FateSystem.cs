using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server._Starlight.Wizard.SpellGraph;
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

    private static readonly EntProtoId _tome = "SpellweaverTome";
    private const int AspectCount = 2;
    private const int StartingEffects = 4;
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

        foreach (var form in glyphs.Where(g => g.Category == GlyphCategory.Form))
            craft.Glyphs.Add(form.ID);

        var effects = glyphs.Where(g => g.Category == GlyphCategory.Effect).ToList();
        _random.Shuffle(effects);
        foreach (var effect in effects.Take(StartingEffects))
            craft.Glyphs.Add(effect.ID);

        var augments = glyphs.Where(g => g.Category == GlyphCategory.Augment).ToList();
        _random.Shuffle(augments);
        foreach (var augment in augments.Take(StartingAugments))
            craft.Glyphs.Add(augment.ID);

        Dirty(body, craft);

        var tome = Spawn(_tome, Transform(body).Coordinates);
        _hands.TryPickupAnyHand(body, tome);

        WeavePrimer(body, craft);
    }

    /// <summary>
    /// Gives a couple of ready-to-use spells so a new wizard can cast immediately without opening the tome.
    /// Built from the glyphs they were just granted: one aimed spell and, if possible, one self-centred one.
    /// </summary>
    private void WeavePrimer(EntityUid body, SpellcraftComponent craft)
    {
        var known = craft.Glyphs.Select(id => _proto.Index(id)).ToList();

        WeaveOne(body, known, "FormAimed", g => g.WorldEvent != null);
        WeaveOne(body, known, "FormSelf", g => g.InstantEvent != null);
    }

    private void WeaveOne(EntityUid body, List<SpellGlyphPrototype> known, string form, Func<SpellGlyphPrototype, bool> effectFilter)
    {
        var effects = known.Where(g => g.Category == GlyphCategory.Effect && effectFilter(g)).ToList();
        if (effects.Count == 0 || known.All(g => g.ID != form))
            return;

        var effect = _random.Pick(effects);
        var chain = new List<ProtoId<SpellGlyphPrototype>> { form, effect.ID };
        if (SpellGraphCompiler.TryBuildChain(_proto, chain, out var graph, out _))
            _spells.TryCreateSpell(body, graph, out _, out _);
    }

    private void Announce(EntityUid body, List<AspectPrototype> aspects, int instability)
    {
        var lines = aspects.Select(a => $"- {Loc.GetString(a.Name)}: {Loc.GetString(a.Description)}");
        var message = Loc.GetString("fate-announce", ("instability", instability), ("aspects", string.Join("\n", lines)));

        _popup.PopupEntity(Loc.GetString("fate-popup"), body, body, PopupType.LargeCaution);
        if (TryComp<ActorComponent>(body, out var actor))
            _chat.DispatchServerMessage(actor.PlayerSession, message);
    }
}
