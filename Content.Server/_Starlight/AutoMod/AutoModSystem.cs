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
using Robust.Shared.Log;

namespace Content.Server._Starlight.AutoMod;

public sealed class AutoModSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly DiscordWebhook _discordWebhook = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private AutoModRuleStore _ruleStore = default!;
    private AutoModRuleCompiler _compiler = default!;
    private AutoModEngine _engine = default!;
    private AutoModStateService _state = default!;
    private AutoModDiscordLogger _discord = default!;
    private AutoModNullLinkSync _nullLink = default!;
    private AutoModActionExecutor _actions = default!;
    private ISawmill _sawmill = default!;

    public string RulesetVersion => _compiler.RulesetVersion;
    public IReadOnlyList<AutoModIncidentRecord> RecentIncidents => _state.GetRecent(250);
    public bool NullLinkHealthy => _nullLink.Healthy;
    public int UnsyncedCount => _state.CountUnsynced();
    public string RuleStorePath => _ruleStore.Path;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("automod");
        _ruleStore = new AutoModRuleStore(_adminLog);
        _ruleStore.Load();

        _compiler = new AutoModRuleCompiler(_ruleStore);
        _engine = new AutoModEngine();
        _state = new AutoModStateService();
        _discord = new AutoModDiscordLogger(_cfg, _discordWebhook, _sawmill);
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
        _adminLog.Add(LogType.AdminMessage, LogImpact.Low, $"AutoMod runtime rules loaded: {_compiler.Rules.Count} enabled compiled rules; saved rules={_ruleStore.Rules.Count}; version={_compiler.RulesetVersion}");
    }

    public IReadOnlyList<AutoModEditableRule> GetEditableRules()
    {
        return _ruleStore.GetClonedRules();
    }

    public string GetRuleJson(string ruleId)
    {
        var rule = _ruleStore.GetClonedRules().FirstOrDefault(x => string.Equals(x.ID, ruleId, StringComparison.OrdinalIgnoreCase));
        return rule == null ? string.Empty : _ruleStore.ToJson(rule);
    }

    public AutoModEditableRule CreateTemplateRule(NetUserId admin, string reason)
    {
        var rule = _ruleStore.CreateTemplate(admin.ToString(), reason);
        ReloadRules();
        return rule;
    }

    public string? ValidateRule(AutoModEditableRule rule)
    {
        return _compiler.Validate(rule);
    }

    public bool SaveRule(AutoModEditableRule rule, NetUserId admin, string reason, out string error)
    {
        var validation = ValidateRule(rule);
        if (validation != null)
        {
            error = validation;
            return false;
        }

        if (!_ruleStore.Upsert(rule, admin.ToString(), reason, out error))
            return false;

        ReloadRules();
        return true;
    }

    public bool SaveRuleJson(string json, NetUserId admin, string reason, out string error)
    {
        if (!_ruleStore.TryFromJson(json, out var rule, out error) || rule == null)
            return false;

        return SaveRule(rule, admin, reason, out error);
    }

    public bool DeleteRule(string ruleId, NetUserId admin, string reason)
    {
        var deleted = _ruleStore.Delete(ruleId, admin.ToString(), reason);
        if (deleted)
            ReloadRules();
        return deleted;
    }

    public bool SetRuleEnabled(string ruleId, bool enabled, NetUserId admin, string reason)
    {
        var changed = _ruleStore.SetEnabled(ruleId, enabled, admin.ToString(), reason);
        if (changed)
            ReloadRules();
        return changed;
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
        DateTime? decaysAt = rule.Prototype.Escalation.Decay <= TimeSpan.Zero
            ? null
            : now + rule.Prototype.Escalation.Decay;

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

        if (_cfg.GetCVar(StarlightCCVars.AutoModNullLinkEnabled))
            _nullLink.QueueIncident(incident);

        var shadow = _cfg.GetCVar(StarlightCCVars.AutoModShadowMode);
        _actions.Execute(player, incident, rule, level, shadow, out feedback);

        if (shadow)
            return false;

        return level.CancelSpeech && (level.ActionMode == AutoModActionMode.Immediate || !level.Approval.AllowSpeechWhilePending);
    }

    public AutoModTestResult Test(string text, string channel, string? ruleId, int mockPoints)
    {
        var rules = ruleId == null
            ? _compiler.Rules
            : _compiler.Rules.Where(x => x.Prototype.ID == ruleId).ToList();

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
        return _ruleStore.GetClonedRules().Select(rule => new AutoModRuleSummary(
                rule.ID,
                rule.Name,
                rule.Enabled,
                rule.Priority,
                rule.Category,
                rule.Severity,
                rule.Match.Kind,
                rule.Escalation.Scope == AutoModEscalationScopeKind.Rule ? $"rule:{rule.ID}" : rule.Escalation.ScopeKey ?? rule.Category,
                string.Join(" / ", rule.Levels.Select(l => $">={l.MinPoints}:" + string.Join('+', l.Actions.Select(a => a.Type)))),
                rule.Discord.LogMode,
                rule.Source))
            .ToList();
    }

    public bool MarkFalsePositive(Guid incidentId, NetUserId admin, string reason)
    {
        var changed = _state.MarkFalsePositive(incidentId, admin, reason);
        if (changed)
            _adminLog.Add(LogType.AdminMessage, LogImpact.Medium, $"AutoMod incident {incidentId} marked false positive by {admin}: {reason}");

        return changed;
    }

    private static AutoModEditableLevel? SelectLevel(AutoModCompiledRule rule, int points)
    {
        return rule.Prototype.Levels
            .Where(x => x.MinPoints <= points)
            .OrderByDescending(x => x.MinPoints)
            .FirstOrDefault();
    }

    private static AutoModActionType PrimaryAction(List<AutoModEditableAction> actions, bool cancelSpeech)
    {
        if (actions.Any(x => x.Type == AutoModActionType.Ban)) return AutoModActionType.Ban;
        if (actions.Any(x => x.Type == AutoModActionType.Kick)) return AutoModActionType.Kick;
        if (actions.Any(x => x.Type == AutoModActionType.CreateNote)) return AutoModActionType.CreateNote;
        if (actions.Any(x => x.Type == AutoModActionType.Warn)) return AutoModActionType.Warn;
        return cancelSpeech ? AutoModActionType.BlockMessage : AutoModActionType.LogOnly;
    }

    private static string? BuildEvidence(string message, AutoModEditableEvidence evidence)
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
