using Content.Shared._Starlight.Wizard.Casting;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using SpellGraphData = Content.Shared._Starlight.Wizard.SpellGraph.SpellGraph;

namespace Content.Shared._Starlight.Wizard.SpellGraph;

[Serializable, NetSerializable]
public enum SpellcraftUiKey : byte
{
    Key,
}

/// <summary>
/// An item that opens the spell-weaving window. Opening it gives the user <see cref="SpellcraftComponent"/> and Wind.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SpellweaverTomeComponent : Component
{
    /// <summary>Debug/admin tome: the user may use every glyph, discipline and prepared spell.</summary>
    [DataField]
    public bool GrantAll;

    /// <summary>Glyphs taught to the user when they open the tome.</summary>
    [DataField]
    public List<ProtoId<SpellGlyphPrototype>> Glyphs = [];

    /// <summary>Node limit given to a user who has none yet.</summary>
    [DataField]
    public int MaxNodes = 6;
}

/// <summary>Where a woven spell is put when it is finished.</summary>
[Serializable, NetSerializable]
public enum SpellOutput : byte
{
    /// <summary>An action on the caster's own action bar.</summary>
    Action,

    /// <summary>A free slot in a wand they hold.</summary>
    Wand,

    /// <summary>An ordinary object they hold, enchanted.</summary>
    Enchant,

    /// <summary>A sheet of paper they hold, turned into a scroll.</summary>
    Scroll,
}

[Serializable, NetSerializable]
public sealed class WovenSpellInfo(NetEntity action, string name, float cost)
{
    public readonly NetEntity Action = action;
    public readonly string Name = name;
    public readonly float Cost = cost;
}

/// <summary>Everything the spellweaving window needs to draw itself.</summary>
[Serializable, NetSerializable]
public sealed class SpellcraftBuiState(
    List<string> glyphs,
    List<SpellDiscipline> disciplines,
    List<string> schools,
    List<string> rotes,
    int maxNodes,
    int maxSpells,
    float wind,
    float windMax,
    List<WovenSpellInfo> spells,
    string? wandName,
    int wandSlots,
    List<string> wandSpells,
    string? enchantTarget,
    bool hasPaper,
    float tarotCost,
    float circleCost,
    bool circleNearby,
    List<RitualInfo> rituals,
    Dictionary<string, float> tides,
    float regen,
    bool hurt,
    int power,
    int maxPower,
    List<ErrandInfo> errands) : BoundUserInterfaceState
{
    public readonly List<string> Glyphs = glyphs;
    public readonly List<SpellDiscipline> Disciplines = disciplines;
    public readonly List<string> Schools = schools;
    public readonly List<string> Rotes = rotes;
    public readonly int MaxNodes = maxNodes;
    public readonly int MaxSpells = maxSpells;
    public readonly float Wind = wind;
    public readonly float WindMax = windMax;
    public readonly List<WovenSpellInfo> Spells = spells;

    // Wandwright
    public readonly string? WandName = wandName;
    public readonly int WandSlots = wandSlots;
    public readonly List<string> WandSpells = wandSpells;

    // Artifice
    public readonly string? EnchantTarget = enchantTarget;
    public readonly bool HasPaper = hasPaper;

    // Tarot and Ritual
    public readonly float TarotCost = tarotCost;
    public readonly float CircleCost = circleCost;
    public readonly bool CircleNearby = circleNearby;
    public readonly List<RitualInfo> Rituals = rituals;

    // Randomisation and growth
    public readonly Dictionary<string, float> Tides = tides;
    public readonly float Regen = regen;
    public readonly bool Hurt = hurt;
    public readonly int Power = power;
    public readonly int MaxPower = maxPower;
    public readonly List<ErrandInfo> Errands = errands;
}

/// <summary>Weave a graph with a given discipline and put the result somewhere.</summary>
[Serializable, NetSerializable]
public sealed class SpellcraftWeaveMessage(SpellGraphData graph, SpellDiscipline discipline, SpellOutput output)
    : BoundUserInterfaceMessage
{
    public readonly SpellGraphData Graph = graph;
    public readonly SpellDiscipline Discipline = discipline;
    public readonly SpellOutput Output = output;
}

/// <summary>Cast a prepared spell from memory (Rote).</summary>
[Serializable, NetSerializable]
public sealed class SpellcraftRoteMessage(string rote) : BoundUserInterfaceMessage
{
    public readonly string Rote = rote;
}

/// <summary>Client asks to forget one of its woven spells.</summary>
[Serializable, NetSerializable]
public sealed class SpellcraftForgetMessage(NetEntity action) : BoundUserInterfaceMessage
{
    public readonly NetEntity Action = action;
}

[Serializable, NetSerializable]
public sealed class SpellcraftDrawCardMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SpellcraftCircleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SpellcraftRiteMessage(string ritual) : BoundUserInterfaceMessage
{
    public readonly string Ritual = ritual;
}

[Serializable, NetSerializable]
public sealed class SpellcraftRefreshMessage : BoundUserInterfaceMessage;
