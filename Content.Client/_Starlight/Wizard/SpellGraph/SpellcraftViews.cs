using System.Linq;
using System.Numerics;
using Content.Client._Starlight.Wizard.SpellGraph.Controls;
using Content.Shared._Starlight.Wizard.Casting;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Wizard.SpellGraph;

/// <summary>One tab of the spellweaving window.</summary>
public interface ISpellcraftView
{
    void UpdateState(SpellcraftBuiState state);
}

/// <summary>Rote: prepared spells, ready to be added to your action bar.</summary>
public sealed partial class RoteView : BoxContainer, ISpellcraftView
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IEntityManager _entMan = default!;

    private readonly SpellcraftWindow _window;
    private readonly BoxContainer _list = ArcaneUi.Column(6);

    public RoteView(SpellcraftWindow window)
    {
        IoCManager.InjectDependencies(this);
        _window = window;
        Orientation = LayoutOrientation.Vertical;
        VerticalExpand = true;
        HorizontalExpand = true;
        AddChild(ArcaneUi.Dim(Loc.GetString("spellcraft-ui-rote-help")));
        AddChild(new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, Children = { _list } });
    }

    public void UpdateState(SpellcraftBuiState state)
    {
        _list.RemoveAllChildren();
        var sprites = _entMan.System<SpriteSystem>();

        var rotes = state.Rotes
            .Select(id => _proto.TryIndex<RoteSpellPrototype>(id, out var r) ? r : null)
            .Where(r => r != null)
            .Select(r => r!)
            .OrderBy(r => Loc.GetString(r.Name))
            .ToList();

        if (rotes.Count == 0)
            _list.AddChild(ArcaneUi.Dim(Loc.GetString("spellcraft-ui-rote-none")));

        foreach (var rote in rotes)
        {
            var row = ArcaneUi.Row(8);
            var icons = ArcaneUi.Row(2);
            foreach (var id in rote.Chain)
            {
                if (!_proto.TryIndex(id, out var glyph))
                    continue;

                icons.AddChild(new TextureRect
                {
                    Texture = ArcaneUi.IconOf(sprites, glyph),
                    Stretch = TextureRect.StretchMode.KeepCentered,
                    MinSize = new Vector2(30, 30),
                    ToolTip = Loc.GetString(glyph.Name),
                });
            }

            row.AddChild(icons);

            var text = ArcaneUi.Column(0);
            text.HorizontalExpand = true;
            text.AddChild(new Label { Text = Loc.GetString(rote.Name), FontColorOverride = ArcaneTheme.Text });
            if (rote.Description is { } description)
                text.AddChild(ArcaneUi.Dim(Loc.GetString(description)));

            row.AddChild(text);

            var cost = "?";
            if (SpellGraphCompiler.TryBuildChain(_proto, rote.Chain, out var graph, out _)
                && SpellGraphCompiler.TryCompile(_proto, graph, int.MaxValue, null, state.Schools, out var plan, out _))
                cost = $"{plan.Cost:0.#}";

            row.AddChild(new Label { Text = Loc.GetString("spellcraft-ui-cost", ("cost", cost)), FontColorOverride = ArcaneTheme.TextDim, VerticalAlignment = VAlignment.Center });

            var prepare = new ArcaneButton(Loc.GetString("spellcraft-ui-prepare")) { Disabled = state.Spells.Count >= state.MaxSpells };
            var captured = rote.ID;
            prepare.OnPressed += _ => _window.SendRote(captured);
            row.AddChild(prepare);

            _list.AddChild(new ArcanePanel { Children = { row } });
        }
    }
}

/// <summary>Wandwright and Artifice: shows what you are holding, then the chain editor to build the spell.</summary>
public sealed class ObjectView : BoxContainer, ISpellcraftView
{
    private readonly SpellDiscipline _discipline;
    private readonly ChainEditor _editor;
    private readonly BoxContainer _status = ArcaneUi.Column(2);

