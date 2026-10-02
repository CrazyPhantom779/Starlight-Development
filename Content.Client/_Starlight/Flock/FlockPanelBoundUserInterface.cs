using Robust.Shared.Map;
using Content.Shared._Starlight.Flock;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Flock;

[UsedImplicitly]
public sealed class FlockPanelBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables] private FlockPanelWindow? _panel;
    [ViewVariables] private FlockTealprintWindow? _menu;
    private NetCoordinates _at;

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        switch (state)
        {
            case FlockPanelState panel:
                _menu?.Close();
                _menu = null;
                if (_panel == null)
                {
                    _panel = this.CreateWindow<FlockPanelWindow>();
                    _panel.OnJump += t => SendMessage(new FlockPanelJumpMessage(t));
                    _panel.OnControl += t => SendMessage(new FlockPanelControlMessage(t));
                    _panel.OnRelease += t => SendMessage(new FlockPanelReleaseTraceMessage(t));
                    _panel.OnClose += () => _panel = null;
                }
                _panel.Update(panel);
                break;
            case FlockTealprintMenuState menu:
                _at = menu.At;
                if (_menu == null)
                {
                    _menu = this.CreateWindow<FlockTealprintWindow>();
                    _menu.OnChoose += id => SendMessage(new FlockTealprintChoiceMessage(id, _at));
                    _menu.OnClose += () => _menu = null;
                }
                _menu.Update(menu);
                break;
        }
    }
}
