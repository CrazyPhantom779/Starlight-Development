namespace Content.Shared._Starlight.AutoMod;

public enum AutoModSeverity : byte
{
    Info,
    Low,
    Medium,
    High,
    Critical,
}

public enum AutoModMatchKind : byte
{
    WordSet,
    Regex,
    RateLimit,
    Custom,
}

public enum AutoModNormalizationMode : byte
{
    None,
    Basic,
    Aggressive,
    RaidResistant,
}

public enum AutoModEscalationScopeKind : byte
{
    Rule,
    Category,
    Global,
    Custom,
}

public enum AutoModActionMode : byte
{
    Immediate,
    RequireApproval,
}

public enum AutoModActionType : byte
{
    LogOnly,
    BlockMessage,
    Warn,
    CreateNote,
    Kick,
    Ban,
    NotifyAdmins,
    DiscordLog,
}

public enum AutoModIncidentStatus : byte
{
    Active,
    Decayed,
    FalsePositive,
    ManuallyDisabled,
    PendingApproval,
    RejectedByAdmin,
    ActionFailed,
}

public enum AutoModSyncStatus : byte
{
    LocalOnly,
    Pending,
    Synced,
    Failed,
}

public enum AutoModEvidenceMode : byte
{
    None,
    HashOnly,
    RedactedPreview,
    FullPreviewStaffOnly,
}

public enum AutoModDiscordLogMode : byte
{
    Never,
    AllIncidents,
    PunishmentsOnly,
    NotesKicksBans,
    BansOnly,
    ApprovalRequestsOnly,
}

public enum AutoModAdminedPolicy : byte
{
    Ignore,
    LogOnly,
    Moderate,
}

public enum AutoModApprovalStatus : byte
{
    Pending,
    Approved,
    Rejected,
    Expired,
}
