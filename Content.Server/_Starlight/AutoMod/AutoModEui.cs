using System.Linq;
using Content.Server.EUI;
using Content.Shared._Starlight.AutoMod;
using Content.Shared.Eui;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._Starlight.AutoMod;

public sealed class AutoModEui : BaseEui
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private AutoModSystem _autoMod => _ent.System<AutoModSystem>();

    public AutoModEui()
        => IoCManager.InjectDependencies(this);

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case AutoModRequestRefreshMessage:
                StateDirty();
                break;

            case AutoModTestRuleMessage test:
                SendMessage(new AutoModTestRuleResultMessage(_autoMod.Test(test.Text, test.Channel, test.RuleId, test.MockPoints)));
                break;

            case AutoModMarkFalsePositiveMessage fp:
                if (Player?.UserId is { } admin)
                {
                    var changed = _autoMod.MarkFalsePositive(fp.IncidentId, admin, fp.Reason);
                    SendMessage(new AutoModOperationResultMessage(changed, changed ? "Marked false positive." : "Incident was not found."));
                }

                StateDirty();
                break;

            case AutoModCreateTemplateRuleMessage create:
                if (Player?.UserId is { } createAdmin)
                {
                    var rule = _autoMod.CreateTemplateRule(createAdmin, create.Reason);
                    SendMessage(new AutoModOperationResultMessage(true, $"Created disabled template rule {rule.ID}. Edit it, save it, then enable it."));
                }

                StateDirty();
                break;

            case AutoModSaveRuleMessage save:
                if (Player?.UserId is { } saveAdmin)
                {
                    var ok = _autoMod.SaveRule(save.Rule, saveAdmin, save.Reason, out var error);
                    SendMessage(new AutoModOperationResultMessage(ok, ok ? $"Saved rule {save.Rule.ID}." : error));
                }

                StateDirty();
                break;

            case AutoModSaveRuleJsonMessage saveJson:
                if (Player?.UserId is { } saveJsonAdmin)
                {
                    var ok = _autoMod.SaveRuleJson(saveJson.Json, saveJsonAdmin, saveJson.Reason, out var error);
                    SendMessage(new AutoModOperationResultMessage(ok, ok ? "Saved rule JSON." : error));
                }

                StateDirty();
                break;

            case AutoModDeleteRuleMessage delete:
                if (Player?.UserId is { } deleteAdmin)
                {
                    var ok = _autoMod.DeleteRule(delete.RuleId, deleteAdmin, delete.Reason);
                    SendMessage(new AutoModOperationResultMessage(ok, ok ? $"Deleted rule {delete.RuleId}." : $"Rule {delete.RuleId} was not found."));
                }

                StateDirty();
                break;

            case AutoModToggleRuleMessage toggle:
                if (Player?.UserId is { } toggleAdmin)
                {
                    var ok = _autoMod.SetRuleEnabled(toggle.RuleId, toggle.Enabled, toggleAdmin, toggle.Reason);
                    SendMessage(new AutoModOperationResultMessage(ok, ok ? $"Rule {toggle.RuleId} enabled={toggle.Enabled}." : $"Rule {toggle.RuleId} was not found."));
                }

                StateDirty();
                break;
        }
    }

    public override EuiStateBase GetNewState()
    {
        var autoMod = _autoMod;
        return new AutoModUiState(
            autoMod.RulesetVersion,
            _cfg.GetCVar(StarlightCCVars.AutoModEnabled),
            _cfg.GetCVar(StarlightCCVars.AutoModShadowMode),
            _cfg.GetCVar(StarlightCCVars.AutoModNullLinkEnabled),
            autoMod.NullLinkHealthy,
            _cfg.GetCVar(StarlightCCVars.AutoModDiscordEnabled),
            0,
            autoMod.UnsyncedCount,
            autoMod.RecentIncidents.ToList(),
            autoMod.GetRuleSummaries(),
            autoMod.GetEditableRules().Select(x => x.Clone()).ToList(),
            autoMod.RuleStorePath);
    }
}
