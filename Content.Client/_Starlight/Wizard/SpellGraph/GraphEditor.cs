using System.Linq;
using System.Numerics;
using Content.Client._Starlight.Wizard.SpellGraph.Controls;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using SpellGraphData = Content.Shared._Starlight.Wizard.SpellGraph.SpellGraph;
using Content.Shared._Starlight.Wizard.Casting;

namespace Content.Client._Starlight.Wizard.SpellGraph;

/// <summary>
/// The Circuit editor: glyphs are nodes on a canvas, joined by wires. Drag nodes to move them, drag from a node's
/// right-hand port onto another node's left-hand port to wire them, right-click a node or a wire to remove it.
/// Unlike the chain editors this edits the real graph, so an Augment can be wired to one Effect or to the whole Form.
/// </summary>
public sealed partial class GraphEditor : BoxContainer
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IEntityManager _entMan = default!;

    private readonly SpellcraftWindow _window;
    private readonly GraphCanvas _canvas;
    private readonly BoxContainer _palette = ArcaneUi.Column(4);
    private readonly KeptScroll _paletteScroll = new() { MinWidth = 180 };
    private readonly Label _preview = new() { FontColorOverride = ArcaneTheme.Text };
    private readonly Label _error = new() { FontColorOverride = ArcaneTheme.Bad };
    private readonly ArcaneButton _weave;
    private SpellcraftBuiState? _state;
    private string _signature = string.Empty;

    public GraphEditor(SpellcraftWindow window)
    {
        IoCManager.InjectDependencies(this);
        _window = window;

        Orientation = LayoutOrientation.Vertical;
        VerticalExpand = true;
        HorizontalExpand = true;
        SeparationOverride = 6;

        _canvas = new GraphCanvas(_proto, _entMan.System<SpriteSystem>());
        _canvas.Changed += Refresh;

        var body = ArcaneUi.Row(8);
        body.VerticalExpand = true;
        var side = ArcaneUi.Column(4);
        side.AddChild(ArcaneUi.Dim(Loc.GetString("spellcraft-ui-circuit-help")));
        _paletteScroll.AddChild(_palette);
        side.AddChild(_paletteScroll);
        body.AddChild(side);
        body.AddChild(new ArcanePanel(margin: 2f) { HorizontalExpand = true, VerticalExpand = true, Children = { _canvas } });
        AddChild(body);

        AddChild(_preview);
        AddChild(_error);

        var buttons = ArcaneUi.Row(6);
        _weave = new ArcaneButton(Loc.GetString("spellcraft-ui-output-action")) { Disabled = true };
        _weave.OnPressed += _ => _window.Weave(_canvas.ToGraph(), SpellDiscipline.Circuit, SpellOutput.Action);
        buttons.AddChild(_weave);

        var clear = new ArcaneButton(Loc.GetString("spellcraft-ui-clear"));
        clear.OnPressed += _ => _canvas.Clear();
        buttons.AddChild(clear);
        AddChild(buttons);
    }

    private SpriteSystem _sprites => field ??= _entMan.System<SpriteSystem>();

    public void UpdateState(SpellcraftBuiState state)
    {
        _state = state;

        var signature = string.Join(',', state.Glyphs);
        if (signature != _signature)
        {
            _signature = signature;
            _paletteScroll.Rebuild(() => BuildPalette(state));
        }

        Refresh();
    }

    private void BuildPalette(SpellcraftBuiState state)
    {
        _palette.RemoveAllChildren();

        var glyphs = state.Glyphs
            .Select(id => _proto.TryIndex<SpellGlyphPrototype>(id, out var g) ? g : null)
            .Where(g => g != null)
            .Select(g => g!)
            .ToList();

        foreach (var category in new[] { GlyphCategory.Form, GlyphCategory.Effect, GlyphCategory.Augment })
        {
            _palette.AddChild(ArcaneUi.Heading(Loc.GetString($"spellcraft-ui-category-{category.ToString().ToLowerInvariant()}")));
            foreach (var glyph in glyphs.Where(g => g.Category == category).OrderBy(g => Loc.GetString(g.Name)))
            {
                var button = new ArcaneButton(Loc.GetString(glyph.Name)) { Accent = ArcaneTheme.GlyphColor(glyph), HorizontalExpand = true, MinSize = new Vector2(150, 24) };
                if (glyph.Description is { } desc)
                    button.ToolTip = Loc.GetString(desc);

                var captured = glyph;
                button.OnPressed += _ => _canvas.AddNode(captured);
                _palette.AddChild(button);
            }
        }
    }

    private void Refresh()
    {
        _preview.Text = string.Empty;
        _error.Text = string.Empty;
        _weave.Disabled = true;

        if (_state == null || _canvas.NodeCount == 0)
            return;

        var known = _state.Glyphs.Select(id => new ProtoId<SpellGlyphPrototype>(id)).ToList();
        if (!SpellGraphCompiler.TryCompile(_proto, _canvas.ToGraph(), _state.MaxNodes, known, _state.Schools, _state.Tides, out var plan, out var error))
        {
            _error.Text = error ?? string.Empty;
            return;
        }

        var cost = plan.Cost;
        _preview.Text = Loc.GetString("spellcraft-ui-preview", ("name", plan.Name), ("cost", cost));
        _weave.Disabled = _state.Spells.Count >= _state.MaxSpells;
        if (_weave.Disabled)
            _error.Text = Loc.GetString("spellcraft-error-too-many-spells");
    }
}

