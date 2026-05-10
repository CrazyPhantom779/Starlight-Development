using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared._Starlight.AutoMod;
using Content.Shared.Eui;
using Content.Shared.Starlight.CCVar;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;

namespace Content.Server._Starlight.AutoMod;

public sealed class AutoModEui : BaseEui
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private AutoModSystem _autoMod => _ent.System<AutoModSystem>();

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
                    _autoMod.MarkFalsePositive(fp.IncidentId, admin, fp.Reason);
                StateDirty();
                break;
        }
    }

    public override EuiStateBase GetNewState()
    {
        return new AutoModUiState(
            _autoMod.RulesetVersion,
            _cfg.GetCVar(StarlightCCVars.AutoModEnabled),
            _cfg.GetCVar(StarlightCCVars.AutoModShadowMode),
            _cfg.GetCVar(StarlightCCVars.AutoModNullLinkEnabled),
            _autoMod.NullLinkHealthy,
            _cfg.GetCVar(StarlightCCVars.AutoModDiscordEnabled),
            0,
            _autoMod.UnsyncedCount,
            _autoMod.RecentIncidents.ToList(),
            _autoMod.GetRuleSummaries());
    }
}
