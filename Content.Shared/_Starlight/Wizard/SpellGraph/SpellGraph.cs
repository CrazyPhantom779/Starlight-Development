using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Wizard.SpellGraph;

/// <summary>
/// The data model every spell editor produces. Editors differ in how they present it, not in what it is.
/// A valid graph has exactly one Form node. Links go Form -> Effect, Form -> Augment or Effect -> Augment.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class SpellGraph
{
    [DataField]
    public List<SpellNode> Nodes = [];

    [DataField]
    public List<SpellLink> Links = [];
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SpellNode
{
    [DataField]
    public int Id;

    [DataField]
    public ProtoId<SpellGlyphPrototype> Glyph;

    public SpellNode()
    {
    }

    public SpellNode(int id, ProtoId<SpellGlyphPrototype> glyph)
    {
        Id = id;
        Glyph = glyph;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SpellLink
{
    [DataField]
    public int From;

    [DataField]
    public int To;

    public SpellLink()
    {
    }

    public SpellLink(int from, int to)
    {
        From = from;
        To = to;
    }
}
