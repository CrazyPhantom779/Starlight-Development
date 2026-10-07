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
/// The spellweaving window: one tab per discipline the wizard has mastered, a Wind meter, the schools they are
/// attuned to, and the list of spells they currently hold.
/// </summary>
public sealed class SpellcraftWindow : FancyWindow
{
    public event Action<SpellGraphData, SpellDiscipline, SpellOutput>? OnWeave;
    public event Action<string>? OnRote;
    public event Action<NetEntity>? OnForget;
    public event Action? OnDrawCard;
    public event Action? OnCircle;
    public event Action<string>? OnRite;
    public event Action? OnRefresh;

    private readonly WindBar _wind = new() { HorizontalExpand = true };
    private readonly BoxContainer _schools = ArcaneUi.Row(8);
    private readonly BoxContainer _tabs = ArcaneUi.Row(2);
    private readonly Control _body = new() { VerticalExpand = true, HorizontalExpand = true };
    private readonly BoxContainer _spellList = ArcaneUi.Column(2);
    private readonly Label _note = new() { FontColorOverride = ArcaneTheme.TextDim };

    private readonly Dictionary<SpellDiscipline, Control> _views = [];
    private readonly Dictionary<SpellDiscipline, ArcaneButton> _tabButtons = [];
    private SpellDiscipline? _active;
    private SpellcraftBuiState? _state;
    private List<SpellDiscipline> _tabOrder = [];

    public SpellcraftWindow()
    {
        Title = Loc.GetString("spellcraft-ui-title");
        MinSize = new Vector2(780, 560);
        SetSize = new Vector2(900, 660);

        var root = ArcaneUi.Column(6);

        var header = ArcaneUi.Row(12);
        header.AddChild(_wind);
        header.AddChild(_schools);
        root.AddChild(new ArcanePanel(raised: true, margin: 4f) { Children = { header } });

        root.AddChild(_tabs);
        root.AddChild(new ArcanePanel(margin: 8f) { VerticalExpand = true, HorizontalExpand = true, Children = { _body } });

        root.AddChild(ArcaneUi.Heading(Loc.GetString("spellcraft-ui-spells")));
        root.AddChild(new ArcanePanel(margin: 4f)
        {
            Children = { new ScrollContainer { MinHeight = 74, MaxHeight = 110, HScrollEnabled = false, Children = { _spellList } } },
        });
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
        _state = state;
        _wind.Set(state.Wind, state.WindMax);

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

        BuildTabs(state);
        BuildSpellList(state);

        foreach (var (discipline, view) in _views)
        {
            if (view is ISpellcraftView updatable)
                updatable.UpdateState(state);
        }
    }

    private Control CreateView(SpellDiscipline discipline)
        => discipline switch
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

    private void BuildTabs(SpellcraftBuiState state)
    {
        var order = state.Disciplines.Distinct().OrderBy(d => (int) d).ToList();
        if (order.SequenceEqual(_tabOrder))
            return;

        _tabOrder = order;
        _tabs.RemoveAllChildren();
        _tabButtons.Clear();

        foreach (var discipline in order)
        {
            if (!_views.ContainsKey(discipline))
            {
                var view = CreateView(discipline);
                view.Visible = false;
                _views[discipline] = view;
                _body.AddChild(view);
            }

            var button = new ArcaneButton(Loc.GetString($"spellcraft-discipline-{discipline.ToString().ToLowerInvariant()}"), toggle: true)
            {
                ToolTip = Loc.GetString($"spellcraft-discipline-{discipline.ToString().ToLowerInvariant()}-desc"),
                MinSize = new Vector2(92, 30),
            };
            var target = discipline;
            button.OnPressed += _ => Select(target);
            _tabs.AddChild(button);
            _tabButtons[discipline] = button;
        }

        Select(_active is { } current && order.Contains(current) ? current : order.FirstOrDefault());
    }

    private void Select(SpellDiscipline discipline)
    {
        _active = discipline;
        foreach (var (key, view) in _views)
            view.Visible = key == discipline;

        foreach (var (key, button) in _tabButtons)
            button.Pressed = key == discipline;

        _note.Text = Loc.GetString($"spellcraft-discipline-{discipline.ToString().ToLowerInvariant()}-desc");
    }

    private void BuildSpellList(SpellcraftBuiState state)
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
    }
}
