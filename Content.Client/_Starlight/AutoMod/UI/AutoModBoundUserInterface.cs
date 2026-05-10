using Content.Shared._Starlight.AutoMod;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.AutoMod.UI;

public sealed class AutoModBoundUserInterface : BoundUserInterface
{
    private AutoModWindow? _window;
    private AutoModUiState? _state;

    public AutoModBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AutoModWindow>();
        _window.TabSelected += tab => _window?.ShowTab(tab, _state);
        _window.TesterSubmitted += text => SendMessage(new AutoModTestRuleMessage(text, "OOC", null, 0));
        SendMessage(new AutoModRequestRefreshMessage());
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is AutoModTestRuleResultMessage result)
            _window?.ShowTestResult(result.Result);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not AutoModUiState auto)
            return;

        _state = auto;
        _window?.Populate(auto);
    }
}
