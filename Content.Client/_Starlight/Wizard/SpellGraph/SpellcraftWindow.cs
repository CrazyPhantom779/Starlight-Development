using System.Linq;
using System.Numerics;
using Content.Client._Starlight.Wizard.SpellGraph.Controls;
using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.Wizard.Casting;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using SpellGraphData = Content.Shared._Starlight.Wizard.SpellGraph.SpellGraph;

namespace Content.Client._Starlight.Wizard.SpellGraph;

/// <summary>
/// The spellweaving window: one tab per discipline the wizard has mastered plus Errands, a Wind meter, the
/// schools they are attuned to with this moment's tides, and the list of spells they currently hold.
/// </summary>
public sealed class SpellcraftWindow : FancyWindow
{
    private const string ErrandsKey = "Errands";

    public event Action<SpellGraphData, SpellDiscipline, SpellOutput>? OnWeave;
    public event Action<string>? OnRote;
    public event Action<NetEntity>? OnForget;
    public event Action? OnDrawCard;
    public event Action? OnCircle;
    public event Action<string>? OnRite;
    public event Action? OnRefresh;

    private readonly WindBar _wind = new() { HorizontalExpand = true };
    private readonly Label _mastery = new() { FontColorOverride = ArcaneTheme.Gold, VerticalAlignment = VAlignment.Center };
    private readonly BoxContainer _schools = ArcaneUi.Row(8);
    private readonly WrapContainer _tides = new() { HorizontalExpand = true };
    private readonly BoxContainer _tabs = ArcaneUi.Row(2);
    private readonly Control _body = new() { VerticalExpand = true, HorizontalExpand = true };
    private readonly BoxContainer _spellList = ArcaneUi.Column(2);
    private readonly KeptScroll _spellScroll = new() { MinHeight = 56, MaxHeight = 96 };
    private readonly Label _note = new() { FontColorOverride = ArcaneTheme.TextDim };

    private readonly Dictionary<string, Control> _views = [];
    private readonly Dictionary<string, ArcaneButton> _tabButtons = [];
    private string? _active;
    private string _spellSignature = string.Empty;
    private List<string> _tabOrder = [];

