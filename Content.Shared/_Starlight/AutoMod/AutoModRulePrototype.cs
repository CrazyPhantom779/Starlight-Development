using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Starlight.AutoMod;

[Prototype("automodRule")]
public sealed partial class AutoModRulePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public bool Enabled = true;

    [DataField]
    public int Priority = 0;

    [DataField]
    public string Category = "General";

    [DataField]
    public AutoModSeverity Severity = AutoModSeverity.Low;

    [DataField]
    public List<string> Channels = new();

    [DataField]
    public AutoModAdminPolicyPrototype AdminPolicy = new();

    [DataField(required: true)]
    public AutoModMatchPrototype Match = new();

    [DataField]
    public AutoModEvidencePrototype Evidence = new();

    [DataField]
    public AutoModEscalationPrototype Escalation = new();

    [DataField]
    public AutoModDiscordPrototype Discord = new();

    [DataField]
    public List<AutoModLevelPrototype> Levels = new();
}

[DataDefinition]
public sealed partial class AutoModAdminPolicyPrototype
{
    [DataField]
    public AutoModAdminedPolicy AdminedUsers = AutoModAdminedPolicy.Ignore;

    [DataField]
    public AutoModAdminedPolicy DeadminnedUsers = AutoModAdminedPolicy.Moderate;

    [DataField]
    public bool IgnoreAdminChannels = true;
}

[DataDefinition]
public sealed partial class AutoModMatchPrototype
{
    [DataField]
    public AutoModMatchKind Kind = AutoModMatchKind.WordSet;

    [DataField]
    public string? WordSet;

    [DataField]
    public string? Pattern;

    [DataField]
    public AutoModNormalizationMode Normalization = AutoModNormalizationMode.Basic;

    [DataField]
    public bool RequireWordBoundary = true;

    [DataField]
    public TimeSpan Window = TimeSpan.FromSeconds(10);

    [DataField]
    public int MaxMessages = 5;

    [DataField]
    public float SimilarityThreshold = 0.85f;
}

[DataDefinition]
public sealed partial class AutoModEvidencePrototype
{
    [DataField]
    public AutoModEvidenceMode Mode = AutoModEvidenceMode.RedactedPreview;

    [DataField]
    public int MaxPreviewLength = 160;

    [DataField]
    public bool ShowMatchedTokenToAdmins = true;
}

[DataDefinition]
public sealed partial class AutoModEscalationPrototype
{
    [DataField]
    public AutoModEscalationScopeKind Scope = AutoModEscalationScopeKind.Rule;

    [DataField]
    public string? ScopeKey;

    [DataField]
    public int PointsPerIncident = 1;

    [DataField]
    public TimeSpan Decay = TimeSpan.FromDays(30);

    [DataField]
    public bool IncludeFalsePositives = false;

    [DataField]
    public bool IncludeDecayed = false;
}

[DataDefinition]
public sealed partial class AutoModDiscordPrototype
{
    [DataField]
    public AutoModDiscordLogMode LogMode = AutoModDiscordLogMode.PunishmentsOnly;

    [DataField]
    public AutoModActionType MinimumAction = AutoModActionType.CreateNote;

    [DataField]
    public List<AutoModActionType> PingRolesOn = new();
}

[DataDefinition]
public sealed partial class AutoModLevelPrototype
{
    [DataField]
    public int MinPoints = 1;

    [DataField]
    public bool CancelSpeech = false;

    [DataField]
    public bool NotifyAdmins = true;

    [DataField]
    public AutoModActionMode ActionMode = AutoModActionMode.Immediate;

    [DataField]
    public AutoModApprovalPrototype Approval = new();

    [DataField]
    public List<AutoModActionPrototype> Actions = new();
}

[DataDefinition]
public sealed partial class AutoModApprovalPrototype
{
    [DataField]
    public TimeSpan Timeout = TimeSpan.FromMinutes(3);

    [DataField]
    public string RequiredPermission = "AutoModAuthorizeAction";

    [DataField]
    public bool AllowSpeechWhilePending = false;
}

[DataDefinition]
public sealed partial class AutoModActionPrototype
{
    [DataField]
    public AutoModActionType Type = AutoModActionType.LogOnly;

    [DataField]
    public string? Message;

    [DataField]
    public string? Reason;

    [DataField]
    public TimeSpan Duration = TimeSpan.Zero;

    [DataField]
    public AutoModSeverity Severity = AutoModSeverity.Low;

    [DataField]
    public TimeSpan Expiry = TimeSpan.FromDays(30);

    [DataField]
    public bool Appealable = true;
}

[Prototype("automodWordSet")]
public sealed partial class AutoModWordSetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public string Visibility = "Sensitive";

    [DataField]
    public List<string> Words = new();

    [DataField]
    public List<string> AllowList = new();
}
