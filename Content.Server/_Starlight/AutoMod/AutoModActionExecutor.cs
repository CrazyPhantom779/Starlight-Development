using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared._Starlight.AutoMod;
using Content.Shared.Database;
using Robust.Shared.Player;

namespace Content.Server._Starlight.AutoMod;

internal sealed class AutoModActionExecutor
{
    private readonly IChatManager _chat;
    private readonly IAdminLogManager _adminLog;
    private readonly AutoModDiscordLogger _discord;
    private readonly HashSet<string> _executed = new();

    public AutoModActionExecutor(IChatManager chat, IAdminLogManager adminLog, AutoModDiscordLogger discord)
    {
        _chat = chat;
        _adminLog = adminLog;
        _discord = discord;
    }

    public void Execute(ICommonSession player, AutoModIncidentRecord incident, AutoModCompiledRule rule, AutoModEditableLevel level, bool shadow, out string? feedback)
    {
        feedback = null;

        var executionKey = $"{incident.IncidentId}:{rule.Prototype.ID}:{incident.ActionTaken}";
        if (!_executed.Add(executionKey))
            return;

        var actionText = DescribeActions(level.Actions);
        var target = $"{player.Name} ({player.UserId})";

        _adminLog.Add(LogType.AdminMessage, LogImpact.Medium,
            $"AutoMod {rule.Prototype.ID} matched {target}: {actionText}; incident={incident.IncidentId}; evidence={incident.EvidencePreview}");

        if (level.NotifyAdmins)
        {
            _chat.SendAdminAlert($"AutoMod: {rule.Prototype.Severity} / {rule.Prototype.Category} / {player.Name} / {actionText} / incident {incident.IncidentId}");
        }

        foreach (var action in level.Actions)
        {
            switch (action.Type)
            {
                case AutoModActionType.Warn:
                    feedback ??= action.Message ?? Loc.GetString("automod-player-warning-generic");
                    break;

                case AutoModActionType.CreateNote:
                    // TODO: Wire this into the branch's real admin-note manager.
                    // This remains as an admin-log mirror until the exact note API is connected.
                    var noteBody = BuildNoteBody(
                        incident,
                        rule,
                        action.Type,
                        action.Severity,
                        action.Expiry,
                        action.Appealable);
                    _adminLog.Add(LogType.AdminMessage, LogImpact.Medium, $"{noteBody}");
                    break;

                case AutoModActionType.Kick:
                    feedback ??= action.Reason ?? Loc.GetString("automod-player-kick-generic");
                    _adminLog.Add(LogType.AdminMessage, LogImpact.High,
                        $"AutoMod kick requested for {target}: {feedback}; incident={incident.IncidentId}");
                    break;

                case AutoModActionType.Ban:
                    feedback ??= action.Reason ?? Loc.GetString("automod-player-ban-generic");
                    _adminLog.Add(LogType.AdminMessage, LogImpact.Extreme,
                        $"AutoMod ban requested for {target}: {feedback}; duration={action.Duration}; incident={incident.IncidentId}");
                    break;
            }
        }

        _discord.Queue(incident, rule, level, actionText);

        if (shadow)
            feedback = null;
    }

    private static string DescribeActions(IEnumerable<AutoModEditableAction> actions)
    {
        var text = string.Join("+", actions.Select(x => x.Type.ToString()));
        return string.IsNullOrWhiteSpace(text) ? "LogOnly" : text;
    }

    private static string BuildNoteBody(
        AutoModIncidentRecord incident,
        AutoModCompiledRule rule,
        AutoModActionType actionType,
        AutoModSeverity severity,
        TimeSpan expiry,
        bool appealable)
    {
        return $"[AUTOMOD {actionType}] {rule.Prototype.Category} / {rule.Prototype.Severity}\n" +
               $"Rule: {rule.Prototype.Name} ({rule.Prototype.ID})\n" +
               $"Incident: {incident.IncidentId}\n" +
               $"Action: {incident.ActionTaken}\n" +
               $"Severity: {severity}\n" +
               $"Expires: {(expiry <= TimeSpan.Zero ? "Never" : DateTime.UtcNow.Add(expiry).ToString("u"))}\n" +
               $"Appealable: {(appealable ? "Yes" : "No")}\n\n" +
               $"Evidence preview:\n{incident.EvidencePreview ?? "<none>"}\n\n" +
               "This note was generated from a structured AutoMod incident. Editing this note does not change AutoMod escalation.";
    }
}