    public ObjectView(SpellcraftWindow window, SpellDiscipline discipline)
    {
        _discipline = discipline;
        Orientation = LayoutOrientation.Vertical;
        VerticalExpand = true;
        HorizontalExpand = true;
        SeparationOverride = 6;

        AddChild(new ArcanePanel(raised: true) { Children = { _status } });

        _editor = discipline == SpellDiscipline.Wandwright
            ? new ChainEditor(window, SpellDiscipline.Glyphwork, ChainInput.Palette, SpellOutput.Wand)
            : new ChainEditor(window, SpellDiscipline.Glyphwork, ChainInput.Palette, SpellOutput.Enchant, SpellOutput.Scroll);
        AddChild(_editor);
    }

    public void UpdateState(SpellcraftBuiState state)
    {
        _status.RemoveAllChildren();
        _status.AddChild(ArcaneUi.Heading(Loc.GetString($"spellcraft-discipline-{_discipline.ToString().ToLowerInvariant()}")));
        _status.AddChild(ArcaneUi.Dim(Loc.GetString($"spellcraft-discipline-{_discipline.ToString().ToLowerInvariant()}-desc")));

        if (_discipline == SpellDiscipline.Wandwright)
        {
            if (state.WandName == null)
            {
                _status.AddChild(new Label { Text = Loc.GetString("spellcraft-ui-wand-none"), FontColorOverride = ArcaneTheme.Bad });
            }
            else
            {
                _status.AddChild(new Label
                {
                    Text = Loc.GetString("spellcraft-ui-wand-held", ("wand", state.WandName), ("used", state.WandSpells.Count), ("slots", state.WandSlots)),
                    FontColorOverride = ArcaneTheme.Good,
                });
                foreach (var spell in state.WandSpells)
                    _status.AddChild(ArcaneUi.Dim("  - " + spell));
            }
        }
        else
        {
            _status.AddChild(new Label
            {
                Text = state.EnchantTarget != null
                    ? Loc.GetString("spellcraft-ui-enchant-target", ("item", state.EnchantTarget))
                    : Loc.GetString("spellcraft-ui-enchant-none"),
                FontColorOverride = state.EnchantTarget != null ? ArcaneTheme.Good : ArcaneTheme.Bad,
            });
            _status.AddChild(new Label
            {
                Text = Loc.GetString(state.HasPaper ? "spellcraft-ui-paper-held" : "spellcraft-ui-paper-none"),
                FontColorOverride = state.HasPaper ? ArcaneTheme.Good : ArcaneTheme.Bad,
            });
        }

        _editor.UpdateState(state);
    }
}

/// <summary>Tarot: draw a card, and read what the arcana can do.</summary>
public sealed partial class TarotView : BoxContainer, ISpellcraftView
{
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly SpellcraftWindow _window;
    private readonly ArcaneButton _draw = new(string.Empty);
    private readonly BoxContainer _codex = ArcaneUi.Column(4);

