using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.AutoMod;

[Serializable, NetSerializable]
public enum AutoModUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class AutoModUiState : BoundUserInterfaceState
{
    public string RulesetVersion { get; }
    public bool Enabled { get; }
    public bool ShadowMode { get; }
    public bool NullLinkEnabled { get; }
    public bool NullLinkHealthy { get; }
    public bool DiscordEnabled { get; }
    public int PendingApprovals { get; }
    public int UnsyncedIncidents { get; }
    public List<AutoModIncidentRecord> RecentIncidents { get; }
    public List<AutoModRuleSummary> Rules { get; }

    public AutoModUiState(
        string rulesetVersion,
        bool enabled,
        bool shadowMode,
        bool nullLinkEnabled,
        bool nullLinkHealthy,
        bool discordEnabled,
        int pendingApprovals,
        int unsyncedIncidents,
        List<AutoModIncidentRecord> recentIncidents,
        List<AutoModRuleSummary> rules)
    {
        RulesetVersion = rulesetVersion;
        Enabled = enabled;
        ShadowMode = shadowMode;
        NullLinkEnabled = nullLinkEnabled;
        NullLinkHealthy = nullLinkHealthy;
        DiscordEnabled = discordEnabled;
        PendingApprovals = pendingApprovals;
        UnsyncedIncidents = unsyncedIncidents;
        RecentIncidents = recentIncidents;
        Rules = rules;
    }
}

[Serializable, NetSerializable]
public sealed class AutoModRequestRefreshMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class AutoModTestRuleMessage : BoundUserInterfaceMessage
{
    public string Text { get; }
    public string Channel { get; }
    public string? RuleId { get; }
    public int MockPoints { get; }

    public AutoModTestRuleMessage(string text, string channel, string? ruleId, int mockPoints)
    {
        Text = text;
        Channel = channel;
        RuleId = ruleId;
        MockPoints = mockPoints;
    }
}

[Serializable, NetSerializable]
public sealed class AutoModTestRuleResultMessage : BoundUserInterfaceMessage
{
    public AutoModTestResult Result { get; }

    public AutoModTestRuleResultMessage(AutoModTestResult result)
    {
        Result = result;
    }
}

[Serializable, NetSerializable]
public sealed class AutoModMarkFalsePositiveMessage : BoundUserInterfaceMessage
{
    public Guid IncidentId { get; }
    public string Reason { get; }

    public AutoModMarkFalsePositiveMessage(Guid incidentId, string reason)
    {
        IncidentId = incidentId;
        Reason = reason;
    }
}
