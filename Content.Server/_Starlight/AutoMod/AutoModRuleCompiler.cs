using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Content.Shared._Starlight.AutoMod;

namespace Content.Server._Starlight.AutoMod;

internal sealed class AutoModRuleCompiler(AutoModRuleStore store)
{
    private readonly AutoModRuleStore _store = store;

    public List<AutoModCompiledRule> Rules { get; private set; } = new();
    public string RulesetVersion { get; private set; } = "unloaded";

    public void Reload()
    {
        var compiled = new List<AutoModCompiledRule>();

        foreach (var rule in _store.Rules)
        {
            if (!rule.Enabled)
                continue;

            try
            {
                var compiledRule = Compile(rule.Clone());
                if (compiledRule != null)
                    compiled.Add(compiledRule);
            }
            catch
            {
                // Bad admin-created rules should not kill the entire ruleset.
                // Validation errors are shown in the UI tester/editor; runtime compile skips invalid enabled entries.
            }
        }

        Rules = compiled
            .OrderByDescending(x => x.Prototype.Priority)
            .ThenBy(x => x.Prototype.ID)
            .ToList();

        RulesetVersion = BuildVersion(Rules);
    }

    public string? Validate(AutoModEditableRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.ID))
            return "Rule ID cannot be empty.";

        // Disabled rules are drafts. Let admins save incomplete rules from the UI and enable them later.
        if (!rule.Enabled)
            return null;

        if (rule.Levels.Count == 0)
            return "Enabled rule must have at least one level.";

        if (rule.Channels.Count == 0)
            return "Enabled rule must apply to at least one channel.";

        if (rule.Match.Kind == AutoModMatchKind.Regex)
        {
            if (string.IsNullOrWhiteSpace(rule.Match.Pattern))
                return "Regex rule requires a pattern.";

            try
            {
                _ = new Regex(rule.Match.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(75));
            }
            catch (Exception e)
            {
                return $"Invalid regex: {e.Message}";
            }
        }

        if (rule.Match.Kind == AutoModMatchKind.WordSet && rule.Match.Words.Count == 0)
            return "WordSet rule requires inline words. YAML/prototype wordsets are intentionally not used.";

        return null;
    }

    private AutoModCompiledRule? Compile(AutoModEditableRule rule)
    {
        var validation = Validate(rule);
        if (validation != null)
            return null;

        Regex? regex = null;
        if (rule.Match.Kind == AutoModMatchKind.Regex && !string.IsNullOrWhiteSpace(rule.Match.Pattern))
        {
            regex = new Regex(rule.Match.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(75));
        }

        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var inline in rule.Match.Words)
            AddNormalized(words, inline, rule.Match.Normalization);

        foreach (var inline in rule.Match.AllowList)
            AddNormalized(allow, inline, rule.Match.Normalization);

        return new AutoModCompiledRule
        {
            Prototype = rule,
            Version = BuildRuleVersion(rule),
            Regex = regex,
            WordSet = words,
            AllowList = allow,
        };
    }

    private static void AddNormalized(HashSet<string> target, string value, AutoModNormalizationMode mode)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var normalized = AutoModNormalizer.Normalize(value, mode).Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
            target.Add(normalized);
    }

    private static string BuildRuleVersion(AutoModEditableRule rule)
    {
        var text = $"{rule.ID}|{rule.Name}|{rule.Priority}|{rule.Category}|{rule.Severity}|{rule.Match.Kind}|{rule.Match.Pattern}|{string.Join(',', rule.Match.Words)}|{string.Join(',', rule.Levels.Select(x => x.MinPoints))}";
        return ShortHash(text);
    }

    private static string BuildVersion(IEnumerable<AutoModCompiledRule> rules)
        => ShortHash(string.Join('|', rules.Select(x => $"{x.Prototype.ID}:{x.Version}")));

    private static string ShortHash(string text)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..12];
    }
}
