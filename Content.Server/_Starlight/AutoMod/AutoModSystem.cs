using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Discord;
using Content.Shared._Starlight.AutoMod;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.AutoMod;

public sealed class AutoModSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly DiscordWebhook _discordWebhook = default!;

    private AutoModRuleCompiler _compiler = default!;
    private AutoModEngine _engine = default!;
    private AutoModStateService _state = default!;
    private AutoModDiscordLogger _discord = default!;
    private AutoModNullLinkSync _nullLink = default!;
    private AutoModActionExecutor _actions = default!;

    public string RulesetVersion => _compiler.RulesetVersion;
    public IReadOnlyList<AutoModIncidentRecord> RecentIncidents => _state.GetRecent(250);
    public bool NullLinkHealthy => _nullLink.Healthy;
    public int UnsyncedCount => _state.CountUnsynced();

    public override void Initialize()
    {
        base.Initialize();

        _compiler = new AutoModRuleCompiler(_prototypes);
        _engine = new AutoModEngine();
        _state = new AutoModStateService();
        _discord = new AutoModDiscordLogger(_cfg, _discordWebhook);
        _nullLink = new AutoModNullLinkSync(_cfg);
        _actions = new AutoModActionExecutor(_chat, _adminLog, _discord);

        ReloadRules();
        _discord.RefreshWebhook();
        _nullLink.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _discord.Flush();
    }

    public void ReloadRules()
    {
        _compiler.Reload();
        _adminLog.Add(LogType.AdminMessage, LogImpact.Low, $"AutoMod rules loaded: {_compiler.Rules.Count} rules; version={_compiler.RulesetVersion}");
    }

    public bool TryCheckChat(ICommonSession player, EntityUid? speaker, string message, ChatChannel channel, out string? feedback)
    {
        feedback = null;

        if (!_cfg.GetCVar(StarlightCCVars.AutoModEnabled))
            return false;

        var channelName = channel.ToString();
        var match = _engine.Evaluate(message, channelName, _compiler.Rules);
        if (match == null)
            return false;

        var rule = match.Rule;
        if (rule.Prototype.AdminPolicy.IgnoreAdminChannels && (channel & ChatChannel.AdminRelated) != 0)
            return false;

        var now = DateTime.UtcNow;
        var before = _state.GetActivePoints(player.UserId, rule.ScopeKey, now);
        var after = before + rule.Prototype.Escalation.PointsPerIncident;
        var level = SelectLevel(rule, after);
        if (level == null)
            return false;

        var primaryAction = PrimaryAction(level.Actions, level.CancelSpeech);
        var decaysAt = rule.Prototype.Escalation.Decay <= TimeSpan.Zero ? null : now + rule.Prototype.Escalation.Decay;
        var incident = new AutoModIncidentRecord(
            Guid.NewGuid(),
            player.UserId,
            player.Name,
            speaker?.ToString(),
            "local-server",
            null,
            rule.Prototype.ID,
            rule.Version,
            _compiler.RulesetVersion,
            rule.Prototype.Category,
            rule.Prototype.Severity,
            channelName,
            now,
            decaysAt,
            Hash(message),
            BuildEvidence(message, rule.Prototype.Evidence),
            rule.Prototype.Evidence.ShowMatchedTokenToAdmins ? match.MatchedText : null,
            rule.Prototype.Escalation.PointsPerIncident,
            rule.ScopeKey,
            level.ActionMode == AutoModActionMode.RequireApproval ? AutoModIncidentStatus.PendingApproval : AutoModIncidentStatus.Active,
            primaryAction,
            null,
            null,
            level.ActionMode == AutoModActionMode.RequireApproval ? Guid.NewGuid() : null,
            _cfg.GetCVar(StarlightCCVars.AutoModNullLinkEnabled) ? AutoModSyncStatus.Pending : AutoModSyncStatus.LocalOnly);

        _state.RecordIncident(incident);
        _nullLink.QueueIncident(incident);

        var shadow = _cfg.GetCVar(StarlightCCVars.AutoModShadowMode);
        _actions.Execute(player, incident, rule, level, shadow, out feedback);

        if (shadow)
            return false;

        return level.CancelSpeech && (level.ActionMode == AutoModActionMode.Immediate || !level.Approval.AllowSpeechWhilePending);
    }

    public AutoModTestResult Test(string text, string channel, string? ruleId, int mockPoints)
    {
        var rules = ruleId == null ? _compiler.Rules : _compiler.Rules.Where(x => x.Prototype.ID == ruleId).ToList();
        var match = _engine.Evaluate(text, channel, rules);
        if (match == null)
        {
            return new AutoModTestResult(false, false, AutoModNormalizer.Normalize(text, AutoModNormalizationMode.Basic), null, null, null, AutoModSeverity.Info, null, mockPoints, mockPoints, "No match", false, false, null, null);
        }

        var rule = match.Rule;
        var after = mockPoints + rule.Prototype.Escalation.PointsPerIncident;
        var level = SelectLevel(rule, after);
        var normalized = AutoModNormalizer.Normalize(text, rule.Prototype.Match.Normalization);
        var action = level == null ? AutoModActionType.LogOnly : PrimaryAction(level.Actions, level.CancelSpeech);
        var feedback = level?.Actions.FirstOrDefault(x => x.Type == AutoModActionType.Warn)?.Message;
        return new AutoModTestResult(
            true,
            level?.CancelSpeech ?? false,
            normalized,
            rule.Prototype.ID,
            rule.Prototype.Name,
            rule.Prototype.Category,
            rule.Prototype.Severity,
            rule.ScopeKey,
            mockPoints,
            after,
            action.ToString(),
            level?.ActionMode == AutoModActionMode.RequireApproval,
            rule.Prototype.Discord.LogMode != AutoModDiscordLogMode.Never,
            feedback,
            BuildEvidence(text, rule.Prototype.Evidence));
    }

    public List<AutoModRuleSummary> GetRuleSummaries()
    {
        return _compiler.Rules.Select(rule => new AutoModRuleSummary(
            rule.Prototype.ID,
            rule.Prototype.Name,
            rule.Prototype.Enabled,
            rule.Prototype.Priority,
            rule.Prototype.Category,
            rule.Prototype.Severity,
            rule.Prototype.Match.Kind,
            rule.ScopeKey,
            string.Join(" / ", rule.Prototype.Levels.Select(l => $">={l.MinPoints}:" + string.Join('+', l.Actions.Select(a => a.Type)))),
            rule.Prototype.Discord.LogMode,
            "Prototype"))
            .ToList();
    }

    public bool MarkFalsePositive(Guid incidentId, NetUserId admin, string reason)
    {
        var changed = _state.MarkFalsePositive(incidentId, admin, reason);
        if (changed)
            _adminLog.Add(LogType.AdminMessage, LogImpact.Medium, $"AutoMod incident {incidentId} marked false positive by {admin}: {reason}");
        return changed;
    }

    private static AutoModLevelPrototype? SelectLevel(AutoModCompiledRule rule, int points)
    {
        return rule.Prototype.Levels
            .Where(x => x.MinPoints <= points)
            .OrderByDescending(x => x.MinPoints)
            .FirstOrDefault();
    }

    private static AutoModActionType PrimaryAction(List<AutoModActionPrototype> actions, bool cancelSpeech)
    {
        if (actions.Any(x => x.Type == AutoModActionType.Ban)) return AutoModActionType.Ban;
        if (actions.Any(x => x.Type == AutoModActionType.Kick)) return AutoModActionType.Kick;
        if (actions.Any(x => x.Type == AutoModActionType.CreateNote)) return AutoModActionType.CreateNote;
        if (actions.Any(x => x.Type == AutoModActionType.Warn)) return AutoModActionType.Warn;
        return cancelSpeech ? AutoModActionType.BlockMessage : AutoModActionType.LogOnly;
    }

    private static string? BuildEvidence(string message, AutoModEvidencePrototype evidence)
    {
        return evidence.Mode switch
        {
            AutoModEvidenceMode.None => null,
            AutoModEvidenceMode.HashOnly => Hash(message),
            _ => message.Length <= evidence.MaxPreviewLength ? message : message[..evidence.MaxPreviewLength] + "...",
        };
    }

    private static string Hash(string text)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}