    public SpellcraftWindow()
    {
        Title = Loc.GetString("spellcraft-ui-title");

        // Small enough for modest screens; everything inside scrolls.
        MinSize = new Vector2(700, 480);
        SetSize = new Vector2(860, 620);

        var root = ArcaneUi.Column(6);

        var header = ArcaneUi.Column(4);
        var top = ArcaneUi.Row(12);
        top.AddChild(_wind);
        top.AddChild(_mastery);
        top.AddChild(_schools);
        header.AddChild(top);

        var tideRow = ArcaneUi.Row(8);
        tideRow.AddChild(new Label
        {
            Text = Loc.GetString("spellcraft-ui-tides"),
            FontColorOverride = ArcaneTheme.TextDim,
            VerticalAlignment = VAlignment.Center,
            ToolTip = Loc.GetString("spellcraft-ui-tides-tip"),
        });
        tideRow.AddChild(_tides);
        header.AddChild(tideRow);
        root.AddChild(new ArcanePanel(raised: true, margin: 4f) { Children = { header } });

        root.AddChild(new ScrollContainer { VScrollEnabled = false, Children = { _tabs } });
        root.AddChild(new ArcanePanel(margin: 8f) { VerticalExpand = true, HorizontalExpand = true, Children = { _body } });

        root.AddChild(ArcaneUi.Heading(Loc.GetString("spellcraft-ui-spells")));
        _spellScroll.AddChild(_spellList);
        root.AddChild(new ArcanePanel(margin: 4f) { Children = { _spellScroll } });
        root.AddChild(_note);

        ContentsContainer.AddChild(new ArcanePanel(margin: 8f)
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            PanelOverride = ArcaneTheme.Box(ArcaneTheme.Background, ArcaneTheme.Border, 0f, 8f),
            Children = { root },
        });
    }

    // Used by the editors and views.
    public void Weave(SpellGraphData graph, SpellDiscipline discipline, SpellOutput output) => OnWeave?.Invoke(graph, discipline, output);
    public void SendRote(string id) => OnRote?.Invoke(id);
    public void SendDrawCard() => OnDrawCard?.Invoke();
    public void SendCircle() => OnCircle?.Invoke();
    public void SendRite(string id) => OnRite?.Invoke(id);
    public void SendRefresh() => OnRefresh?.Invoke();

    public void UpdateState(SpellcraftBuiState state)
    {
        _wind.Set(state.Wind, state.WindMax);
        _wind.ToolTip = Loc.GetString(state.Hurt ? "spellcraft-ui-regen-hurt" : "spellcraft-ui-regen", ("rate", MathF.Round(state.Regen, 2)));
        _mastery.Text = Loc.GetString("spellcraft-ui-power", ("power", state.Power));

        BuildSchools(state);
        BuildTides(state);
        BuildTabs(state);
        BuildSpellList(state);

        foreach (var view in _views.Values)
        {
            if (view is ISpellcraftView updatable)
                updatable.UpdateState(state);
        }
    }

    private void BuildSchools(SpellcraftBuiState state)
    {
        _schools.RemoveAllChildren();
        _schools.AddChild(new Label { Text = Loc.GetString("spellcraft-ui-attuned"), FontColorOverride = ArcaneTheme.TextDim, VerticalAlignment = VAlignment.Center });
        if (state.Schools.Count == 0)
            _schools.AddChild(new Label { Text = Loc.GetString("spellcraft-ui-attuned-none"), FontColorOverride = ArcaneTheme.TextDim, VerticalAlignment = VAlignment.Center });

        foreach (var school in state.Schools.OrderBy(s => s))
        {
            _schools.AddChild(new Label
            {
                Text = Loc.GetString($"spellcraft-school-{school.ToLowerInvariant()}"),
                FontColorOverride = ArcaneTheme.SchoolColor(school),
                VerticalAlignment = VAlignment.Center,
            });
        }
    }

    private void BuildTides(SpellcraftBuiState state)
    {
        _tides.RemoveAllChildren();
        foreach (var (school, tide) in state.Tides.OrderBy(t => t.Key))
        {
            // Below 1 is cheaper to cast from right now, above 1 is dearer.
            var tint = tide < 0.97f ? ArcaneTheme.Good : tide > 1.03f ? ArcaneTheme.Bad : ArcaneTheme.TextDim;
            var chip = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 0, 10, 0) };
            chip.AddChild(new Label { Text = Loc.GetString($"spellcraft-school-{school.ToLowerInvariant()}") + " ", FontColorOverride = ArcaneTheme.SchoolColor(school) });
            chip.AddChild(new Label { Text = $"x{tide:0.00}", FontColorOverride = tint });
            _tides.AddChild(chip);
        }
    }

    private Control CreateView(string key)
    {
        if (key == ErrandsKey)
            return new ErrandView();

        return Enum.Parse<SpellDiscipline>(key) switch
        {
            SpellDiscipline.Rote => new RoteView(this),
            SpellDiscipline.Glyphwork => new ChainEditor(this, SpellDiscipline.Glyphwork, ChainInput.Palette, SpellOutput.Action),
            SpellDiscipline.Sigil => new ChainEditor(this, SpellDiscipline.Sigil, ChainInput.Sigil, SpellOutput.Action),
            SpellDiscipline.Circuit => new GraphEditor(this),
            SpellDiscipline.Wandwright => new ObjectView(this, SpellDiscipline.Wandwright),
            SpellDiscipline.Artifice => new ObjectView(this, SpellDiscipline.Artifice),
            SpellDiscipline.Tarot => new TarotView(this),
            _ => new RitualView(this),
        };
    }

    private void BuildTabs(SpellcraftBuiState state)
    {
        List<string> order = [.. state.Disciplines.Distinct().OrderBy(d => (int) d).Select(d => d.ToString()), ErrandsKey];
        if (order.SequenceEqual(_tabOrder))
            return;

        _tabOrder = order;
        _tabs.RemoveAllChildren();
        _tabButtons.Clear();

        foreach (var key in order)
        {
            if (!_views.ContainsKey(key))
            {
                var view = CreateView(key);
                view.Visible = false;
                _views[key] = view;
                _body.AddChild(view);
            }

            var button = new ArcaneButton(TabTitle(key), toggle: true)
            {
                ToolTip = TabDescription(key),
                MinSize = new Vector2(92, 30),
            };
            var target = key;
            button.OnPressed += _ => Select(target);
            _tabs.AddChild(button);
            _tabButtons[key] = button;
        }

        Select(_active is { } current && order.Contains(current) ? current : order[0]);
    }

    private static string TabTitle(string key)
        => key == ErrandsKey
            ? Loc.GetString("spellcraft-ui-errands")
            : Loc.GetString($"spellcraft-discipline-{key.ToLowerInvariant()}");

    private static string TabDescription(string key)
        => key == ErrandsKey
            ? Loc.GetString("spellcraft-ui-errands-help")
            : Loc.GetString($"spellcraft-discipline-{key.ToLowerInvariant()}-desc");

    private void Select(string key)
    {
        _active = key;
        foreach (var (name, view) in _views)
            view.Visible = name == key;

        foreach (var (name, button) in _tabButtons)
            button.Pressed = name == key;

        _note.Text = TabDescription(key);
    }

    private void BuildSpellList(SpellcraftBuiState state)
    {
        var signature = string.Join(';', state.Spells.Select(sp => $"{sp.Action}{sp.Name}{sp.Cost}"));
        if (signature == _spellSignature)
            return;

        _spellSignature = signature;
        _spellScroll.Rebuild(() =>
        {
            _spellList.RemoveAllChildren();
            if (state.Spells.Count == 0)
                _spellList.AddChild(ArcaneUi.Dim(Loc.GetString("spellcraft-ui-spells-none")));

            foreach (var spell in state.Spells)
            {
                var row = ArcaneUi.Row(6);
                row.AddChild(new Label
                {
                    Text = $"{spell.Name} ({spell.Cost:0.#})",
                    FontColorOverride = ArcaneTheme.Text,
                    HorizontalExpand = true,
                    ClipText = true,
                });

                var forget = new ArcaneButton(Loc.GetString("spellcraft-ui-forget")) { MinSize = new Vector2(70, 22) };
                var action = spell.Action;
                forget.OnPressed += _ => OnForget?.Invoke(action);
                row.AddChild(forget);
                _spellList.AddChild(row);
            }
        });
    }
}