/// <summary>The canvas the nodes and wires live on. Positions are purely client-side; only the graph is sent.</summary>
public sealed class GraphCanvas : Control
{
    private const float NodeWidth = 132f;
    private const float NodeHeight = 48f;
    private const float PortRadius = 7f;

    private sealed class Node(int id, SpellGlyphPrototype glyph, Vector2 position)
    {
        public readonly int Id = id;
        public readonly SpellGlyphPrototype Glyph = glyph;
        public Vector2 Position = position;

        public bool HasInput => Glyph.Category != GlyphCategory.Form;
        public bool HasOutput => Glyph.Category != GlyphCategory.Augment;
        public Vector2 InputPort => Position + new Vector2(0, NodeHeight / 2f);
        public Vector2 OutputPort => Position + new Vector2(NodeWidth, NodeHeight / 2f);
        public UIBox2 Box => new(Position, Position + new Vector2(NodeWidth, NodeHeight));
    }

    private readonly IPrototypeManager _proto;
    private readonly SpriteSystem _sprites;
    private readonly List<Node> _nodes = [];
    private readonly List<(int From, int To)> _links = [];
    private readonly Dictionary<string, Texture?> _icons = [];
    private readonly Font _font;
    private int _nextId;
    private int _spawned;

    private Node? _dragging;
    private Vector2 _dragOffset;
    private Node? _linkFrom;
    private Vector2 _cursor;

    public event Action? Changed;

    public int NodeCount => _nodes.Count;

