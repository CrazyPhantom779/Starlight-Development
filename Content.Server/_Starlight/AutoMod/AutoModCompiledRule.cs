using System.Text.RegularExpressions;
using Content.Shared._Starlight.AutoMod;

namespace Content.Server._Starlight.AutoMod;

internal sealed class AutoModCompiledRule
{
    public required AutoModRulePrototype Prototype { get; init; }
    public required string Version { get; init; }
    public Regex? Regex { get; init; }
    public HashSet<string> WordSet { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> AllowList { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string ScopeKey => Prototype.Escalation.Scope switch
    {
        AutoModEscalationScopeKind.Rule => $"rule:{Prototype.ID}",
        AutoModEscalationScopeKind.Category => $"category:{Prototype.Category}",
        AutoModEscalationScopeKind.Global => "global",
        AutoModEscalationScopeKind.Custom => $"custom:{Prototype.Escalation.ScopeKey ?? Prototype.Category}",
        _ => $"rule:{Prototype.ID}",
    };
}

internal sealed record AutoModMatch(
    AutoModCompiledRule Rule,
    string? MatchedText,
    int Index,
    int Length);

internal sealed record AutoModEvaluation(
    AutoModMatch? Match,
    AutoModLevelPrototype? Level,
    int PointsBefore,
    int PointsAfter,
    bool CancelSpeech,
    bool RequiresApproval,
    string? Feedback,
    AutoModActionType PrimaryAction);
