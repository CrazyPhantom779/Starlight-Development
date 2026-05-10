using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared._Starlight.AutoMod;
using Content.Shared.Chat;
using Content.Shared.Database;
using Robust.Shared.Player;
using Robust.Shared.Utility;

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

    public void Execute(ICommonSession player, AutoModIncidentRecord incident, AutoModCompiledRule rule, AutoModLevelPrototype level, bool shadow, out string? feedback)
    {
        feedback = null;
        var executionKey = $"{incident.IncidentId}:{rule.Prototype.ID}:{incident.ActionTaken}";
        if (!_executed.Add(executionKey))
            return;

        var actionText = DescribeActions(level.Actions);
        _adminLog.Add(LogType.AdminMessage, LogImpact.Medium, $"AutoMod {rule.Prototype.ID} matched {player:Player}: {actionText}; incident={incident.IncidentId}; evidence={incident.EvidencePreview}");

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
                    // Real admin-note APIs vary across Starlight branches. This logs the note body now and keeps the text deterministic.
                    _adminLog.Add(LogType.AdminMessage, LogImpact.Medium, BuildNoteBody(incident, rule, action));
                    break;
                case AutoModActionType.Kick:
                    // The actual kick should be wired through the branch's existing kick helper after local API confirmation.
                    feedback ??= action.Reason ?? Loc.GetString("automod-player-kick-generic");
                    _adminLog.Add(LogType.AdminMessage, LogImpact.High, $"AutoMod kick requested for {player:Player}: {feedback}; incident={incident.IncidentId}");
                    break;
                case AutoModActionType.Ban:
                    // The actual ban should be wired through the branch's existing ban helper after local API confirmation.
                    feedback ??= action.Reason ?? Loc.GetString("automod-player-ban-generic");
                    _adminLog.Add(LogType.AdminMessage, LogImpact.High, $"AutoMod ban requested for {player:Player}: {feedback}; duration={action.Duration}; incident={incident.IncidentId}");
                    break;
            }
        }

        _discord.QueueIncident(incident, rule, level);
    }

    private static string DescribeActions(IEnumerable<AutoModActionPrototype> actions)
    {
        var list = actions.Select(x => x.Type.ToString()).Distinct().ToArray();
        return list.Length == 0 ? "LogOnly" : string.Join("+", list);
    }

    private static string BuildNoteBody(AutoModIncidentRecord incident, AutoModCompiledRule rule, AutoModActionPrototype action)
    {
        return $"[AUTOMOD {action.Severity}] {rule.Prototype.Category} / {rule.Prototype.Name}\n" +
               $"Rule: {rule.Prototype.ID}\n" +
               $"Incident: {incident.IncidentId}\n" +
               $"Action: {incident.ActionTaken}\n" +
               $"Expires: {incident.DecaysAtUtc:O}\n" +
               $"Appealable: {action.Appealable}\n" +
               $"Evidence: {incident.EvidencePreview}\n\n" +
               "This note was generated from a structured AutoMod incident. Editing this note does not affect AutoMod escalation.";
    }
}
