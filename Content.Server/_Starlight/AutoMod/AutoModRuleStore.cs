using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Server.Administration.Logs;
using Content.Shared._Starlight.AutoMod;
using Content.Shared.Chat;
using Content.Shared.Database;

namespace Content.Server._Starlight.AutoMod;

/// <summary>
/// Persistent runtime store for AutoMod rules.
///
/// This intentionally does not read YAML/prototypes. The saved runtime JSON document is the only rule source
/// so admins can create, edit, disable, and delete every rule from the in-game AutoMod UI without touching the codebase.
/// </summary>
internal sealed class AutoModRuleStore
{
    private const string DirectoryName = "data";
    private const string FileName = "automod_rules.json";
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = false,
    };

    private readonly IAdminLogManager _adminLog;
    private readonly string _path;
    private List<AutoModEditableRule> _rules = new();

    public AutoModRuleStore(IAdminLogManager adminLog)
    {
        _adminLog = adminLog;
        _path = System.IO.Path.Combine(AppContext.BaseDirectory, DirectoryName, FileName);
    }

    public IReadOnlyList<AutoModEditableRule> Rules => _rules;
    public string Path => _path;

    public void Load()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);

        if (!File.Exists(_path))
        {
            _rules = new List<AutoModEditableRule>();
            Save("created empty AutoMod runtime rule store");
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var document = JsonSerializer.Deserialize<AutoModRuleStoreDocument>(json, JsonOptions);
            if (document == null || document.SchemaVersion != CurrentSchemaVersion)
            {
                ResetInvalidStore("rule store was missing or used an unsupported schema version");
                return;
            }

            _rules = Sanitize(document.Rules ?? new List<AutoModEditableRule>());
        }
        catch (Exception e)
        {
            ResetInvalidStore($"failed to load saved rules: {e.Message}");
        }
    }

    public List<AutoModEditableRule> GetClonedRules()
    {
        return _rules.Select(x => x.Clone()).ToList();
    }

    public string ToJson(AutoModEditableRule rule)
    {
        return JsonSerializer.Serialize(rule, JsonOptions);
    }

    public bool TryFromJson(string json, out AutoModEditableRule? rule, out string error)
    {
        rule = null;
        error = string.Empty;

        try
        {
            rule = JsonSerializer.Deserialize<AutoModEditableRule>(json, JsonOptions);
            if (rule == null)
            {
                error = "Rule JSON did not deserialize into a rule.";
                return false;
            }

            rule = Sanitize(rule);
            return true;
        }
        catch (Exception e)
        {
            error = $"Invalid rule JSON: {e.Message}";
            return false;
        }
    }

    public AutoModEditableRule CreateTemplate(string admin, string reason)
    {
        var id = $"automod-rule-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var rule = CreateTemplateRule(id);
        _rules.Add(rule);
        Save($"template rule {id} created by {admin}: {reason}");
        return rule.Clone();
    }

    public bool Upsert(AutoModEditableRule rule, string admin, string reason, out string error)
    {
        error = string.Empty;
        rule = Sanitize(rule);

        if (string.IsNullOrWhiteSpace(rule.ID))
        {
            error = "Rule ID cannot be empty.";
            return false;
        }

        var existing = _rules.FindIndex(x => string.Equals(x.ID, rule.ID, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
            _rules[existing] = rule;
        else
            _rules.Add(rule);

        Save($"rule {rule.ID} saved by {admin}: {reason}");
        return true;
    }

    public bool Delete(string id, string admin, string reason)
    {
        var removed = _rules.RemoveAll(x => string.Equals(x.ID, id, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
            return false;

        Save($"rule {id} deleted by {admin}: {reason}");
        return true;
    }

    public bool SetEnabled(string id, bool enabled, string admin, string reason)
    {
        var rule = _rules.FirstOrDefault(x => string.Equals(x.ID, id, StringComparison.OrdinalIgnoreCase));
        if (rule == null)
            return false;

        rule.Enabled = enabled;
        Save($"rule {id} enabled={enabled} by {admin}: {reason}");
        return true;
    }

    private void Save(string audit)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        var document = new AutoModRuleStoreDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            SavedAt = DateTime.UtcNow,
            Rules = _rules,
        };

        File.WriteAllText(_path, JsonSerializer.Serialize(document, JsonOptions));
        _adminLog.Add(LogType.AdminMessage, LogImpact.Medium, $"AutoMod rules saved: {audit}");
    }

    private void ResetInvalidStore(string reason)
    {
        _rules = new List<AutoModEditableRule>();
        Save($"reset invalid AutoMod runtime rule store: {reason}");
        _adminLog.Add(LogType.AdminMessage, LogImpact.High,
            $"AutoMod rule store at {_path} was reset to an empty clean schema. Reason: {reason}");
    }

    private static List<AutoModEditableRule> Sanitize(IEnumerable<AutoModEditableRule> rules)
    {
        return rules.Select(Sanitize)
            .Where(x => !string.IsNullOrWhiteSpace(x.ID))
            .GroupBy(x => x.ID, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.ID)
            .ToList();
    }

    private static AutoModEditableRule Sanitize(AutoModEditableRule rule)
    {
        rule.ID = rule.ID.Trim();
        rule.Name = string.IsNullOrWhiteSpace(rule.Name) ? rule.ID : rule.Name.Trim();
        rule.Description = rule.Description?.Trim() ?? string.Empty;
        rule.Category = string.IsNullOrWhiteSpace(rule.Category) ? "General" : rule.Category.Trim();
        rule.Channels = rule.Channels
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        rule.Match.WordSet = null;
        rule.Match.Pattern = string.IsNullOrWhiteSpace(rule.Match.Pattern) ? null : rule.Match.Pattern.Trim();
        rule.Match.Words = rule.Match.Words
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        rule.Match.AllowList = rule.Match.AllowList
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        rule.Evidence.MaxPreviewLength = Math.Clamp(rule.Evidence.MaxPreviewLength, 8, 1000);
        rule.Escalation.PointsPerIncident = Math.Max(0, rule.Escalation.PointsPerIncident);
        rule.Levels = rule.Levels.OrderBy(x => x.MinPoints).ToList();
        rule.Source = "RuntimeUI";
        return rule;
    }

    private static AutoModEditableRule CreateTemplateRule(string id)
    {
        return new AutoModEditableRule
        {
            ID = id,
            Name = "New AutoMod Rule",
            Description = "Created in-game. Fill in match words/regex, levels, actions, and enable when ready.",
            Enabled = false,
            Priority = 100,
            Category = "General",
            Severity = AutoModSeverity.Low,
            Channels = new List<string> { ChatChannel.OOC.ToString(), ChatChannel.LOOC.ToString() },
            AdminPolicy = new AutoModEditableAdminPolicy
            {
                AdminedUsers = AutoModAdminedPolicy.Ignore,
                DeadminnedUsers = AutoModAdminedPolicy.Moderate,
                IgnoreAdminChannels = true,
            },
            Match = new AutoModEditableMatch
            {
                Kind = AutoModMatchKind.WordSet,
                WordSet = null,
                Words = new List<string>(),
                AllowList = new List<string>(),
                Normalization = AutoModNormalizationMode.Basic,
                RequireWordBoundary = true,
                Window = TimeSpan.FromSeconds(10),
                MaxMessages = 5,
                SimilarityThreshold = 0.85f,
            },
            Evidence = new AutoModEditableEvidence
            {
                Mode = AutoModEvidenceMode.RedactedPreview,
                MaxPreviewLength = 160,
                ShowMatchedTokenToAdmins = true,
            },
            Escalation = new AutoModEditableEscalation
            {
                Scope = AutoModEscalationScopeKind.Rule,
                ScopeKey = null,
                PointsPerIncident = 1,
                Decay = TimeSpan.FromDays(30),
                IncludeFalsePositives = false,
                IncludeDecayed = false,
            },
            Discord = new AutoModEditableDiscord
            {
                LogMode = AutoModDiscordLogMode.PunishmentsOnly,
                MinimumAction = AutoModActionType.CreateNote,
                PingRolesOn = new List<AutoModActionType> { AutoModActionType.Ban },
            },
            Levels = new List<AutoModEditableLevel>
            {
                new()
                {
                    MinPoints = 1,
                    CancelSpeech = true,
                    NotifyAdmins = true,
                    ActionMode = AutoModActionMode.Immediate,
                    Approval = new AutoModEditableApproval
                    {
                        Timeout = TimeSpan.FromMinutes(3),
                        RequiredPermission = "AutoModAuthorizeAction",
                        AllowSpeechWhilePending = false,
                    },
                    Actions = new List<AutoModEditableAction>
                    {
                        new()
                        {
                            Type = AutoModActionType.Warn,
                            Message = "Your message was blocked by AutoMod.",
                            Severity = AutoModSeverity.Low,
                            Expiry = TimeSpan.FromDays(30),
                            Appealable = true,
                        },
                        new()
                        {
                            Type = AutoModActionType.CreateNote,
                            Severity = AutoModSeverity.Low,
                            Expiry = TimeSpan.FromDays(30),
                            Appealable = true,
                        },
                    },
                },
            },
            Source = "RuntimeUI",
        };
    }

    private sealed class AutoModRuleStoreDocument
    {
        public int SchemaVersion { get; set; }
        public DateTime SavedAt { get; set; }
        public List<AutoModEditableRule>? Rules { get; set; }
    }
}