    public GraphCanvas(IPrototypeManager proto, SpriteSystem sprites)
    {
        _proto = proto;
        _sprites = sprites;
        MinSize = new Vector2(340, 220);
        HorizontalExpand = true;
        VerticalExpand = true;
        RectClipContent = true;

        // A plain Control ignores the mouse by default, which would let the window's own drag-to-move take the click.
        MouseFilter = MouseFilterMode.Stop;

        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 11);
    }

    public void AddNode(SpellGlyphPrototype glyph)
    {
        // Stagger new nodes so they don't pile up.
        var column = _spawned % 3;
        var row = _spawned / 3 % 5;
        _spawned++;
        _nodes.Add(new Node(_nextId++, glyph, new Vector2(16 + (column * 150), 16 + (row * 58) + (column * 12))));
        Changed?.Invoke();
    }

    public void Clear()
    {
        _nodes.Clear();
        _links.Clear();
        _spawned = 0;
        _dragging = null;
        _linkFrom = null;
        Changed?.Invoke();
    }

    public SpellGraphData ToGraph()
    {
        var graph = new SpellGraphData();
        foreach (var node in _nodes)
            graph.Nodes.Add(new SpellNode(node.Id, node.Glyph.ID));

        foreach (var (from, to) in _links)
            graph.Links.Add(new SpellLink(from, to));

        return graph;
    }

    private Node? NodeAt(Vector2 point)
    {
        for (var i = _nodes.Count - 1; i >= 0; i--)
        {
            if (_nodes[i].Box.Contains(point))
                return _nodes[i];
        }

        return null;
    }

    private Node? InputPortAt(Vector2 point)
        => _nodes.LastOrDefault(n => n.HasInput && (n.InputPort - point).Length() <= PortRadius * 2.2f);

    private Node? OutputPortAt(Vector2 point)
        => _nodes.LastOrDefault(n => n.HasOutput && (n.OutputPort - point).Length() <= PortRadius * 2.2f);

    private bool CanLink(Node from, Node to)
    {
        if (from == to || _links.Contains((from.Id, to.Id)))
            return false;

        return (from.Glyph.Category, to.Glyph.Category) switch
        {
            (GlyphCategory.Form, GlyphCategory.Effect) => true,
            (GlyphCategory.Form, GlyphCategory.Augment) => true,
            (GlyphCategory.Effect, GlyphCategory.Augment) => true,
            _ => false,
        };
    }

    private (int From, int To)? LinkAt(Vector2 point)
    {
        foreach (var link in _links)
        {
            var from = _nodes.FirstOrDefault(n => n.Id == link.From);
            var to = _nodes.FirstOrDefault(n => n.Id == link.To);
            if (from == null || to == null)
                continue;

            if (DistanceToSegment(point, from.OutputPort, to.InputPort) <= 6f)
                return link;
        }

        return null;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var length = ab.LengthSquared();
        if (length < 0.001f)
            return (p - a).Length();

        var t = Math.Clamp(Vector2.Dot(p - a, ab) / length, 0f, 1f);
        return (p - (a + (ab * t))).Length();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        var point = args.RelativePosition;
        _cursor = point;

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            args.Handle();
            if (OutputPortAt(point) is { } source)
            {
                _linkFrom = source;
                args.Handle();
                return;
            }

            if (NodeAt(point) is { } node)
            {
                _dragging = node;
                _dragOffset = point - node.Position;
                // Raise to the top.
                _nodes.Remove(node);
                _nodes.Add(node);
                args.Handle();
            }
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            if (NodeAt(point) is { } node)
            {
                _nodes.Remove(node);
                _links.RemoveAll(l => l.From == node.Id || l.To == node.Id);
                Changed?.Invoke();
            }
            else if (LinkAt(point) is { } link)
            {
                _links.Remove(link);
                Changed?.Invoke();
            }

            args.Handle();
        }
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _cursor = args.RelativePosition;
        _dragging?.Position = _cursor - _dragOffset;
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_linkFrom != null)
        {
            var target = InputPortAt(args.RelativePosition) ?? NodeAt(args.RelativePosition);
            if (target != null && target.HasInput && CanLink(_linkFrom, target))
            {
                _links.Add((_linkFrom.Id, target.Id));
                Changed?.Invoke();
            }

            _linkFrom = null;
        }

        _dragging = null;
    }

    private Texture? IconOf(SpellGlyphPrototype glyph)
    {
        if (!_icons.TryGetValue(glyph.ID, out var icon))
            _icons[glyph.ID] = icon = ArcaneUi.IconOf(_sprites, glyph);

        return icon;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(new UIBox2(0, 0, PixelWidth, PixelHeight), ArcaneTheme.Background);

        // Faint grid.
        for (var x = 0f; x < PixelWidth; x += 32f)
            handle.DrawLine(new Vector2(x, 0), new Vector2(x, PixelHeight), Color.White.WithAlpha(0.04f));

        for (var y = 0f; y < PixelHeight; y += 32f)
            handle.DrawLine(new Vector2(0, y), new Vector2(PixelWidth, y), Color.White.WithAlpha(0.04f));

        foreach (var (fromId, toId) in _links)
        {
            var from = _nodes.FirstOrDefault(n => n.Id == fromId);
            var to = _nodes.FirstOrDefault(n => n.Id == toId);
            if (from != null && to != null)
                DrawWire(handle, from.OutputPort, to.InputPort, ArcaneTheme.GlyphColor(from.Glyph));
        }

        if (_linkFrom != null)
            DrawWire(handle, _linkFrom.OutputPort, _cursor, ArcaneTheme.TextDim);

        foreach (var node in _nodes)
        {
            var accent = ArcaneTheme.GlyphColor(node.Glyph);
            handle.DrawRect(node.Box, ArcaneTheme.PanelRaised);
            handle.DrawRect(node.Box, accent, false);
            handle.DrawRect(new UIBox2(node.Position, node.Position + new Vector2(NodeWidth, 3)), accent);

            if (IconOf(node.Glyph) is { } icon)
                handle.DrawTextureRect(icon, new UIBox2(node.Position + new Vector2(6, 8), node.Position + new Vector2(38, 40)));

            handle.DrawString(_font, node.Position + new Vector2(44, 12), Loc.GetString(node.Glyph.Name), ArcaneTheme.Text);
            handle.DrawString(_font, node.Position + new Vector2(44, 28), $"{node.Glyph.Cost:0.#}", ArcaneTheme.TextDim);

            if (node.HasInput)
                handle.DrawCircle(node.InputPort, PortRadius, ArcaneTheme.Border);

            if (node.HasOutput)
                handle.DrawCircle(node.OutputPort, PortRadius, accent);
        }
    }

    private static void DrawWire(DrawingHandleScreen handle, Vector2 a, Vector2 b, Color color)
    {
        // A gentle S-curve drawn as short segments.
        var previous = a;
        const int Segments = 16;
        var span = MathF.Max(30f, MathF.Abs(b.X - a.X) * 0.5f);
        for (var i = 1; i <= Segments; i++)
        {
            var t = i / (float) Segments;
            var p0 = a;
            var p1 = a + new Vector2(span, 0);
            var p2 = b - new Vector2(span, 0);
            var p3 = b;
            var u = 1f - t;
            var point = (u * u * u * p0) + (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t * p3);
            handle.DrawLine(previous, point, color);
            previous = point;
        }
    }
}
