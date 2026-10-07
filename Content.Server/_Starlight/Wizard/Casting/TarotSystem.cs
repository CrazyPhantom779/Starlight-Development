using Content.Server._Starlight.Wizard.Items;
using Content.Server._Starlight.Wizard.SpellGraph;
using Content.Shared._Starlight.Wizard.Casting;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared._Starlight.Wizard.SpellGraph;

namespace Content.Server._Starlight.Wizard.Casting;

/// <summary>Draws tarot cards. Each is a single-use spell, upright or (less often) reversed.</summary>
public sealed partial class TarotSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SpellGraphSystem _spells = default!;
    [Dependency] private SpellItemSystem _items = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    private const float ReversedChance = 0.3f;

    /// <summary>Draws a random card for a wizard and puts it in their hand (or at their feet).</summary>
    public EntityUid? DrawCard(EntityUid wizard)
    {
        var cards = new List<TarotCardPrototype>(_proto.EnumeratePrototypes<TarotCardPrototype>());
        if (cards.Count == 0)
            return null;

        var total = 0f;
        foreach (var candidate in cards)
            total += candidate.Weight;

        var roll = _random.NextFloat() * total;
        var card = cards[^1];
        foreach (var candidate in cards)
        {
            roll -= candidate.Weight;
            if (roll > 0f)
                continue;

            card = candidate;
            break;
        }

        var reversed = _random.Prob(ReversedChance);
        var chain = reversed ? card.Reversed : card.Upright;
        if (!SpellGraphCompiler.TryBuildChain(_proto, chain, out var graph, out _)
            || !_spells.TryCompile(wizard, graph, SpellDiscipline.Tarot, ignoreKnown: true, out var __, out _))
            return null;

        var item = Spawn(card.Entity, Transform(wizard).Coordinates);
        if (!TryComp<SpellItemComponent>(item, out var comp) || !_items.TryStore((item, comp), graph, __, out _))
        {
            QueueDel(item);
            return null;
        }

        var title = Loc.GetString(reversed ? "tarot-title-reversed" : "tarot-title-upright",
            ("numeral", card.Numeral),
            ("name", Loc.GetString(card.Name)));
        comp.Spells[0].Title = title;
        _meta.SetEntityName(item, title);

        _hands.TryPickupAnyHand(wizard, item);
        return item;
    }
}
