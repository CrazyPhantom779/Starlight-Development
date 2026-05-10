using System.Linq;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.AutoMod;

/// <summary>
/// Runtime/editable AutoMod rule data.
/// This is the complete rule format saved by the in-game AutoMod UI.
/// No YAML/prototype rules or word sets are used by runtime AutoMod.
/// </summary>
[Serializable, NetSerializable]
public sealed class AutoModEditableRule
{
    public string ID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public string Category { get; set; } = "General";
    public AutoModSeverity Severity { get; set; } = AutoModSeverity.Low;
    public List<string> Channels { get; set; } = new();
    public AutoModEditableAdminPolicy AdminPolicy { get; set; } = new();
    public AutoModEditableMatch Match { get; set; } = new();
    public AutoModEditableEvidence Evidence { get; set; } = new();
    public AutoModEditableEscalation Escalation { get; set; } = new();
    public AutoModEditableDiscord Discord { get; set; } = new();
    public List<AutoModEditableLevel> Levels { get; set; } = new();
    public string Source { get; set; } = "RuntimeUI";

    public AutoModEditableRule Clone()
    {
        return new AutoModEditableRule
        {
            ID = ID,
            Name = Name,
            Description = Description,
            Enabled = Enabled,
            Priority = Priority,
            Category = Category,
            Severity = Severity,
            Channels = Channels.ToList(),
            AdminPolicy = AdminPolicy.Clone(),
            Match = Match.Clone(),
            Evidence = Evidence.Clone(),
            Escalation = Escalation.Clone(),
            Discord = Discord.Clone(),
            Levels = Levels.Select(x => x.Clone()).ToList(),
            Source = Source,
        };
    }
}

[Serializable, NetSerializable]
public sealed class AutoModEditableAdminPolicy
{
    public AutoModAdminedPolicy AdminedUsers { get; set; } = AutoModAdminedPolicy.Ignore;
    public AutoModAdminedPolicy DeadminnedUsers { get; set; } = AutoModAdminedPolicy.Moderate;
    public bool IgnoreAdminChannels { get; set; } = true;

    public AutoModEditableAdminPolicy Clone()
    {
        return new AutoModEditableAdminPolicy
        {
            AdminedUsers = AdminedUsers,
            DeadminnedUsers = DeadminnedUsers,
            IgnoreAdminChannels = IgnoreAdminChannels,
        };
    }
}

[Serializable, NetSerializable]
public sealed class AutoModEditableMatch
{
    public AutoModMatchKind Kind { get; set; } = AutoModMatchKind.WordSet;
    public string? WordSet { get; set; }
    public string? Pattern { get; set; }
    public List<string> Words { get; set; } = new();
    public List<string> AllowList { get; set; } = new();
    public AutoModNormalizationMode Normalization { get; set; } = AutoModNormalizationMode.Basic;
    public bool RequireWordBoundary { get; set; } = true;
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxMessages { get; set; } = 5;
    public float SimilarityThreshold { get; set; } = 0.85f;

    public AutoModEditableMatch Clone()
    {
        return new AutoModEditableMatch
        {
            Kind = Kind,
            WordSet = WordSet,
            Pattern = Pattern,
            Words = Words.ToList(),
            AllowList = AllowList.ToList(),
            Normalization = Normalization,
            RequireWordBoundary = RequireWordBoundary,
            Window = Window,
            MaxMessages = MaxMessages,
            SimilarityThreshold = SimilarityThreshold,
        };
    }
}

[Serializable, NetSerializable]
public sealed class AutoModEditableEvidence
{
    public AutoModEvidenceMode Mode { get; set; } = AutoModEvidenceMode.RedactedPreview;
    public int MaxPreviewLength { get; set; } = 160;
    public bool ShowMatchedTokenToAdmins { get; set; } = true;

    public AutoModEditableEvidence Clone()
    {
        return new AutoModEditableEvidence
        {
            Mode = Mode,
            MaxPreviewLength = MaxPreviewLength,
            ShowMatchedTokenToAdmins = ShowMatchedTokenToAdmins,
        };
    }
}

[Serializable, NetSerializable]
public sealed class AutoModEditableEscalation
{
    public AutoModEscalationScopeKind Scope { get; set; } = AutoModEscalationScopeKind.Rule;
    public string? ScopeKey { get; set; }
    public int PointsPerIncident { get; set; } = 1;
    public TimeSpan Decay { get; set; } = TimeSpan.FromDays(30);
    public bool IncludeFalsePositives { get; set; }
    public bool IncludeDecayed { get; set; }

    public AutoModEditableEscalation Clone()
    {
        return new AutoModEditableEscalation
        {
            Scope = Scope,
            ScopeKey = ScopeKey,
            PointsPerIncident = PointsPerIncident,
            Decay = Decay,
            IncludeFalsePositives = IncludeFalsePositives,
            IncludeDecayed = IncludeDecayed,
        };
    }
}

[Serializable, NetSerializable]
public sealed class AutoModEditableDiscord
{
    public AutoModDiscordLogMode LogMode { get; set; } = AutoModDiscordLogMode.PunishmentsOnly;
    public AutoModActionType MinimumAction { get; set; } = AutoModActionType.CreateNote;
    public List<AutoModActionType> PingRolesOn { get; set; } = new();

    public AutoModEditableDiscord Clone()
    {
        return new AutoModEditableDiscord
        {
            LogMode = LogMode,
            MinimumAction = MinimumAction,
            PingRolesOn = PingRolesOn.ToList(),
        };
    }
}

[Serializable, NetSerializable]
public sealed class AutoModEditableLevel
{
    public int MinPoints { get; set; } = 1;
    public bool CancelSpeech { get; set; }
    public bool NotifyAdmins { get; set; } = true;
    public AutoModActionMode ActionMode { get; set; } = AutoModActionMode.Immediate;
    public AutoModEditableApproval Approval { get; set; } = new();
    public List<AutoModEditableAction> Actions { get; set; } = new();

    public AutoModEditableLevel Clone()
    {
        return new AutoModEditableLevel
        {
            MinPoints = MinPoints,
            CancelSpeech = CancelSpeech,
            NotifyAdmins = NotifyAdmins,
            ActionMode = ActionMode,
            Approval = Approval.Clone(),
            Actions = Actions.Select(x => x.Clone()).ToList(),
        };
    }
}

[Serializable, NetSerializable]
public sealed class AutoModEditableApproval
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(3);
    public string RequiredPermission { get; set; } = "AutoModAuthorizeAction";
    public bool AllowSpeechWhilePending { get; set; }

    public AutoModEditableApproval Clone()
    {
        return new AutoModEditableApproval
        {
            Timeout = Timeout,
            RequiredPermission = RequiredPermission,
            AllowSpeechWhilePending = AllowSpeechWhilePending,
        };
    }
}

[Serializable, NetSerializable]
public sealed class AutoModEditableAction
{
    public AutoModActionType Type { get; set; } = AutoModActionType.LogOnly;
    public string? Message { get; set; }
    public string? Reason { get; set; }
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public AutoModSeverity Severity { get; set; } = AutoModSeverity.Low;
    public TimeSpan Expiry { get; set; } = TimeSpan.FromDays(30);
    public bool Appealable { get; set; } = true;

    public AutoModEditableAction Clone()
    {
        return new AutoModEditableAction
        {
            Type = Type,
            Message = Message,
            Reason = Reason,
            Duration = Duration,
            Severity = Severity,
            Expiry = Expiry,
            Appealable = Appealable,
        };
    }
}