    public TarotView(SpellcraftWindow window)
    {
        IoCManager.InjectDependencies(this);
        _window = window;
        Orientation = LayoutOrientation.Vertical;
        VerticalExpand = true;
        HorizontalExpand = true;
        SeparationOverride = 6;

        AddChild(ArcaneUi.Dim(Loc.GetString("spellcraft-ui-tarot-help")));
        _draw.OnPressed += _ => _window.SendDrawCard();
        AddChild(_draw);
        AddChild(ArcaneUi.Heading(Loc.GetString("spellcraft-ui-arcana")));
        AddChild(new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, Children = { _codex } });
    }

    public void UpdateState(SpellcraftBuiState state)
    {
        _draw.Text = Loc.GetString("spellcraft-ui-tarot-draw", ("cost", state.TarotCost));
        _codex.RemoveAllChildren();

        string Names(IEnumerable<ProtoId<SpellGlyphPrototype>> chain)
            => string.Join(" + ", chain
                .Select(id => _proto.TryIndex(id, out var g) ? g : null)
                .Where(g => g is { Category: GlyphCategory.Effect })
                .Select(g => Loc.GetString(g!.Name)));

        foreach (var card in _proto.EnumeratePrototypes<TarotCardPrototype>().OrderBy(c => c.ID))
        {
            var text = ArcaneUi.Column(0);
            text.AddChild(new Label { Text = $"{card.Numeral}  {Loc.GetString(card.Name)}", FontColorOverride = ArcaneTheme.Gold });
            if (card.Flavor is { } flavor)
                text.AddChild(ArcaneUi.Dim(Loc.GetString(flavor)));

            text.AddChild(new Label { Text = Loc.GetString("spellcraft-ui-upright", ("effects", Names(card.Upright))), FontColorOverride = ArcaneTheme.Text });
            text.AddChild(new Label { Text = Loc.GetString("spellcraft-ui-reversed", ("effects", Names(card.Reversed))), FontColorOverride = ArcaneTheme.TextDim });
            _codex.AddChild(new ArcanePanel { Children = { text } });
        }
    }
}

/// <summary>Ritual: draw a circle, lay offerings on it, perform a rite.</summary>
public sealed class RitualView : BoxContainer, ISpellcraftView
{
    private readonly SpellcraftWindow _window;
    private readonly Label _status = new();
    private readonly ArcaneButton _circle = new(string.Empty);
    private readonly BoxContainer _list = ArcaneUi.Column(6);

    public RitualView(SpellcraftWindow window)
    {
        _window = window;
        Orientation = LayoutOrientation.Vertical;
        VerticalExpand = true;
        HorizontalExpand = true;
        SeparationOverride = 6;

        AddChild(ArcaneUi.Dim(Loc.GetString("spellcraft-ui-ritual-help")));

        var top = ArcaneUi.Row(8);
        _circle.OnPressed += _ => _window.SendCircle();
        top.AddChild(_circle);
        var refresh = new ArcaneButton(Loc.GetString("spellcraft-ui-refresh"));
        refresh.OnPressed += _ => _window.SendRefresh();
        top.AddChild(refresh);
        _status.VerticalAlignment = VAlignment.Center;
        top.AddChild(_status);
        AddChild(top);

        AddChild(new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, Children = { _list } });
    }

    public void UpdateState(SpellcraftBuiState state)
    {
        _circle.Text = Loc.GetString("spellcraft-ui-circle-draw", ("cost", state.CircleCost));
        _status.Text = Loc.GetString(state.CircleNearby ? "spellcraft-ui-circle-near" : "spellcraft-ui-circle-far");
        _status.FontColorOverride = state.CircleNearby ? ArcaneTheme.Good : ArcaneTheme.Bad;

        _list.RemoveAllChildren();
        foreach (var rite in state.Rituals)
        {
            var column = ArcaneUi.Column(2);
            column.HorizontalExpand = true;
            column.AddChild(new Label { Text = rite.Name, FontColorOverride = ArcaneTheme.Gold });
            column.AddChild(ArcaneUi.Dim(rite.Description));

            foreach (var offering in rite.Offerings)
            {
                var met = offering.Have >= offering.Need;
                column.AddChild(new Label
                {
                    Text = Loc.GetString("spellcraft-ui-offering", ("label", offering.Label), ("have", offering.Have), ("need", offering.Need)),
                    FontColorOverride = met ? ArcaneTheme.Good : ArcaneTheme.TextDim,
                });
            }

            var row = ArcaneUi.Row(8);
            row.AddChild(column);
            var perform = new ArcaneButton(Loc.GetString("spellcraft-ui-perform")) { Disabled = !rite.Satisfied || !state.CircleNearby };
            var id = rite.Id;
            perform.OnPressed += _ => _window.SendRite(id);
            row.AddChild(perform);
            _list.AddChild(new ArcanePanel { Children = { row } });
        }
    }
}
