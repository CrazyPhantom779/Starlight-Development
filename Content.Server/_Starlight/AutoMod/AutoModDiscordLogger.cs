using Content.Server.Discord;
using Content.Shared._Starlight.AutoMod;
using Content.Shared.CCVar;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Log;

namespace Content.Server._Starlight.AutoMod;

internal sealed class AutoModDiscordLogger
{
    private readonly IConfigurationManager _cfg;
    private readonly DiscordWebhook _discord;
    private readonly ISawmill _sawmill;
    private readonly Queue<(AutoModIncidentRecord Incident, string Message)> _queue = new();

    private WebhookIdentifier? _webhookId;

    public AutoModDiscordLogger(IConfigurationManager cfg, DiscordWebhook discord, ISawmill sawmill)
    {
        _cfg = cfg;
        _discord = discord;
        _sawmill = sawmill;
    }

    public void RefreshWebhook()
    {
        _webhookId = null;

        if (!_cfg.GetCVar(StarlightCCVars.AutoModDiscordEnabled))
            return;

        var webhook = _cfg.GetCVar(StarlightCCVars.AutoModDiscordWebhook);
        if (string.IsNullOrWhiteSpace(webhook))
            return;

        _discord.GetWebhook(webhook, data => _webhookId = data.ToIdentifier());
    }

    public void Queue(AutoModIncidentRecord incident, AutoModCompiledRule rule, AutoModEditableLevel level, string actionText)
    {
        if (!_cfg.GetCVar(StarlightCCVars.AutoModDiscordEnabled))
            return;

        if (!ShouldLog(incident, rule, level))
            return;

        var message = $"Rule: {rule.Prototype.Name} ({rule.Prototype.ID})\n" +
                      $"Category: {rule.Prototype.Category}\n" +
                      $"Severity: {rule.Prototype.Severity}\n" +
                      $"Player: {incident.PlayerNameAtTime} ({incident.PlayerUserId})\n" +
                      $"Action: {actionText}\n" +
                      $"Incident: {incident.IncidentId}\n" +
                      $"Evidence: {incident.EvidencePreview ?? "<none>"}";

        _queue.Enqueue((incident, message));
    }

    public async void Flush()
    {
        if (_webhookId is null)
            return;

        while (_queue.TryDequeue(out var entry))
        {
            try
            {
                var embed = new WebhookEmbed
                {
                    Title = "AutoMod incident",
                    Description = entry.Message,
                    Footer = new WebhookEmbedFooter
                    {
                        Text = $"Server: {_cfg.GetCVar(CCVars.AdminLogsServerName)} | Incident: {entry.Incident.IncidentId}",
                    },
                };

                var payload = new WebhookPayload { Embeds = [embed] };
                await _discord.CreateMessage(_webhookId.Value, payload);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to send AutoMod Discord log: {e}");
                break;
            }
        }
    }

    private static bool ShouldLog(AutoModIncidentRecord incident, AutoModCompiledRule rule, AutoModEditableLevel level)
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
