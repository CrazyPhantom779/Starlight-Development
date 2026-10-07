using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Wizard.SpellGraph;

/// <summary>
/// A 3x3 grid of dots. Hold the mouse button and drag across adjacent dots to draw a pattern; release to cast the
/// glyph whose sigil matches. Patterns can be drawn in either direction. Unknown or unlearned patterns fizzle.
/// </summary>
public sealed class SigilPad : Control
{
    private const float DotRadius = 7f;
    private const float HitRadius = 24f;
    private const float Spacing = 74f;
    private new const float Margin = 32f;
    private const float FlashSeconds = 0.5f;

    private readonly Dictionary<string, string> _byPattern = [];
    private readonly List<int> _path = [];
    private bool _drawing;
    private Vector2 _cursor;
    private float _flash;
    private bool _flashGood;

    public event Action<string>? OnGlyph;
    public event Action? OnMisdraw;

    public SigilPad()
    {
        MinSize = new Vector2((Margin * 2) + (Spacing * 2), (Margin * 2) + (Spacing * 2));
        SetSize = MinSize;

        // A plain Control ignores the mouse by default, which would let the window's own drag-to-move take the click.
        MouseFilter = MouseFilterMode.Stop;
    }

    /// <summary>Sets which glyphs the wizard knows, and therefore which patterns are recognised.</summary>
    public void SetKnown(IEnumerable<SpellGlyphPrototype> glyphs)
    {
        _byPattern.Clear();
        foreach (var glyph in glyphs)
        {
            if (glyph.Sigil is { } sigil)
                _byPattern[Canonical([.. sigil.Split('-').Select(int.Parse)])] = glyph.ID;
        }
    }

    private static string Canonical(List<int> path)
    {
        var forward = string.Join('-', path);
        var back = string.Join('-', Enumerable.Reverse(path));
        return string.CompareOrdinal(forward, back) <= 0 ? forward : back;
    }

    private static Vector2 DotPosition(int index)
        => new(Margin + (index % 3 * Spacing), Margin + (index / 3 * Spacing));

    private static bool Adjacent(int a, int b)
        => a != b && Math.Abs((a / 3) - (b / 3)) <= 1 && Math.Abs((a % 3) - (b % 3)) <= 1;

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _drawing = true;
        _path.Clear();
        _cursor = args.RelativePosition;
        TryAddDot(_cursor);
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!_drawing)
            return;

        _cursor = args.RelativePosition;
        TryAddDot(_cursor);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || !_drawing)
            return;

        _drawing = false;
        Finish();
        args.Handle();
    }

    private void TryAddDot(Vector2 position)
    {
        for (var i = 0; i < 9; i++)
        {
            if ((DotPosition(i) - position).Length() > HitRadius || _path.Contains(i))
                continue;

            if (_path.Count == 0 || Adjacent(_path[^1], i))
                _path.Add(i);

            return;
        }
    }

    private void Finish()
    {
        _flash = FlashSeconds;
        if (_path.Count >= 3 && _byPattern.TryGetValue(Canonical(_path), out var glyph))
        {
            _flashGood = true;
            OnGlyph?.Invoke(glyph);
        }
        else
        {
            _flashGood = false;
            if (_path.Count > 0)
                OnMisdraw?.Invoke();
        }

        // Keep the drawn path visible while it flashes.
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_flash > 0f)
        {
            _flash -= args.DeltaSeconds;
            if (_flash <= 0f && !_drawing)
                _path.Clear();
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var box = new UIBox2(0, 0, PixelWidth, PixelHeight);
        handle.DrawRect(box, Controls.ArcaneTheme.Background);
        handle.DrawRect(box, Controls.ArcaneTheme.Border, false);

        var lineColor = _flash > 0f ? (_flashGood ? Controls.ArcaneTheme.Good : Controls.ArcaneTheme.Bad) : Controls.ArcaneTheme.Gold;
        for (var i = 1; i < _path.Count; i++)
            handle.DrawLine(DotPosition(_path[i - 1]), DotPosition(_path[i]), lineColor);

        if (_drawing && _path.Count > 0)
            handle.DrawLine(DotPosition(_path[^1]), _cursor, Controls.ArcaneTheme.TextDim);

        for (var i = 0; i < 9; i++)
        {
            var onPath = _path.Contains(i);
            handle.DrawCircle(DotPosition(i), DotRadius, onPath ? lineColor : Controls.ArcaneTheme.Border);
        }
    }
}
