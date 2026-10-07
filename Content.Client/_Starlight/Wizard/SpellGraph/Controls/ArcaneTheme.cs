using Content.Shared._Starlight.Wizard.SpellGraph;
using Robust.Client.Graphics;

namespace Content.Client._Starlight.Wizard.SpellGraph.Controls;

/// <summary>Colours and style boxes shared by every control in the spellweaving window.</summary>
public static class ArcaneTheme
{
    public static readonly Color Background = Color.FromHex("#120d26");
    public static readonly Color Panel = Color.FromHex("#1c1640");
    public static readonly Color PanelRaised = Color.FromHex("#2a2261");
    public static readonly Color PanelHover = Color.FromHex("#3a3085");
    public static readonly Color Border = Color.FromHex("#4e3f9c");
    public static readonly Color Gold = Color.FromHex("#e0b84c");
    public static readonly Color Teal = Color.FromHex("#5fd1d1");
    public static readonly Color Text = Color.FromHex("#ece6ff");
    public static readonly Color TextDim = Color.FromHex("#9d93cf");
    public static readonly Color Good = Color.FromHex("#7be08a");
    public static readonly Color Bad = Color.FromHex("#ff6b78");
    public static readonly Color WindFill = Color.FromHex("#6f8dff");

    private static readonly Dictionary<string, Color> _schools = new()
    {
        ["Fire"] = Color.FromHex("#ff6a3d"),
        ["Ice"] = Color.FromHex("#7fd6ff"),
        ["Lightning"] = Color.FromHex("#ffe14d"),
        ["Holy"] = Color.FromHex("#fff0a8"),
        ["Evocation"] = Color.FromHex("#c18cff"),
        ["Ender"] = Color.FromHex("#8a6bff"),
        ["Nature"] = Color.FromHex("#6ed16e"),
        ["Blood"] = Color.FromHex("#e0475a"),
        ["Eldritch"] = Color.FromHex("#4fd0c0"),
    };

    public static Color SchoolColor(string school)
        => _schools.TryGetValue(school, out var color) ? color : Text;

    /// <summary>The accent colour of a glyph: gold for Forms, teal for Augments, its school's colour for Effects.</summary>
    public static Color GlyphColor(SpellGlyphPrototype glyph)
        => glyph.Category switch
        {
        GlyphCategory.Form => Gold,
        GlyphCategory.Augment => Teal,
        _ => glyph.Schools.Count > 0 ? SchoolColor(glyph.Schools[0]) : Text,
    };

    public static StyleBoxFlat Box(Color fill, Color border, float thickness = 1f, float margin = 6f)
        => new()
        {
        BackgroundColor = fill,
        BorderColor = border,
        BorderThickness = new Thickness(thickness),
        ContentMarginLeftOverride = margin,
        ContentMarginRightOverride = margin,
        ContentMarginTopOverride = margin,
        ContentMarginBottomOverride = margin,
    };
}
