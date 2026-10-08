using System.Linq;
using System.Numerics;
using Content.Client._Starlight.Wizard.SpellGraph.Controls;
using Content.Shared._Starlight.Wizard.Casting;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Wizard.SpellGraph;

public enum ChainInput : byte
{
    /// <summary>Pick glyphs from a palette (Glyphwork).</summary>
    Palette,

    /// <summary>Draw each glyph's pattern on the sigil pad (Sigil).</summary>
    Sigil,
}

/// <summary>
/// The chain editor: build an ordered chain of glyphs (a Form, then Effects with Augments after them),
/// see the live cost, and weave it. Glyphwork picks from a palette; Sigil draws patterns. Both share this.
/// </summary>
public sealed partial class ChainEditor : BoxContainer
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IEntityManager _entMan = default!;

    private readonly SpellcraftWindow _window;
    private readonly SpellDiscipline _discipline;
    private readonly ChainInput _input;
    private readonly List<(SpellOutput Output, ArcaneButton Button)> _outputs = [];

    private readonly BoxContainer _palette = ArcaneUi.Column(6);
    private readonly KeptScroll _paletteScroll = new();
    private readonly BoxContainer _chainBox = ArcaneUi.Row(4);
    private readonly Label _preview = new() { FontColorOverride = ArcaneTheme.Text };
    private readonly Label _detail = new() { FontColorOverride = ArcaneTheme.TextDim };
    private readonly Label _error = new() { FontColorOverride = ArcaneTheme.Bad };
    private readonly SigilPad? _pad;
    private readonly BoxContainer _schoolFilter = ArcaneUi.Row(4);

    private readonly List<string> _chain = [];
    private string? _school;
    private SpellcraftBuiState? _state;
    private string _signature = string.Empty;

    public ChainEditor(SpellcraftWindow window, SpellDiscipline discipline, ChainInput input, params SpellOutput[] outputs)
    {
        IoCManager.InjectDependencies(this);
        _window = window;
        _discipline = discipline;
        _input = input;

        Orientation = LayoutOrientation.Vertical;
        VerticalExpand = true;
        HorizontalExpand = true;
        SeparationOverride = 6;

        // Input: palette or sigil pad.
        if (input == ChainInput.Sigil)
        {
            _pad = new SigilPad();
            _pad.OnGlyph += Add;
            _pad.OnMisdraw += () => _error.Text = Loc.GetString("spellcraft-ui-sigil-misdraw");
            var padRow = ArcaneUi.Row(10);
            padRow.AddChild(new ArcanePanel { Children = { _pad } });
            var legend = ArcaneUi.Column(4);
            legend.AddChild(ArcaneUi.Heading(Loc.GetString("spellcraft-ui-sigil-legend")));
            legend.AddChild(ArcaneUi.Dim(Loc.GetString("spellcraft-ui-sigil-help")));
            _paletteScroll.HorizontalExpand = true;
            _paletteScroll.AddChild(_palette);
            legend.AddChild(_paletteScroll);
            padRow.AddChild(legend);
            legend.HorizontalExpand = true;
            legend.VerticalExpand = true;
            padRow.VerticalExpand = true;
            AddChild(padRow);
        }
        else
        {
            AddChild(_schoolFilter);
            _paletteScroll.AddChild(_palette);
            AddChild(_paletteScroll);
        }

        AddChild(ArcaneUi.Heading(Loc.GetString("spellcraft-ui-chain")));
        AddChild(new ScrollContainer { MinHeight = 96, VScrollEnabled = false, Children = { _chainBox } });

        AddChild(_preview);
        AddChild(_detail);
        AddChild(_error);

        var buttons = ArcaneUi.Row(6);
        foreach (var output in outputs)
        {
            var button = new ArcaneButton(Loc.GetString($"spellcraft-ui-output-{output.ToString().ToLowerInvariant()}"));
            var target = output;
            button.OnPressed += _ => Weave(target);
            buttons.AddChild(button);
            _outputs.Add((output, button));
        }

        var clear = new ArcaneButton(Loc.GetString("spellcraft-ui-clear"));
        clear.OnPressed += _ =>
        {
            _chain.Clear();
            Refresh();
        };
        buttons.AddChild(clear);
        AddChild(buttons);
    }

    private SpriteSystem _sprites => field ??= _entMan.System<SpriteSystem>();

    private void Weave(SpellOutput output)
    {
        if (_chain.Count == 0)
            return;

        var ids = _chain.Select(id => new ProtoId<SpellGlyphPrototype>(id));
        if (!SpellGraphCompiler.TryBuildChain(_proto, ids, out var graph, out _))
            return;

        _window.Weave(graph, _discipline, output);
    }

    private void Add(string id)
    {
        _chain.Add(id);
        Refresh();
    }

    public void UpdateState(SpellcraftBuiState state)
    {
        _state = state;

        // The server refreshes this window every couple of seconds. Only rebuild the palette when what you know changed,
        // otherwise buttons would be replaced under your cursor mid-click.
        var signature = string.Join(',', state.Glyphs) + "|" + string.Join(',', state.Schools);
        if (signature != _signature)
        {
            _signature = signature;
            BuildFilter();
            BuildPalette();
        }

        UpdatePreview();
    }

    private void BuildFilter()
    {
        _schoolFilter.RemoveAllChildren();
        if (_state == null)
            return;

        var all = new ArcaneButton(Loc.GetString("spellcraft-ui-all"), toggle: true) { Pressed = _school == null, MinSize = new Vector2(50, 24) };
        all.OnPressed += _ =>
        {
            _school = null;
            BuildFilter();
            BuildPalette();
        };
        _schoolFilter.AddChild(all);

        foreach (var school in _state.Glyphs
                     .Select(id => _proto.TryIndex<SpellGlyphPrototype>(id, out var g) ? g : null)
                     .Where(g => g != null)
                     .SelectMany(g => g!.Schools)
                     .Distinct()
                     .OrderBy(s => s))
        {
            var attuned = _state.Schools.Contains(school);
            var button = new ArcaneButton(Loc.GetString($"spellcraft-school-{school.ToLowerInvariant()}") + (attuned ? " *" : string.Empty), toggle: true)
            {
                Pressed = _school == school,
                MinSize = new Vector2(60, 24),
                Accent = ArcaneTheme.SchoolColor(school),
                ToolTip = attuned ? Loc.GetString("spellcraft-ui-attuned-tip") : null,
            };
            var target = school;
            button.OnPressed += _ =>
            {
                _school = target;
                BuildFilter();
                BuildPalette();
            };
            _schoolFilter.AddChild(button);
        }
    }

    private void BuildPalette()
        => _paletteScroll.Rebuild(BuildPaletteContents);

    private void BuildPaletteContents()
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
            var inCategory = glyphs
                .Where(g => g.Category == category && (_school == null || g.Schools.Contains(_school)))
                .OrderBy(g => g.Schools.FirstOrDefault() ?? string.Empty)
                .ThenBy(g => Loc.GetString(g.Name))
                .ToList();

            if (inCategory.Count == 0)
                continue;

            _palette.AddChild(ArcaneUi.Heading(Loc.GetString($"spellcraft-ui-category-{category.ToString().ToLowerInvariant()}")));
            var wrap = new WrapContainer { HorizontalExpand = true };
            foreach (var glyph in inCategory)
            {
                var id = glyph.ID;
                var tile = new GlyphTile(glyph, ArcaneUi.IconOf(_sprites, glyph), compact: _input == ChainInput.Sigil);
                if (_input == ChainInput.Sigil)
                {
                    // In sigil mode the palette is only a reference: you must draw the pattern.
                    tile.Disabled = true;
                    tile.ToolTip = (glyph.Description is { } d ? Loc.GetString(d) + "\n" : string.Empty) + Loc.GetString("spellcraft-ui-sigil-draw-it");
                }
                else
                {
                    tile.OnPressed += _ => Add(id);
                }

                wrap.AddChild(tile);
            }

            _palette.AddChild(wrap);
        }

        _pad?.SetKnown(glyphs);
    }

    private void Refresh()
    {
        _chainBox.RemoveAllChildren();
        for (var i = 0; i < _chain.Count; i++)
        {
            var index = i;
            if (!_proto.TryIndex<SpellGlyphPrototype>(_chain[i], out var glyph))
                continue;

            var tile = new GlyphTile(glyph, ArcaneUi.IconOf(_sprites, glyph), compact: true) { ToolTip = Loc.GetString("spellcraft-ui-remove-tip") };
            tile.OnPressed += _ =>
            {
                _chain.RemoveAt(index);
                Refresh();
            };
            _chainBox.AddChild(tile);
        }

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        _preview.Text = string.Empty;
        _detail.Text = string.Empty;
        if (_input != ChainInput.Sigil || _chain.Count > 0)
            _error.Text = string.Empty;

        foreach (var (_, button) in _outputs)
            button.Disabled = true;

        if (_state == null || _chain.Count == 0)
            return;

        var known = _state.Glyphs.Select(id => new ProtoId<SpellGlyphPrototype>(id)).ToList();
        var ids = _chain.Select(id => new ProtoId<SpellGlyphPrototype>(id));
        if (!SpellGraphCompiler.TryBuildChain(_proto, ids, out var graph, out var error)
            || !SpellGraphCompiler.TryCompile(_proto, graph, _state.MaxNodes, known, _state.Schools, _state.Tides, out var plan, out error))
        {
            _error.Text = error ?? string.Empty;
            return;
        }

        var cost = _discipline == SpellDiscipline.Sigil ? MathF.Max(1f, MathF.Round(plan.Cost * 0.9f, 1)) : plan.Cost;
        _preview.Text = Loc.GetString("spellcraft-ui-preview", ("name", plan.Name), ("cost", cost));
        _detail.Text = Loc.GetString("spellcraft-ui-preview-detail",
            ("delivery", Loc.GetString($"spellcraft-delivery-{plan.Delivery.ToString().ToLowerInvariant()}")),
            ("steps", plan.Steps.Count));

        var storable = plan.Steps.All(s => s.Glyph.Effects.Count > 0);
        var heldSpells = _state.Spells.Count;
        foreach (var (output, button) in _outputs)
        {
            button.Disabled = output switch
            {
                SpellOutput.Action => heldSpells >= _state.MaxSpells,
                SpellOutput.Wand => !storable || _state.WandName == null || _state.WandSpells.Count >= _state.WandSlots,
                SpellOutput.Enchant => !storable || _state.EnchantTarget == null,
                SpellOutput.Scroll => !storable || !_state.HasPaper,
                _ => true,
            };
        }

        if (!storable && _outputs.Any(o => o.Output != SpellOutput.Action))
            _error.Text = Loc.GetString("spellcraft-error-not-storable");
        else if (heldSpells >= _state.MaxSpells && _outputs.Any(o => o.Output == SpellOutput.Action))
            _error.Text = Loc.GetString("spellcraft-error-too-many-spells");
    }
}
