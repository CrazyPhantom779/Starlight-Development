using Content.Shared._Starlight.Flock;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using System.Numerics;

namespace Content.Client._Starlight.Flock;

/// <summary>The Flock control panel. Built in code (no XAML) so it's easy to restyle later.</summary>
public sealed class FlockPanelWindow : DefaultWindow
{
    public event Action<NetEntity>? OnJump;
    public event Action<NetEntity>? OnControl;
    public event Action<NetEntity>? OnRelease;

    private readonly BoxContainer _root = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly Label _compute = new();
    private readonly ProgressBar _computeBar = new() { MinValue = 0, MaxValue = 1, MinHeight = 14 };
    private readonly Label _relay = new();
    private readonly BoxContainer _drones = Section();
    private readonly BoxContainer _traces = Section();
    private readonly BoxContainer _structures = Section();
    private readonly BoxContainer _enemies = Section();

    public FlockPanelWindow()
    {
        Title = Loc.GetString("flock-panel-title");
        MinSize = new Vector2(480, 520);
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        scroll.AddChild(_root);
        ContentsContainer.AddChild(scroll);

        _root.AddChild(_compute);
        _root.AddChild(_computeBar);
        _root.AddChild(_relay);
        AddSection(Loc.GetString("flock-panel-drones"), _drones);
        AddSection(Loc.GetString("flock-panel-traces"), _traces);
        AddSection(Loc.GetString("flock-panel-structures"), _structures);
        AddSection(Loc.GetString("flock-panel-enemies"), _enemies);
    }

    private static BoxContainer Section() => new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };

    private void AddSection(string title, BoxContainer box)
    {
        _root.AddChild(new Label { Text = title, StyleClasses = { "LabelHeading" } });
        _root.AddChild(box);
    }

    public void Update(FlockPanelState s)
    {
        _compute.Text = Loc.GetString("flock-panel-compute", ("used", s.UsedCompute), ("total", s.TotalCompute), ("tiles", s.FlockTiles), ("egg", s.EggCost));
        _computeBar.Value = s.TotalCompute <= 0 ? 0 : Math.Clamp((float) s.UsedCompute / s.TotalCompute, 0f, 1f);
        _relay.Text = s.RelayBuilt
            ? Loc.GetString("flock-panel-relay-built")
            : Loc.GetString(s.RelayUnlocked ? "flock-panel-relay-unlocked" : "flock-panel-relay-locked", ("compute", s.RelayCompute), ("tiles", s.RelayTiles));

        Fill(_drones, s.Drones, drone: true);
        Fill(_traces, s.Traces, trace: true);
        Fill(_structures, s.Structures);
        Fill(_enemies, s.Enemies);
    }

    private void Fill(BoxContainer box, List<FlockPanelEntry> entries, bool drone = false, bool trace = false)
    {
        box.RemoveAllChildren();
        if (entries.Count == 0)
        {
            box.AddChild(new Label { Text = Loc.GetString("flock-panel-none") });
            return;
        }
        foreach (var e in entries)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            var text = e.Name + (string.IsNullOrEmpty(e.Extra) ? "" : $" [{e.Extra}]") + (drone ? $"  ({e.Resources}u)" : "");
            row.AddChild(new Label { Text = text, HorizontalExpand = true });
            if (drone || e.Health < 1f)
                row.AddChild(new ProgressBar { MinValue = 0, MaxValue = 1, Value = e.Health, MinWidth = 60, MinHeight = 10, VerticalAlignment = VAlignment.Center });
            var ent = e.Entity;
            var jump = new Button { Text = Loc.GetString("flock-panel-jump") };
            jump.OnPressed += _ => OnJump?.Invoke(ent);
            row.AddChild(jump);
            if (drone && e.Extra != "dead" && e.Extra != "controlled")
            {
                var ctl = new Button { Text = Loc.GetString("flock-panel-control") };
                ctl.OnPressed += _ => OnControl?.Invoke(ent);
                row.AddChild(ctl);
            }
            if (trace)
            {
                var rel = new Button { Text = Loc.GetString("flock-panel-release") };
                rel.OnPressed += _ => OnRelease?.Invoke(ent);
                row.AddChild(rel);
            }
            box.AddChild(row);
        }
    }
}

/// <summary>Small popup listing the structures that can be tealprinted.</summary>
public sealed class FlockTealprintWindow : DefaultWindow
{
    public event Action<string>? OnChoose;
    private readonly BoxContainer _list = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3 };

    public FlockTealprintWindow()
    {
        Title = Loc.GetString("flock-tealprint-title");
        MinSize = new Vector2(280, 200);
        ContentsContainer.AddChild(_list);
    }

    public void Update(FlockTealprintMenuState state)
    {
        _list.RemoveAllChildren();
        foreach (var (proto, name, cost) in state.Available)
        {
            var b = new Button { Text = cost > 0 ? $"{name} ({cost})" : name, HorizontalExpand = true };
            var p = proto;
            b.OnPressed += _ => OnChoose?.Invoke(p);
            _list.AddChild(b);
        }
    }
}
