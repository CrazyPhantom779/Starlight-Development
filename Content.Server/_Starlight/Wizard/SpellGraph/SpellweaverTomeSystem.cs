using System.Linq;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Wizard.SpellGraph;

/// <summary>Server side of the spell-weaving window.</summary>
public sealed partial class SpellweaverTomeSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SpellGraphSystem _spells = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedWindSystem _wind = default!;

    // Hard cap on how many glyphs a client may send in one request.
    private const int MaxChainLength = 16;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SpellweaverTomeComponent>(SpellcraftUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<SpellcraftWeaveMessage>(OnWeave);
            subs.Event<SpellcraftForgetMessage>(OnForget);
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

    private void OnWeave(Entity<SpellweaverTomeComponent> ent, ref SpellcraftWeaveMessage args)
    {
        var actor = args.Actor;
        if (args.Chain.Count is 0 or > MaxChainLength)
            return;

        var chain = args.Chain.Select(id => new ProtoId<SpellGlyphPrototype>(id)).ToList();
        if (!SpellGraphCompiler.TryBuildChain(_proto, chain, out var graph, out var error)
            || !_spells.TryCreateSpell(actor, graph, out _, out error))
        {
            _popup.PopupEntity(error ?? Loc.GetString("spellcraft-error-no-actions"), actor, actor);
            return;
        }

        SendState(ent, actor);
    }

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

    private void SendState(Entity<SpellweaverTomeComponent> ent, EntityUid actor)
    {
        if (!TryComp<SpellcraftComponent>(actor, out var craft))
            return;

        var glyphs = craft.Unrestricted
            ? _proto.EnumeratePrototypes<SpellGlyphPrototype>().Select(g => g.ID).ToList()
            : craft.Glyphs.Select(g => g.Id).ToList();

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

        var state = new SpellcraftBuiState(glyphs, craft.MaxNodes, craft.MaxSpells, wind, windMax, spells);
        _ui.SetUiState(ent.Owner, SpellcraftUiKey.Key, state);
    }
}
