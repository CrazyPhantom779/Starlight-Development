using System.Linq;
using Content.Server.EUI;
using Content.Shared._Starlight.AutoMod;
using Content.Shared.Eui;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._Starlight.AutoMod;

public sealed class AutoModEui : BaseEui
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private AutoModSystem AutoMod => _ent.System<AutoModSystem>();

    public AutoModEui()
    {
        IoCManager.InjectDependencies(this);
    }

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
                SendMessage(new AutoModTestRuleResultMessage(AutoMod.Test(test.Text, test.Channel, test.RuleId, test.MockPoints)));
                break;

            case AutoModMarkFalsePositiveMessage fp:
                if (Player?.UserId is { } admin)
                {
                    var changed = AutoMod.MarkFalsePositive(fp.IncidentId, admin, fp.Reason);
                    SendMessage(new AutoModOperationResultMessage(changed, changed ? "Marked false positive." : "Incident was not found."));
                }

                StateDirty();
                break;

            case AutoModCreateTemplateRuleMessage create:
                if (Player?.UserId is { } createAdmin)
                {
                    var rule = AutoMod.CreateTemplateRule(createAdmin, create.Reason);
                    SendMessage(new AutoModOperationResultMessage(true, $"Created disabled template rule {rule.ID}. Edit it, save it, then enable it."));
                }

                StateDirty();
                break;

            case AutoModSaveRuleMessage save:
                if (Player?.UserId is { } saveAdmin)
                {
                    var ok = AutoMod.SaveRule(save.Rule, saveAdmin, save.Reason, out var error);
                    SendMessage(new AutoModOperationResultMessage(ok, ok ? $"Saved rule {save.Rule.ID}." : error));
                }

                StateDirty();
                break;

            case AutoModSaveRuleJsonMessage saveJson:
                if (Player?.UserId is { } saveJsonAdmin)
                {
                    var ok = AutoMod.SaveRuleJson(saveJson.Json, saveJsonAdmin, saveJson.Reason, out var error);
                    SendMessage(new AutoModOperationResultMessage(ok, ok ? "Saved rule JSON." : error));
                }

                StateDirty();
                break;

            case AutoModDeleteRuleMessage delete:
                if (Player?.UserId is { } deleteAdmin)
                {
                    var ok = AutoMod.DeleteRule(delete.RuleId, deleteAdmin, delete.Reason);
                    SendMessage(new AutoModOperationResultMessage(ok, ok ? $"Deleted rule {delete.RuleId}." : $"Rule {delete.RuleId} was not found."));
                }

                StateDirty();
                break;

            case AutoModToggleRuleMessage toggle:
                if (Player?.UserId is { } toggleAdmin)
                {
                    var ok = AutoMod.SetRuleEnabled(toggle.RuleId, toggle.Enabled, toggleAdmin, toggle.Reason);
                    SendMessage(new AutoModOperationResultMessage(ok, ok ? $"Rule {toggle.RuleId} enabled={toggle.Enabled}." : $"Rule {toggle.RuleId} was not found."));
                }

                StateDirty();
                break;
        }
    }

    public override EuiStateBase GetNewState()
    {
        var autoMod = AutoMod;
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
