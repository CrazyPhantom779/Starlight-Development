using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Wizard.Casting;

/// <summary>
/// A way of working magic. Every discipline produces the same <c>SpellGraph</c> through a different editor,
/// and each one stores the result differently.
/// </summary>
[Serializable, NetSerializable]
public enum SpellDiscipline : byte
{
    /// <summary>Cast prepared spells you already know.</summary>
    Rote,

    /// <summary>Slot a chain of glyphs together.</summary>
    Glyphwork,

    /// <summary>Draw each glyph's pattern by hand.</summary>
    Sigil,

    /// <summary>Wire glyph nodes together on a canvas.</summary>
    Circuit,

    /// <summary>Bind spells into wands.</summary>
    Wandwright,

    /// <summary>Enchant everyday objects with spells.</summary>
    Artifice,

    /// <summary>Draw cards that hold single-use spells.</summary>
    Tarot,

    /// <summary>Draw circles, make offerings and perform rites.</summary>
    Ritual,
}
