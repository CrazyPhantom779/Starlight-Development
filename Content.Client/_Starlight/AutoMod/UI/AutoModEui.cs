using Content.Client.Eui;
using Content.Shared._Starlight.AutoMod;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Starlight.AutoMod.UI;

[UsedImplicitly]
public sealed class AutoModEui : BaseEui
{
    private readonly AutoModWindow _window;
    private AutoModUiState? _state;

    public AutoModEui()
    {
        _window = new AutoModWindow();
        _window.TabSelected += tab => _window.ShowTab(tab, _state);
        _window.TesterSubmitted += (text, channel, ruleId, points) => SendMessage(new AutoModTestRuleMessage(text, channel, ruleId, points));
        _window.CreateTemplateRuleRequested += () => SendMessage(new AutoModCreateTemplateRuleMessage("Created from AutoMod UI"));
        _window.SaveRuleRequested += (rule, reason) => SendMessage(new AutoModSaveRuleMessage(rule, reason));
        _window.DeleteRuleRequested += (ruleId, reason) => SendMessage(new AutoModDeleteRuleMessage(ruleId, reason));
        _window.ToggleRuleRequested += (ruleId, enabled, reason) => SendMessage(new AutoModToggleRuleMessage(ruleId, enabled, reason));
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
        SendMessage(new AutoModRequestRefreshMessage());
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);
        if (state is not AutoModUiState auto)
            return;

        _state = auto;
        _window.Populate(auto);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case AutoModTestRuleResultMessage result:
                _window.ShowTestResult(result.Result);
                break;
            case AutoModOperationResultMessage result:
                _window.ShowOperationResult(result);
                SendMessage(new AutoModRequestRefreshMessage());
                break;
        }
    }
}
