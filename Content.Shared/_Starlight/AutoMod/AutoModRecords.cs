using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.AutoMod;

[Serializable, NetSerializable]
public sealed record AutoModIncidentRecord(
    Guid IncidentId,
    NetUserId PlayerUserId,
    string PlayerNameAtTime,
    string? CharacterNameAtTime,
    string ServerId,
    string? RoundId,
    string RuleId,
    string RuleVersion,
    string RulesetVersion,
    string Category,
    AutoModSeverity Severity,
    string Channel,
    DateTime CreatedAtUtc,
    DateTime? DecaysAtUtc,
    string MessageHash,
    string? EvidencePreview,
    string? MatchedTokenRedacted,
    int Points,
    string EscalationScope,
    AutoModIncidentStatus Status,
    AutoModActionType ActionTaken,
    Guid? LinkedNoteId,
    int? LinkedBanId,
    Guid? ApprovalRequestId,
    AutoModSyncStatus SyncStatus);

[Serializable, NetSerializable]
public sealed record AutoModAdjustmentRecord(
    Guid AdjustmentId,
    Guid IncidentId,
    NetUserId AdminUserId,
    DateTime CreatedAtUtc,
    string FieldChanged,
    string? OldValue,
    string? NewValue,
    string Reason);

[Serializable, NetSerializable]
public sealed record AutoModApprovalRequestRecord(
    Guid RequestId,
    Guid IncidentId,
    NetUserId PlayerUserId,
    string RuleId,
    AutoModActionType ProposedAction,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    AutoModApprovalStatus Status,
    NetUserId? DecidedByAdmin,
    string? DecisionReason);

[Serializable, NetSerializable]
public sealed record AutoModPlayerScopeState(
    NetUserId PlayerUserId,
    string ScopeKey,
    int ActivePoints,
    int ActiveIncidentCount,
    DateTime? NextDecayUtc,
    DateTime LastIncidentUtc);

[Serializable, NetSerializable]
public sealed record AutoModRuleSummary(
    string Id,
    string Name,
    bool Enabled,
    int Priority,
    string Category,
    AutoModSeverity Severity,
    AutoModMatchKind MatchKind,
    string EscalationScope,
    string ActionSummary,
    AutoModDiscordLogMode DiscordLogMode,
    string Source);

[Serializable, NetSerializable]
public sealed record AutoModTestResult(
    bool Matched,
    bool CancelSpeech,
    string NormalizedText,
    string? RuleId,
    string? RuleName,
    string? Category,
    AutoModSeverity Severity,
    string? EscalationScope,
    int PointsBefore,
    int PointsAfter,
    string ActionSummary,
    bool RequiresApproval,
    bool WouldDiscordLog,
    string? PlayerFeedback,
    string? EvidencePreview);
