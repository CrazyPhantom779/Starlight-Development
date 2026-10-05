using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Wizard.SpellGraph;

/// <summary>
/// Marks an entity as able to weave its own spells, and records what it may use.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpellcraftComponent : Component
{
    /// <summary>Glyphs this caster has learned.</summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<SpellGlyphPrototype>> Glyphs = [];

    /// <summary>Max nodes in a single spell. The main complexity lever.</summary>
    [DataField, AutoNetworkedField]
    public int MaxNodes = 5;

    /// <summary>Max woven spells held at once.</summary>
    [DataField, AutoNetworkedField]
    public int MaxSpells = 5;

    /// <summary>Admin/debug: ignore <see cref="Glyphs"/> and allow every glyph.</summary>
    [DataField, AutoNetworkedField]
    public bool Unrestricted;
}
