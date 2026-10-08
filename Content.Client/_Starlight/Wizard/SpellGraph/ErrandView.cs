using System.Linq;
using Content.Client._Starlight.Wizard.SpellGraph.Controls;
using Content.Shared._Starlight.Wizard.SpellGraph;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.Wizard.SpellGraph;

/// <summary>
/// Errands: small tasks the Winds set around the station. Each one finished makes the wizard permanently stronger.
/// They are separate from the wizard's objectives.
/// </summary>
public sealed class ErrandView : BoxContainer, ISpellcraftView
{
    private readonly Label _mastery = new() { FontColorOverride = ArcaneTheme.Gold };
    private readonly BoxContainer _list = ArcaneUi.Column(6);
    private readonly KeptScroll _scroll = new();
    private string _signature = string.Empty;

    public ErrandView()
    {
        Orientation = LayoutOrientation.Vertical;
        VerticalExpand = true;
        HorizontalExpand = true;
        SeparationOverride = 6;

        AddChild(ArcaneUi.Dim(Loc.GetString("spellcraft-ui-errands-help")));
        AddChild(_mastery);
        _scroll.AddChild(_list);
        AddChild(_scroll);
    }

    public void UpdateState(SpellcraftBuiState state)
    {
        _mastery.Text = Loc.GetString("spellcraft-ui-power", ("power", state.Power)) + $" / {state.MaxPower}";

        var signature = string.Join(';', state.Errands.Select(e => $"{e.Title}{e.Progress}"));
        if (signature == _signature)
            return;

        _signature = signature;
        _scroll.Rebuild(() => Build(state));
    }

    private void Build(SpellcraftBuiState state)
    {
        _list.RemoveAllChildren();

        if (state.Errands.Count == 0)
            _list.AddChild(ArcaneUi.Dim(Loc.GetString("spellcraft-ui-errands-done")));

        foreach (var errand in state.Errands)
        {
            var column = ArcaneUi.Column(3);
            column.HorizontalExpand = true;
            column.AddChild(new Label { Text = errand.Title, FontColorOverride = ArcaneTheme.Gold });
            column.AddChild(ArcaneUi.Dim(errand.Description));

            var bar = new MiniBar { Fraction = errand.Goal <= 0 ? 0f : errand.Progress / (float) errand.Goal, Fill = ArcaneTheme.Good };
            var progress = ArcaneUi.Row(8);
            progress.AddChild(bar);
            progress.AddChild(new Label
            {
                Text = Loc.GetString("spellcraft-ui-errand-progress", ("progress", errand.Progress), ("goal", errand.Goal)),
                FontColorOverride = ArcaneTheme.TextDim,
            });
            column.AddChild(progress);
            column.AddChild(new Label { Text = Loc.GetString("errand-reward-prefix", ("reward", errand.Reward)), FontColorOverride = ArcaneTheme.Teal });

            _list.AddChild(new ArcanePanel { Children = { column } });
        }
    }
}
