using Content.Shared._Starlight.Wizard.Casting;
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

    /// <summary>Schools this caster is attuned to. Glyphs of an attuned school cost less.</summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> Schools = [];

    /// <summary>
    /// This caster's personal tides: a cost multiplier per school that drifts over the round.
    /// A school running hot costs more, one running cool costs less.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, float> Tides = [];

    /// <summary>How much mastery the caster has earned from errands. Each point makes them a little stronger.</summary>
    [DataField, AutoNetworkedField]
    public int Power;

    /// <summary>Ways of working magic this caster has mastered.</summary>
    [DataField, AutoNetworkedField]
    public HashSet<SpellDiscipline> Disciplines = [SpellDiscipline.Rote, SpellDiscipline.Glyphwork];

    /// <summary>Prepared spells this caster knows.</summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<RoteSpellPrototype>> Rotes = [];

    /// <summary>Admin/debug: ignore <see cref="Glyphs"/>, <see cref="Disciplines"/> and <see cref="Rotes"/> and allow everything.</summary>
    [DataField, AutoNetworkedField]
    public bool Unrestricted;
}
