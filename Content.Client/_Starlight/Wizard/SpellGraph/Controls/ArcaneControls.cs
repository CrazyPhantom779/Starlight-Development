using System.Numerics;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.Wizard.SpellGraph.Controls;

/// <summary>A framed panel in the arcane palette.</summary>
public sealed class ArcanePanel : PanelContainer
{
    public ArcanePanel(bool raised = false, Color? border = null, float margin = 6f)
        => PanelOverride = ArcaneTheme.Box(raised
        ? ArcaneTheme.PanelRaised
        : ArcaneTheme.Panel, border
        ?? ArcaneTheme.Border, 1f, margin);
}

/// <summary>A themed push button. Can also act as a toggle tab.</summary>
public sealed class ArcaneButton : BaseButton
{
    private readonly Label _label;

    public Color Accent = ArcaneTheme.Gold;

    public string Text
    {
        get => _label.Text ?? string.Empty;
        set => _label.Text = value;
    }

    public ArcaneButton(string text, bool toggle = false)
    {
        ToggleMode = toggle;
        MinSize = new Vector2(70, 28);
        _label = new Label
        {
            Text = text,
            Align = Label.AlignMode.Center,
            VAlign = Label.VAlignMode.Center,
            HorizontalExpand = true,
            VerticalExpand = true,
            FontColorOverride = ArcaneTheme.Text,
            Margin = new Thickness(8, 2),
        };
        AddChild(_label);
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        _label.FontColorOverride = DrawMode == DrawModeEnum.Disabled ? ArcaneTheme.TextDim : ArcaneTheme.Text;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var box = new UIBox2(0, 0, PixelWidth, PixelHeight);
        var selected = ToggleMode && Pressed;
        var fill = DrawMode switch
        {
            DrawModeEnum.Disabled => ArcaneTheme.Background,
            DrawModeEnum.Pressed => ArcaneTheme.PanelHover,
            DrawModeEnum.Hover => ArcaneTheme.PanelHover,
            _ => selected ? ArcaneTheme.PanelHover : ArcaneTheme.PanelRaised,
        };
        handle.DrawRect(box, fill);
        handle.DrawRect(box, DrawMode == DrawModeEnum.Disabled ? ArcaneTheme.Border : (selected ? Accent : ArcaneTheme.Border), false);
        if (selected)
            handle.DrawRect(new UIBox2(0, PixelHeight - 3, PixelWidth, PixelHeight), Accent);

        base.Draw(handle);
    }
}

/// <summary>The Wind meter.</summary>
public sealed class WindBar : Control
{
    private readonly Label _label = new() { Align = Label.AlignMode.Center, VAlign = Label.VAlignMode.Center, FontColorOverride = ArcaneTheme.Text };
    public float Value;
    public float Max = 100f;

    public WindBar()
    {
        MinSize = new Vector2(220, 22);
        AddChild(_label);
    }

    public void Set(float value, float max)
    {
        Value = value;
        Max = max;
        _label.Text = Loc.GetString("spellcraft-ui-wind", ("wind", MathF.Round(value)), ("max", MathF.Round(max)));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var box = new UIBox2(0, 0, PixelWidth, PixelHeight);
        handle.DrawRect(box, ArcaneTheme.Background);

        var fraction = Max <= 0f ? 0f : Math.Clamp(Value / Max, 0f, 1f);
        var fill = new UIBox2(2, 2, 2 + ((PixelWidth - 4) * fraction), PixelHeight - 2);
        handle.DrawRect(fill, Value < 0f ? ArcaneTheme.Bad : ArcaneTheme.WindFill);
        handle.DrawRect(box, ArcaneTheme.Border, false);
        base.Draw(handle);
    }
}

/// <summary>A glyph as a clickable tile: its sigil icon, name and cost, framed in its school's colour.</summary>
public sealed class GlyphTile : BaseButton
{
    public readonly SpellGlyphPrototype Glyph;
    private readonly Color _accent;

    public GlyphTile(SpellGlyphPrototype glyph, Texture? icon, bool compact = false)
    {
        Glyph = glyph;
        _accent = ArcaneTheme.GlyphColor(glyph);
        MinSize = compact ? new Vector2(78, 66) : new Vector2(94, 84);

        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(3), SeparationOverride = 1 };
        column.AddChild(new TextureRect
        {
            Texture = icon,
            Stretch = TextureRect.StretchMode.KeepCentered,
            MinSize = compact ? new Vector2(40, 40) : new Vector2(48, 48),
            HorizontalAlignment = HAlignment.Center,
        });
        column.AddChild(new Label
        {
            Text = Loc.GetString(glyph.Name),
            Align = Label.AlignMode.Center,
            ClipText = true,
            FontColorOverride = ArcaneTheme.Text,
        });
        if (!compact)
        {
            column.AddChild(new Label
            {
                Text = $"{glyph.Cost:0.#}",
                Align = Label.AlignMode.Center,
                FontColorOverride = ArcaneTheme.TextDim,
            });
        }

        AddChild(column);

        if (glyph.Description is { } description)
            ToolTip = Loc.GetString(description);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var box = new UIBox2(0, 0, PixelWidth, PixelHeight);
        handle.DrawRect(box, DrawMode is DrawModeEnum.Hover or DrawModeEnum.Pressed ? ArcaneTheme.PanelHover : ArcaneTheme.PanelRaised);
        handle.DrawRect(box, _accent, false);
        handle.DrawRect(new UIBox2(0, 0, PixelWidth, 3), _accent);
        base.Draw(handle);
    }
}

/// <summary>Small helpers for building the window out of themed pieces.</summary>
public static class ArcaneUi
{
    public static Label Heading(string text)
        => new() { Text = text, FontColorOverride = ArcaneTheme.Gold, Margin = new Thickness(0, 4, 0, 2) };

    public static Label Dim(string text)
        => new() { Text = text, FontColorOverride = ArcaneTheme.TextDim, ClipText = false };

    public static Texture? IconOf(SpriteSystem sprites, SpellGlyphPrototype glyph)
        => glyph.Icon is { } icon ? sprites.Frame0(icon) : null;

    public static BoxContainer Row(int separation = 6)
        => new() { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = separation };

    public static BoxContainer Column(int separation = 4)
        => new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = separation };
}
