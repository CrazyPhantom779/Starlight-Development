using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.AutoMod;

[Serializable, NetSerializable]
public enum AutoModUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class AutoModUiState(
    string rulesetVersion,
    bool enabled,
    bool shadowMode,
    bool nullLinkEnabled,
    bool nullLinkHealthy,
    bool discordEnabled,
    int pendingApprovals,
    int unsyncedIncidents,
    List<AutoModIncidentRecord> recentIncidents,
    List<AutoModRuleSummary> rules,
    List<AutoModEditableRule> editableRules,
    string ruleStorePath) : EuiStateBase
{
    public string RulesetVersion { get; } = rulesetVersion;
    public bool Enabled { get; } = enabled;
    public bool ShadowMode { get; } = shadowMode;
    public bool NullLinkEnabled { get; } = nullLinkEnabled;
    public bool NullLinkHealthy { get; } = nullLinkHealthy;
    public bool DiscordEnabled { get; } = discordEnabled;
    public int PendingApprovals { get; } = pendingApprovals;
    public int UnsyncedIncidents { get; } = unsyncedIncidents;
    public List<AutoModIncidentRecord> RecentIncidents { get; } = recentIncidents;
    public List<AutoModRuleSummary> Rules { get; } = rules;
    public List<AutoModEditableRule> EditableRules { get; } = editableRules;
    public string RuleStorePath { get; } = ruleStorePath;
}

[Serializable, NetSerializable]
public sealed class AutoModRequestRefreshMessage : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class AutoModTestRuleMessage(string text, string channel, string? ruleId, int mockPoints) : EuiMessageBase
{
    public string Text { get; } = text;
    public string Channel { get; } = channel;
    public string? RuleId { get; } = ruleId;
    public int MockPoints { get; } = mockPoints;
}

[Serializable, NetSerializable]
public sealed class AutoModTestRuleResultMessage(AutoModTestResult result) : EuiMessageBase
{
    public AutoModTestResult Result { get; } = result;
}

[Serializable, NetSerializable]
public sealed class AutoModMarkFalsePositiveMessage : EuiMessageBase
{
    public Guid IncidentId { get; }
    public string Reason { get; }

    public AutoModMarkFalsePositiveMessage(Guid incidentId, string reason)
    {
        IncidentId = incidentId;
        Reason = reason;
    }
}

[Serializable, NetSerializable]
public sealed class AutoModCreateTemplateRuleMessage(string reason) : EuiMessageBase
{
    public string Reason { get; } = reason;
}

[Serializable, NetSerializable]
public sealed class AutoModSaveRuleMessage(AutoModEditableRule rule, string reason) : EuiMessageBase
{
    public AutoModEditableRule Rule { get; } = rule;
    public string Reason { get; } = reason;
}

[Serializable, NetSerializable]
public sealed class AutoModSaveRuleJsonMessage : EuiMessageBase
{
    public string Json { get; }
    public string Reason { get; }

    public AutoModSaveRuleJsonMessage(string json, string reason)
    {
        Json = json;
        Reason = reason;
    }
}

[Serializable, NetSerializable]
public sealed class AutoModDeleteRuleMessage : EuiMessageBase
{
    public string RuleId { get; }
    public string Reason { get; }

    public AutoModDeleteRuleMessage(string ruleId, string reason)
    {
        RuleId = ruleId;
        Reason = reason;
    }
}

[Serializable, NetSerializable]
public sealed class AutoModToggleRuleMessage : EuiMessageBase
{
    public string RuleId { get; }
    public bool Enabled { get; }
    public string Reason { get; }

    public AutoModToggleRuleMessage(string ruleId, bool enabled, string reason)
    {
        RuleId = ruleId;
        Enabled = enabled;
        Reason = reason;
    }
}

[Serializable, NetSerializable]
public sealed class AutoModOperationResultMessage : EuiMessageBase
{
    public bool Success { get; }
    public string Message { get; }

    public AutoModOperationResultMessage(bool success, string message)
    {
        Success = success;
        Message = message;
    }
}
