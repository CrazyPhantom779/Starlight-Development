using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared._Starlight.AutoMod;

namespace Content.Server._Starlight.AutoMod;

internal sealed class AutoModEngine
{
    public AutoModMatch? Evaluate(string raw, string channel, IReadOnlyList<AutoModCompiledRule> rules)
    {
        foreach (var rule in rules)
        {
            if (rule.Prototype.Channels.Count > 0 && !rule.Prototype.Channels.Any(x => string.Equals(x, channel, StringComparison.OrdinalIgnoreCase)))
                continue;

            var normalized = AutoModNormalizer.Normalize(raw, rule.Prototype.Match.Normalization);
            switch (rule.Prototype.Match.Kind)
            {
                case AutoModMatchKind.WordSet:
                    var match = MatchWordSet(normalized, rule);
                    if (match != null)
                        return match;
                    break;
                case AutoModMatchKind.Regex:
                    if (rule.Regex is not null)
                    {
                        try
                        {
                            var m = rule.Regex.Match(normalized);
                            if (m.Success)
                                return new AutoModMatch(rule, m.Value, m.Index, m.Length);
                        }
                        catch (RegexMatchTimeoutException)
                        {
                            continue;
                        }
                    }
                    break;
            }
        }

        return null;
    }

    private static AutoModMatch? MatchWordSet(string normalized, AutoModCompiledRule rule)
    {
        foreach (var allow in rule.AllowList)
        {
            if (!string.IsNullOrWhiteSpace(allow) && normalized.Contains(allow, StringComparison.OrdinalIgnoreCase))
                return null;
        }

        foreach (var word in rule.WordSet)
        {
            if (string.IsNullOrWhiteSpace(word))
                continue;

            var index = normalized.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                if (!rule.Prototype.Match.RequireWordBoundary || HasBoundaries(normalized, index, word.Length))
                    return new AutoModMatch(rule, word, index, word.Length);

                index = normalized.IndexOf(word, index + 1, StringComparison.OrdinalIgnoreCase);
            }
        }

        return null;
    }

    private static bool HasBoundaries(string text, int index, int length)
    {
        var before = index <= 0 || !char.IsLetterOrDigit(text[index - 1]);
        var afterIndex = index + length;
        var after = afterIndex >= text.Length || !char.IsLetterOrDigit(text[afterIndex]);
        return before && after;
    }
}
