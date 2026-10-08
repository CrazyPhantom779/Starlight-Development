using System.Diagnostics.CodeAnalysis;
using Content.Server._Starlight.Wizard.Casting;
using Content.Server._Starlight.Wizard.SpellGraph;
using Content.Server._Starlight.Wizard.Wind;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Wind;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using SpellGraphData = Content.Shared._Starlight.Wizard.SpellGraph.SpellGraph;

namespace Content.Server._Starlight.Wizard.Items;

/// <summary>
/// Wands, enchanted objects, scrolls and tarot cards. Click with one to cast its active spell; use it in hand to
/// cycle spells (or to cast it, if it is a self-centred spell and the only one inside).
/// </summary>
public sealed partial class SpellItemSystem : EntitySystem
{
    [Dependency] private SpellGraphSystem _spells = default!;
    [Dependency] private WindSystem _wind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// Stores a spell in an item. Fails if the item is full or the spell can only be cast directly.
    /// </summary>
    public bool TryStore(Entity<SpellItemComponent> item,
        SpellGraphData graph,
        SpellGraphPlan plan,
        [NotNullWhen(false)] out string? error)
    {
        if (!SpellGraphSystem.IsStorable(plan))
        {
            error = Loc.GetString("spellcraft-error-not-storable");
            return false;
        }

        if (item.Comp.Spells.Count >= item.Comp.Slots)
        {
            error = Loc.GetString("spellcraft-error-item-full");
            return false;
        }

        item.Comp.Spells.Add(new StoredSpell(graph, plan));
        item.Comp.Active = item.Comp.Spells.Count - 1;
        error = null;
        return true;
    }

    [SubscribeLocalEvent]
    private void OnAfterInteract(Entity<SpellItemComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryCastActive(ent, args.User, args.ClickLocation, args.Target);
    }

    [SubscribeLocalEvent]
    private void OnUseInHand(Entity<SpellItemComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Spells.Count == 0)
            return;

        var active = ent.Comp.Spells[Math.Clamp(ent.Comp.Active, 0, ent.Comp.Spells.Count - 1)];

        // A lone self-centred spell has nothing to aim, so using the item casts it.
        if (ent.Comp.Spells.Count == 1 && active.Plan.TargetMode == SpellTargetMode.Self)
        {
            args.Handled = TryCastActive(ent, args.User, default, null);
            return;
        }

        if (ent.Comp.Spells.Count < 2)
            return;

        ent.Comp.Active = (ent.Comp.Active + 1) % ent.Comp.Spells.Count;
        var next = ent.Comp.Spells[ent.Comp.Active];
        _popup.PopupEntity(Loc.GetString("spellitem-active", ("spell", next.Title ?? next.Name)), ent, args.User);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<SpellItemComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Spells.Count == 0)
        {
            args.PushMarkup(Loc.GetString("spellitem-empty"));
            return;
        }

        for (var i = 0; i < ent.Comp.Spells.Count; i++)
        {
            var spell = ent.Comp.Spells[i];
            var key = i == ent.Comp.Active ? "spellitem-line-active" : "spellitem-line";
            args.PushMarkup(Loc.GetString(key, ("spell", spell.Title ?? spell.Name), ("cost", spell.Plan.Cost)));
        }

        if (ent.Comp.Charges is { } charges)
            args.PushMarkup(Loc.GetString("spellitem-charges", ("charges", charges)));
    }

    private bool TryCastActive(Entity<SpellItemComponent> ent, EntityUid user, EntityCoordinates target, EntityUid? targetEntity)
    {
        var comp = ent.Comp;
        if (comp.Spells.Count == 0)
            return false;

        if (_timing.CurTime < comp.NextUse)
            return true;

        var spell = comp.Spells[Math.Clamp(comp.Active, 0, comp.Spells.Count - 1)];
        var plan = spell.Plan;

        // Aimed spells need a spot to aim at.
        if (plan.TargetMode == SpellTargetMode.World && target == default)
            return false;

        if (comp.PayWind && TryComp<WindComponent>(user, out var wind) && !_wind.CanAfford((user, wind), plan.Cost))
        {
            _popup.PopupEntity(Loc.GetString("wind-not-enough"), user, user);
            return true;
        }

        if (!_spells.TryCastPlan(plan, user, target, targetEntity))
            return true;

        if (comp.PayWind)
            _wind.TrySpend(user, plan.Cost);

        comp.NextUse = _timing.CurTime + TimeSpan.FromSeconds(comp.CooldownSeconds);

        if (comp.Charges is { } charges)
        {
            comp.Charges = charges - 1;
            if (comp.Charges <= 0 && comp.ConsumeWhenEmpty)
            {
                _popup.PopupEntity(Loc.GetString("spellitem-spent", ("item", Name(ent))), user, user);

                if (comp.Kind is SpellItemKind.Card or SpellItemKind.Scroll)
                {
                    var used = new SpellItemUsedEvent(user, comp.Kind == SpellItemKind.Card);
                    RaiseLocalEvent(user, ref used);
                }

                QueueDel(ent);
            }
        }

        return true;
    }
}
