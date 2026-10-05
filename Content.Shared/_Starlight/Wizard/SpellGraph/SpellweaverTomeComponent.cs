using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

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
    /// <summary>Debug/admin tome: the user may use every glyph.</summary>
    [DataField]
    public bool GrantAll;

    /// <summary>Glyphs taught to the user when they open the tome.</summary>
    [DataField]
    public List<ProtoId<SpellGlyphPrototype>> Glyphs = new();

    /// <summary>Node limit given to a user who has none yet.</summary>
    [DataField]
    public int MaxNodes = 6;
}

[Serializable, NetSerializable]
public sealed class WovenSpellInfo(NetEntity action, string name, float cost)
{
    public readonly NetEntity Action = action;
    public readonly string Name = name;
    public readonly float Cost = cost;
}

[Serializable, NetSerializable]
public sealed class SpellcraftBuiState(
    List<string> glyphs,
    int maxNodes,
    int maxSpells,
    float wind,
    float windMax,
    List<WovenSpellInfo> spells) : BoundUserInterfaceState
{
    public readonly List<string> Glyphs = glyphs;
    public readonly int MaxNodes = maxNodes;
    public readonly int MaxSpells = maxSpells;
    public readonly float Wind = wind;
    public readonly float WindMax = windMax;
    public readonly List<WovenSpellInfo> Spells = spells;
}

/// <summary>Client asks to weave a spell from an ordered glyph chain.</summary>
[Serializable, NetSerializable]
public sealed class SpellcraftWeaveMessage(List<string> chain) : BoundUserInterfaceMessage
{
    public readonly List<string> Chain = chain;
}

/// <summary>Client asks to forget one of its woven spells.</summary>
[Serializable, NetSerializable]
public sealed class SpellcraftForgetMessage(NetEntity action) : BoundUserInterfaceMessage
{
    public readonly NetEntity Action = action;
}
