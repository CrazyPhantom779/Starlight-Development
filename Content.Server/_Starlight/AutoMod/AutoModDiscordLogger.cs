using Content.Server.Discord;
using Content.Shared._Starlight.AutoMod;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._Starlight.AutoMod;

internal sealed class AutoModDiscordLogger
{
    private readonly IConfigurationManager _cfg;
    private readonly DiscordWebhook _discord;
    private WebhookIdentifier? _webhook;
    private readonly Queue<string> _pending = new();

    public AutoModDiscordLogger(IConfigurationManager cfg, DiscordWebhook discord)
    {
        _cfg = cfg;
        _discord = discord;
    }

    public void RefreshWebhook()
    {
        _webhook = null;
        if (!_cfg.GetCVar(StarlightCCVars.AutoModDiscordEnabled))
            return;

        var url = _cfg.GetCVar(StarlightCCVars.AutoModDiscordWebhook);
        if (string.IsNullOrWhiteSpace(url))
            return;

        _discord.GetWebhook(url, data => _webhook = data.ToIdentifier());
    }

    public void QueueIncident(AutoModIncidentRecord incident, AutoModCompiledRule rule, AutoModLevelPrototype level)
    {
        if (!_cfg.GetCVar(StarlightCCVars.AutoModDiscordEnabled))
            return;

        if (!ShouldLog(incident, rule, level))
            return;

        var content = $"**AutoMod {incident.ActionTaken}**\n" +
                      $"Player: `{incident.PlayerNameAtTime}`\n" +
                      $"Rule: `{rule.Prototype.Name}` (`{rule.Prototype.ID}`)\n" +
                      $"Category: `{incident.Category}` Severity: `{incident.Severity}`\n" +
                      $"Scope: `{incident.EscalationScope}` Points: `{incident.Points}`\n" +
                      $"Incident: `{incident.IncidentId}`\n" +
                      $"Evidence: `{incident.EvidencePreview ?? "hash-only"}`";
        _pending.Enqueue(content);
    }

    public async void Flush()
    {
        if (_webhook == null || !_cfg.GetCVar(StarlightCCVars.AutoModDiscordEnabled))
            return;

        while (_pending.TryDequeue(out var message))
        {
            await _discord.CreateMessage(_webhook.Value, message);
        }
    }

    private static bool ShouldLog(AutoModIncidentRecord incident, AutoModCompiledRule rule, AutoModLevelPrototype level)
    {
        return rule.Prototype.Discord.LogMode switch
        {
            AutoModDiscordLogMode.Never => false,
            AutoModDiscordLogMode.AllIncidents => true,
            AutoModDiscordLogMode.PunishmentsOnly => incident.ActionTaken != AutoModActionType.LogOnly && incident.ActionTaken != AutoModActionType.BlockMessage,
            AutoModDiscordLogMode.NotesKicksBans => incident.ActionTaken is AutoModActionType.CreateNote or AutoModActionType.Kick or AutoModActionType.Ban,
            AutoModDiscordLogMode.BansOnly => incident.ActionTaken == AutoModActionType.Ban,
            AutoModDiscordLogMode.ApprovalRequestsOnly => level.ActionMode == AutoModActionMode.RequireApproval,
            _ => false,
        };
    }
}
