using System.Linq;
using Content.Server._Starlight.Wizard.Casting;
using Content.Server._Starlight.Wizard.Items;
using Content.Server._Starlight.Wizard.Wind;
using Content.Shared._Starlight.Wizard.Casting;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SpellGraphData = Content.Shared._Starlight.Wizard.SpellGraph.SpellGraph;

namespace Content.Server._Starlight.Wizard.SpellGraph;

/// <summary>Server side of the spell-weaving window: every discipline's actions end up here.</summary>
public sealed partial class SpellweaverTomeSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SpellGraphSystem _spells = default!;
    [Dependency] private SpellItemSystem _items = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private WindSystem _wind = default!;
    [Dependency] private TarotSystem _tarot = default!;
    [Dependency] private RitualSystem _ritual = default!;

    private static readonly EntProtoId _scrollProto = "SpellScrollBlank";

    // Limits on what a client may send in one request.
    private const int MaxNodes = 32;
    private const int MaxLinks = 64;

    // What it costs to put a spell into an object, as a multiple of the spell's cost.
    private const float WandBindFactor = 0.25f;
    private const float EnchantFactor = 2f;
    private const float ScrollFactor = 1.2f;
    private const int EnchantCharges = 3;

    private const float TarotCost = 12f;
    private const float CircleCost = 10f;
    private static readonly TimeSpan _tarotCooldown = TimeSpan.FromSeconds(20);

    private readonly Dictionary<EntityUid, TimeSpan> _nextDraw = [];

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SpellweaverTomeComponent>(SpellcraftUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<SpellcraftWeaveMessage>(OnWeave);
            subs.Event<SpellcraftRoteMessage>(OnRote);
            subs.Event<SpellcraftForgetMessage>(OnForget);
            subs.Event<SpellcraftDrawCardMessage>(OnDrawCard);
            subs.Event<SpellcraftCircleMessage>(OnCircle);
            subs.Event<SpellcraftRiteMessage>(OnRite);
            subs.Event<SpellcraftRefreshMessage>(OnRefresh);
        });
    }

    private void OnOpened(Entity<SpellweaverTomeComponent> ent, ref BoundUIOpenedEvent args)
    {
        var actor = args.Actor;

        var craft = EnsureComp<SpellcraftComponent>(actor);
        craft.MaxNodes = Math.Max(craft.MaxNodes, ent.Comp.MaxNodes);
        if (ent.Comp.GrantAll)
            craft.Unrestricted = true;

        foreach (var glyph in ent.Comp.Glyphs)
            craft.Glyphs.Add(glyph);

        Dirty(actor, craft);
        EnsureComp<WindComponent>(actor);

        SendState(ent, actor);
    }

    private void OnRefresh(Entity<SpellweaverTomeComponent> ent, ref SpellcraftRefreshMessage args)
        => SendState(ent, args.Actor);

    private bool Has(SpellcraftComponent craft, SpellDiscipline discipline)
        => craft.Unrestricted || craft.Disciplines.Contains(discipline);

    private void Fail(EntityUid actor, string? error)
        => _popup.PopupEntity(error ?? Loc.GetString("spellcraft-error-no-actions"), actor, actor);

    // ---------------------------------------------------------------- weaving

    private void OnWeave(Entity<SpellweaverTomeComponent> ent, ref SpellcraftWeaveMessage args)
    {
        var actor = args.Actor;
        var graph = args.Graph;

        if (graph.Nodes.Count is 0 or > MaxNodes || graph.Links.Count > MaxLinks)
            return;

        if (!TryComp<SpellcraftComponent>(actor, out var craft) || !Has(craft, args.Discipline))
            return;

        // The editing discipline must be one that builds spells; the output needs its own discipline.
        if (args.Discipline is not (SpellDiscipline.Glyphwork or SpellDiscipline.Sigil or SpellDiscipline.Circuit))
            return;

        var neededForOutput = args.Output switch
        {
            SpellOutput.Wand => SpellDiscipline.Wandwright,
            SpellOutput.Enchant or SpellOutput.Scroll => SpellDiscipline.Artifice,
            _ => args.Discipline,
        };
        if (!Has(craft, neededForOutput))
            return;

        string? error;
        switch (args.Output)
        {
            case SpellOutput.Action:
                if (!_spells.TryCreateSpell(actor, graph, args.Discipline, ignoreKnown: false, out _, out error))
                    Fail(actor, error);

                break;
            default:
                if (!_spells.TryCompile(actor, graph, args.Discipline, ignoreKnown: false, out var plan, out error)
                    || !TryPutInObject(ent, actor, args.Output, graph, plan, out error))
                    Fail(actor, error);

                break;
        }

        SendState(ent, actor);
    }

    private bool TryPutInObject(Entity<SpellweaverTomeComponent> tome,
        EntityUid actor,
        SpellOutput output,
        SpellGraphData graph,
        SpellGraphPlan plan,
        out string? error)
    {
        error = null;

        if (!SpellGraphSystem.IsStorable(plan))
        {
            error = Loc.GetString("spellcraft-error-not-storable");
            return false;
        }

        switch (output)
        {
            case SpellOutput.Wand:
            {
                if (FindWand(actor) is not { } wand)
                {
                    error = Loc.GetString("spellcraft-error-no-wand");
                    return false;
                }

                if (!CanAfford(actor, plan.Cost * WandBindFactor, out error)
                    || !_items.TryStore(wand, graph, plan, out error))
                    return false;

                _wind.TrySpend(actor, plan.Cost * WandBindFactor);
                return true;
            }
            case SpellOutput.Enchant:
            {
                if (FindEnchantable(actor, tome) is not { } target)
                {
                    error = Loc.GetString("spellcraft-error-no-enchant-target");
                    return false;
                }

                var cost = plan.Cost * EnchantFactor;
                if (!CanAfford(actor, cost, out error))
                    return false;

                var item = EnsureComp<SpellItemComponent>(target);
                item.Kind = SpellItemKind.Enchanted;
                item.Slots = 1;
                item.Charges = EnchantCharges;
                item.PayWind = false;
                if (!_items.TryStore((target, item), graph, plan, out error))
                {
                    RemComp<SpellItemComponent>(target);
                    return false;
                }

                _wind.TrySpend(actor, cost);
                return true;
            }
            case SpellOutput.Scroll:
            {
                if (FindPaper(actor, tome) is not { } paper)
                {
                    error = Loc.GetString("spellcraft-error-no-paper");
                    return false;
                }

                var cost = plan.Cost * ScrollFactor;
                if (!CanAfford(actor, cost, out error))
                    return false;

                var scroll = Spawn(_scrollProto, Transform(actor).Coordinates);
                if (!TryComp<SpellItemComponent>(scroll, out var item) || !_items.TryStore((scroll, item), graph, plan, out error))
                {
                    QueueDel(scroll);
                    return false;
                }

                _wind.TrySpend(actor, cost);
                QueueDel(paper);
                _hands.TryPickupAnyHand(actor, scroll);
                return true;
            }
        }

        return false;
    }

    private bool CanAfford(EntityUid actor, float cost, out string? error)
    {
        error = null;
        if (!TryComp<WindComponent>(actor, out var wind) || _wind.CanAfford((actor, wind), cost))
            return true;

        error = Loc.GetString("wind-not-enough");
        return false;
    }

    private Entity<SpellItemComponent>? FindWand(EntityUid actor)
    {
        foreach (var held in _hands.EnumerateHeld(actor))
        {
            if (TryComp<SpellItemComponent>(held, out var item) && item.Kind == SpellItemKind.Wand && item.Spells.Count < item.Slots)
                return (held, item);
        }

        return null;
    }

    private EntityUid? FindEnchantable(EntityUid actor, EntityUid tome)
    {
        foreach (var held in _hands.EnumerateHeld(actor))
        {
            if (held != tome && !HasComp<SpellItemComponent>(held) && !HasComp<SpellweaverTomeComponent>(held))
                return held;
        }

        return null;
    }

    private EntityUid? FindPaper(EntityUid actor, EntityUid tome)
    {
        foreach (var held in _hands.EnumerateHeld(actor))
        {
            if (held != tome && HasComp<PaperComponent>(held))
                return held;
        }

        return null;
    }

    // ---------------------------------------------------------------- rote, tarot, ritual

    private void OnRote(Entity<SpellweaverTomeComponent> ent, ref SpellcraftRoteMessage args)
    {
        var actor = args.Actor;
        if (!TryComp<SpellcraftComponent>(actor, out var craft) || !Has(craft, SpellDiscipline.Rote))
            return;

        if (!_proto.TryIndex<RoteSpellPrototype>(args.Rote, out var rote) || (!craft.Unrestricted && !craft.Rotes.Contains(rote.ID)))
            return;

        if (!SpellGraphCompiler.TryBuildChain(_proto, rote.Chain, out var graph, out var error)
            || !_spells.TryCreateSpell(actor, graph, SpellDiscipline.Rote, ignoreKnown: true, out _, out error))
            Fail(actor, error);

        SendState(ent, actor);
    }

    private void OnDrawCard(Entity<SpellweaverTomeComponent> ent, ref SpellcraftDrawCardMessage args)
    {
        var actor = args.Actor;
        if (!TryComp<SpellcraftComponent>(actor, out var craft) || !Has(craft, SpellDiscipline.Tarot))
            return;

        if (_nextDraw.TryGetValue(actor, out var next) && _timing.CurTime < next)
        {
            Fail(actor, Loc.GetString("tarot-draw-cooldown"));
            return;
        }

        if (!CanAfford(actor, TarotCost, out var error))
        {
            Fail(actor, error);
            return;
        }

        if (_tarot.DrawCard(actor) is { } card)
        {
            _wind.TrySpend(actor, TarotCost);
            _nextDraw[actor] = _timing.CurTime + _tarotCooldown;
            _popup.PopupEntity(Loc.GetString("tarot-drawn", ("card", Name(card))), actor, actor, PopupType.Medium);
        }

        SendState(ent, actor);
    }

    private void OnCircle(Entity<SpellweaverTomeComponent> ent, ref SpellcraftCircleMessage args)
    {
        _ritual.TryDrawCircle(args.Actor);
        SendState(ent, args.Actor);
    }

    private void OnRite(Entity<SpellweaverTomeComponent> ent, ref SpellcraftRiteMessage args)
    {
        _ritual.TryPerform(args.Actor, args.Ritual);
        SendState(ent, args.Actor);
    }

    // ---------------------------------------------------------------- forgetting

    private void OnForget(Entity<SpellweaverTomeComponent> ent, ref SpellcraftForgetMessage args)
    {
        var actor = args.Actor;
        var action = GetEntity(args.Action);

        // Only a spell's own holder may forget it, and only woven spells.
        if (!Exists(action)
            || !HasComp<SpellGraphActionComponent>(action)
            || !TryComp<ActionComponent>(action, out var comp)
            || comp.AttachedEntity != actor)
            return;

        _actions.RemoveAction(actor, action);
        QueueDel(action);
        SendState(ent, actor);
    }

    // ---------------------------------------------------------------- state

    private void SendState(Entity<SpellweaverTomeComponent> ent, EntityUid actor)
    {
        if (!TryComp<SpellcraftComponent>(actor, out var craft))
            return;

        var glyphs = craft.Unrestricted
            ? _proto.EnumeratePrototypes<SpellGlyphPrototype>().Select(g => g.ID).ToList()
            : craft.Glyphs.Select(g => g.Id).ToList();

        var disciplines = craft.Unrestricted
            ? Enum.GetValues<SpellDiscipline>().ToList()
            : craft.Disciplines.ToList();

        var rotes = craft.Unrestricted
            ? _proto.EnumeratePrototypes<RoteSpellPrototype>().Select(r => r.ID).ToList()
            : craft.Rotes.Select(r => r.Id).ToList();

        var wind = 0f;
        var windMax = 0f;
        if (TryComp<WindComponent>(actor, out var windComp))
        {
            wind = _wind.GetWind((actor, windComp));
            windMax = windComp.Max;
        }

        var spells = new List<WovenSpellInfo>();
        foreach (var action in _actions.GetActions(actor))
        {
            if (!HasComp<SpellGraphActionComponent>(action) || !TryComp<WindCostComponent>(action, out var cost))
                continue;

            spells.Add(new WovenSpellInfo(GetNetEntity(action.Owner), Name(action.Owner), cost.Cost));
        }

        string? wandName = null;
        var wandSlots = 0;
        List<string> wandSpells = [];
        foreach (var held in _hands.EnumerateHeld(actor))
        {
            if (!TryComp<SpellItemComponent>(held, out var item) || item.Kind != SpellItemKind.Wand)
                continue;

            wandName = Name(held);
            wandSlots = item.Slots;
            wandSpells = item.Spells.Select(s => s.Title ?? s.Name).ToList();
            break;
        }

        var enchantTarget = FindEnchantable(actor, ent.Owner) is { } target ? Name(target) : null;
        var rituals = Has(craft, SpellDiscipline.Ritual) ? _ritual.GetInfos(actor) : [];
        var circleNearby = _ritual.TryFindCircle(actor, out _);

        var state = new SpellcraftBuiState(glyphs,
            disciplines,
            craft.Schools.ToList(),
            rotes,
            craft.MaxNodes + (Has(craft, SpellDiscipline.Circuit) ? 2 : 0),
            craft.MaxSpells,
            wind,
            windMax,
            spells,
            wandName,
            wandSlots,
            wandSpells,
            enchantTarget,
            FindPaper(actor, ent.Owner) != null,
            TarotCost,
            CircleCost,
            circleNearby,
            rituals);
        _ui.SetUiState(ent.Owner, SpellcraftUiKey.Key, state);
    }
}
