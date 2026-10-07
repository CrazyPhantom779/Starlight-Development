using Content.Shared._Starlight.Wizard.SpellGraph;
using SpellGraphData = Content.Shared._Starlight.Wizard.SpellGraph.SpellGraph;

namespace Content.Server._Starlight.Wizard.Items;

public enum SpellItemKind : byte
{
    /// <summary>A reusable wand. Casting costs the holder Wind.</summary>
    Wand,

    /// <summary>An ordinary object enchanted with a spell. Limited charges, paid for when enchanted.</summary>
    Enchanted,

    /// <summary>A paper scroll inscribed with a spell. One use.</summary>
    Scroll,

    /// <summary>A tarot card. One use.</summary>
    Card,
}

/// <summary>A spell kept inside an item.</summary>
public sealed class StoredSpell(SpellGraphData graph, SpellGraphPlan plan)
{
    public readonly SpellGraphData Graph = graph;
    public readonly SpellGraphPlan Plan = plan;
    public string Name = plan.Name;
    public string? Title;
}

/// <summary>
/// An item that holds one or more woven spells and casts them when used. This one component backs wands,
/// enchanted objects, scrolls and tarot cards: they differ only in slots, charges and who pays for the cast.
/// </summary>
[RegisterComponent]
public sealed partial class SpellItemComponent : Component
{
    [DataField]
    public SpellItemKind Kind = SpellItemKind.Wand;

    /// <summary>How many spells can be stored.</summary>
    [DataField]
    public int Slots = 3;

    /// <summary>Remaining uses. Null means unlimited.</summary>
    [DataField]
    public int? Charges;

    /// <summary>Whether each cast costs the holder Wind (wands do).</summary>
    [DataField]
    public bool PayWind;

    /// <summary>Delete the item when its last charge is used.</summary>
    [DataField]
    public bool ConsumeWhenEmpty;

    [DataField]
    public float CooldownSeconds = 1.5f;

    [ViewVariables]
    public List<StoredSpell> Spells = [];

    [ViewVariables]
    public int Active;

    [ViewVariables]
    public TimeSpan NextUse;
}
