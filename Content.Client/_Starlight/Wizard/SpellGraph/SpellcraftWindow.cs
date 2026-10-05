using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Wizard.SpellGraph;

/// <summary>
/// Glyphwork editor: pick glyphs from the palette to build an ordered chain (Form, then Effects, with Augments
/// applying to the Effect before them), see the live cost, then weave it into a spell.
/// All validation is done by the shared <see cref="SpellGraphCompiler"/>; the server re-checks everything.
/// </summary>
public sealed partial class SpellcraftWindow : DefaultWindow
{
    [Dependency] private IPrototypeManager _proto = default!;

    public event Action<List<string>>? OnWeave;
    public event Action<NetEntity>? OnForget;

    private readonly BoxContainer _palette = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _chainBox = new() { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
    private readonly Label _preview = new() { Text = string.Empty };
    private readonly Label _error = new() { Modulate = Color.OrangeRed };
    private readonly Label _wind = new();
    private readonly Button _weave = new() { Text = Loc.GetString("spellcraft-ui-weave"), Disabled = true };
    private readonly Button _clear = new() { Text = Loc.GetString("spellcraft-ui-clear") };
    private readonly BoxContainer _spellList = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };

    private readonly List<string> _chain = [];
    private SpellcraftBuiState? _state;

    public SpellcraftWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("spellcraft-ui-title");
        MinSize = new Vector2(620, 460);
        SetSize = new Vector2(700, 520);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true, HorizontalExpand = true, SeparationOverride = 6 };

        root.AddChild(_wind);
        root.AddChild(new Label { Text = Loc.GetString("spellcraft-ui-palette") });
        root.AddChild(new ScrollContainer { MinHeight = 170, HScrollEnabled = false, Children = { _palette } });

        root.AddChild(new Label { Text = Loc.GetString("spellcraft-ui-chain") });
        root.AddChild(new ScrollContainer { MinHeight = 44, VScrollEnabled = false, Children = { _chainBox } });
        root.AddChild(_preview);
        root.AddChild(_error);

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        buttons.AddChild(_weave);
        buttons.AddChild(_clear);
        root.AddChild(buttons);

        root.AddChild(new Label { Text = Loc.GetString("spellcraft-ui-spells") });
        root.AddChild(new ScrollContainer { MinHeight = 90, VerticalExpand = true, HScrollEnabled = false, Children = { _spellList } });

        Contents.AddChild(root);

        _weave.OnPressed += _ => OnWeave?.Invoke([.. _chain]);
        _clear.OnPressed += _ =>
        {
            _chain.Clear();
            RefreshChain();
        };
    }

    public void UpdateState(SpellcraftBuiState state)
    {
        _state = state;
        _wind.Text = Loc.GetString("spellcraft-ui-wind", ("wind", MathF.Round(state.Wind)), ("max", MathF.Round(state.WindMax)));

        BuildPalette();
        BuildSpellList();
        RefreshChain();
    }

    private void BuildPalette()
    {
        _palette.RemoveAllChildren();
        if (_state == null)
            return;

        var glyphs = _state.Glyphs
            .Select(id => _proto.TryIndex<SpellGlyphPrototype>(id, out var g) ? g : null)
            .Where(g => g != null)
            .Select(g => g!)
            .ToList();

        foreach (var category in new[] { GlyphCategory.Form, GlyphCategory.Effect, GlyphCategory.Augment })
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            row.AddChild(new Label { Text = Loc.GetString($"spellcraft-ui-category-{category.ToString().ToLowerInvariant()}"), Modulate = Color.LightGray });

            var wrap = new WrapContainer();
            foreach (var glyph in glyphs.Where(g => g.Category == category).OrderBy(g => Loc.GetString(g.Name)))
            {
                var id = glyph.ID;
                var button = new Button
                {
                    Text = $"{Loc.GetString(glyph.Name)} ({glyph.Cost})",
                    ToolTip = glyph.Description is { } desc ? Loc.GetString(desc) : null,
                    MinWidth = 90,
                };
                button.OnPressed += _ =>
                {
                    _chain.Add(id);
                    RefreshChain();
                };
                wrap.AddChild(button);
            }

            row.AddChild(wrap);
            _palette.AddChild(row);
        }
    }

    private void RefreshChain()
    {
        _chainBox.RemoveAllChildren();

        for (var i = 0; i < _chain.Count; i++)
        {
            var index = i;
            var name = _proto.TryIndex<SpellGlyphPrototype>(_chain[i], out var g) ? Loc.GetString(g.Name) : _chain[i];
            var button = new Button { Text = name, ToolTip = Loc.GetString("spellcraft-ui-remove-tip") };
            button.OnPressed += _ =>
            {
                _chain.RemoveAt(index);
                RefreshChain();
            };
            _chainBox.AddChild(button);
        }

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        _preview.Text = string.Empty;
        _error.Text = string.Empty;
        _weave.Disabled = true;

        if (_state == null || _chain.Count == 0)
            return;

        var known = _state.Glyphs.Select(id => new ProtoId<SpellGlyphPrototype>(id)).ToList();
        var ids = _chain.Select(id => new ProtoId<SpellGlyphPrototype>(id));
        if (!SpellGraphCompiler.TryBuildChain(_proto, ids, out var graph, out var error)
            || !SpellGraphCompiler.TryCompile(_proto, graph, _state.MaxNodes, known, out var plan, out error))
        {
            _error.Text = error ?? string.Empty;
            return;
        }

        _preview.Text = Loc.GetString("spellcraft-ui-preview", ("name", plan.Name), ("cost", plan.Cost));
        _weave.Disabled = _state.Spells.Count >= _state.MaxSpells;
        if (_weave.Disabled)
            _error.Text = Loc.GetString("spellcraft-error-too-many-spells");
    }

    private void BuildSpellList()
    {
        _spellList.RemoveAllChildren();
        if (_state == null)
            return;

        foreach (var spell in _state.Spells)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            row.AddChild(new Label { Text = $"{spell.Name} ({spell.Cost})", HorizontalExpand = true });

            var forget = new Button { Text = Loc.GetString("spellcraft-ui-forget") };
            var action = spell.Action;
            forget.OnPressed += _ => OnForget?.Invoke(action);
            row.AddChild(forget);

            _spellList.AddChild(row);
        }
    }
}
