using System.Globalization;
using System.Text;
using Content.Shared._Starlight.AutoMod;

namespace Content.Server._Starlight.AutoMod;

internal static class AutoModNormalizer
{
    public static string Normalize(string input, AutoModNormalizationMode mode)
    {
        if (mode == AutoModNormalizationMode.None)
            return input;

        var text = input.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
        text = CollapseWhitespace(text);

        if (mode < AutoModNormalizationMode.Aggressive)
            return text;

        text = RemoveZeroWidth(text);
        text = CollapseRepeated(text, 3);
        text = StripSeparators(text);

        if (mode < AutoModNormalizationMode.RaidResistant)
            return text;

        text = ApplyLeet(text);
        text = CollapseRepeated(text, 2);
        return text;
    }

    private static string CollapseWhitespace(string input)
    {
        var sb = new StringBuilder(input.Length);
        var lastSpace = false;
        foreach (var c in input)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastSpace)
                    sb.Append(' ');
                lastSpace = true;
                continue;
            }

            lastSpace = false;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string RemoveZeroWidth(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat is UnicodeCategory.Format or UnicodeCategory.Control)
                continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string CollapseRepeated(string input, int max)
    {
        if (input.Length == 0)
            return input;

        var sb = new StringBuilder(input.Length);
        var last = '\0';
        var count = 0;
        foreach (var c in input)
        {
            if (c == last)
                count++;
            else
            {
                last = c;
                count = 1;
            }

            if (count <= max)
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static string StripSeparators(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c is '.' or '-' or '_' or '*' or '~' or '`' or '\'' or '"')
                continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string ApplyLeet(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            sb.Append(c switch
            {
                '0' => 'o',
                '1' => 'i',
                '3' => 'e',
                '4' => 'a',
                '5' => 's',
                '7' => 't',
                '@' => 'a',
                '$' => 's',
                _ => c,
            });
        }
        return sb.ToString();
    }
}
