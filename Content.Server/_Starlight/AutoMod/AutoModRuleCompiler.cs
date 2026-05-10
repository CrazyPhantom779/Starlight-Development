using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Content.Shared._Starlight.AutoMod;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.AutoMod;

internal sealed class AutoModRuleCompiler
{
    private readonly IPrototypeManager _prototypes;
    public string RulesetVersion { get; private set; } = "local-empty";
    public List<AutoModCompiledRule> Rules { get; private set; } = new();

    public AutoModRuleCompiler(IPrototypeManager prototypes)
    {
        _prototypes = prototypes;
    }

    public void Reload()
    {
        var wordSets = _prototypes.EnumeratePrototypes<AutoModWordSetPrototype>()
            .ToDictionary(x => x.ID, x => x, StringComparer.OrdinalIgnoreCase);

        var compiled = new List<AutoModCompiledRule>();
        foreach (var rule in _prototypes.EnumeratePrototypes<AutoModRulePrototype>().Where(x => x.Enabled))
        {
            Regex? regex = null;
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (rule.Match.Kind == AutoModMatchKind.Regex && !string.IsNullOrWhiteSpace(rule.Match.Pattern))
            {
                if (rule.Match.Pattern.Length > 512)
                    continue;

                regex = new Regex(rule.Match.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));
            }

            if (rule.Match.Kind == AutoModMatchKind.WordSet && rule.Match.WordSet is { } setId && wordSets.TryGetValue(setId, out var set))
            {
                foreach (var word in set.Words)
                    words.Add(AutoModNormalizer.Normalize(word, rule.Match.Normalization));
                foreach (var word in set.AllowList)
                    allow.Add(AutoModNormalizer.Normalize(word, rule.Match.Normalization));
            }

            compiled.Add(new AutoModCompiledRule
            {
                Prototype = rule,
                Version = Hash(rule.ID + rule.Name + rule.Priority + rule.Category + rule.Severity + rule.Match.Kind + rule.Match.Pattern + string.Join(',', words)),
                Regex = regex,
                WordSet = words,
                AllowList = allow,
            });
        }

        Rules = compiled.OrderByDescending(x => x.Prototype.Priority).ToList();
        RulesetVersion = Hash(string.Join('|', Rules.Select(x => x.Prototype.ID + ':' + x.Version)));
    }

    private static string Hash(string text)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..12];
    }
}
