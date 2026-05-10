using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Content.Shared._Starlight.AutoMod;

namespace Content.Client._Starlight.AutoMod.UI;

/// <summary>
/// Client-only lightweight formatter for the rule editor. The server remains authoritative for parsing and saving rule text.
/// </summary>
public static class AutoModRuleJsonFormatter
{
    public static string ToJson(AutoModEditableRule rule)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        Prop(sb, 1, "ID", rule.ID, comma: true);
        Prop(sb, 1, "Name", rule.Name, comma: true);
        Prop(sb, 1, "Description", rule.Description ?? string.Empty, comma: true);
        Prop(sb, 1, "Enabled", rule.Enabled, comma: true);
        Prop(sb, 1, "Priority", rule.Priority, comma: true);
        Prop(sb, 1, "Category", rule.Category, comma: true);
        Prop(sb, 1, "Severity", rule.Severity.ToString(), comma: true);

        ArrayProp(sb, 1, "Channels", rule.Channels, comma: true);

        sb.AppendLine(Indent(1) + "\"AdminPolicy\": {");
        Prop(sb, 2, "AdminedUsers", rule.AdminPolicy.AdminedUsers.ToString(), comma: true);
        Prop(sb, 2, "DeadminnedUsers", rule.AdminPolicy.DeadminnedUsers.ToString(), comma: true);
        Prop(sb, 2, "IgnoreAdminChannels", rule.AdminPolicy.IgnoreAdminChannels, comma: false);
        sb.AppendLine(Indent(1) + "},");

        sb.AppendLine(Indent(1) + "\"Match\": {");
        Prop(sb, 2, "Kind", rule.Match.Kind.ToString(), comma: true);
        NullableProp(sb, 2, "WordSet", null, comma: true);
        NullableProp(sb, 2, "Pattern", rule.Match.Pattern, comma: true);
        ArrayProp(sb, 2, "Words", rule.Match.Words, comma: true);
        ArrayProp(sb, 2, "AllowList", rule.Match.AllowList, comma: true);
        Prop(sb, 2, "Normalization", rule.Match.Normalization.ToString(), comma: true);
        Prop(sb, 2, "RequireWordBoundary", rule.Match.RequireWordBoundary, comma: true);
        Prop(sb, 2, "Window", rule.Match.Window.ToString(), comma: true);
        Prop(sb, 2, "MaxMessages", rule.Match.MaxMessages, comma: true);
        Prop(sb, 2, "SimilarityThreshold", rule.Match.SimilarityThreshold.ToString(CultureInfo.InvariantCulture), raw: true, comma: false);
        sb.AppendLine(Indent(1) + "},");

        sb.AppendLine(Indent(1) + "\"Evidence\": {");
        Prop(sb, 2, "Mode", rule.Evidence.Mode.ToString(), comma: true);
        Prop(sb, 2, "MaxPreviewLength", rule.Evidence.MaxPreviewLength, comma: true);
        Prop(sb, 2, "ShowMatchedTokenToAdmins", rule.Evidence.ShowMatchedTokenToAdmins, comma: false);
        sb.AppendLine(Indent(1) + "},");

        sb.AppendLine(Indent(1) + "\"Escalation\": {");
        Prop(sb, 2, "Scope", rule.Escalation.Scope.ToString(), comma: true);
        NullableProp(sb, 2, "ScopeKey", rule.Escalation.ScopeKey, comma: true);
        Prop(sb, 2, "PointsPerIncident", rule.Escalation.PointsPerIncident, comma: true);
        Prop(sb, 2, "Decay", rule.Escalation.Decay.ToString(), comma: true);
        Prop(sb, 2, "IncludeFalsePositives", rule.Escalation.IncludeFalsePositives, comma: true);
        Prop(sb, 2, "IncludeDecayed", rule.Escalation.IncludeDecayed, comma: false);
        sb.AppendLine(Indent(1) + "},");

        sb.AppendLine(Indent(1) + "\"Discord\": {");
        Prop(sb, 2, "LogMode", rule.Discord.LogMode.ToString(), comma: true);
        Prop(sb, 2, "MinimumAction", rule.Discord.MinimumAction.ToString(), comma: true);
        ArrayProp(sb, 2, "PingRolesOn", rule.Discord.PingRolesOn.Select(x => x.ToString()), comma: false);
        sb.AppendLine(Indent(1) + "},");

        sb.AppendLine(Indent(1) + "\"Levels\": [");
        for (var i = 0; i < rule.Levels.Count; i++)
        {
            var level = rule.Levels[i];
            sb.AppendLine(Indent(2) + "{");
            Prop(sb, 3, "MinPoints", level.MinPoints, comma: true);
            Prop(sb, 3, "CancelSpeech", level.CancelSpeech, comma: true);
            Prop(sb, 3, "NotifyAdmins", level.NotifyAdmins, comma: true);
            Prop(sb, 3, "ActionMode", level.ActionMode.ToString(), comma: true);

            sb.AppendLine(Indent(3) + "\"Approval\": {");
            Prop(sb, 4, "Timeout", level.Approval.Timeout.ToString(), comma: true);
            Prop(sb, 4, "RequiredPermission", level.Approval.RequiredPermission, comma: true);
            Prop(sb, 4, "AllowSpeechWhilePending", level.Approval.AllowSpeechWhilePending, comma: false);
            sb.AppendLine(Indent(3) + "},");

            sb.AppendLine(Indent(3) + "\"Actions\": [");
            for (var actionIndex = 0; actionIndex < level.Actions.Count; actionIndex++)
            {
                var action = level.Actions[actionIndex];
                sb.AppendLine(Indent(4) + "{");
                Prop(sb, 5, "Type", action.Type.ToString(), comma: true);
                NullableProp(sb, 5, "Message", action.Message, comma: true);
                NullableProp(sb, 5, "Reason", action.Reason, comma: true);
                Prop(sb, 5, "Duration", action.Duration.ToString(), comma: true);
                Prop(sb, 5, "Severity", action.Severity.ToString(), comma: true);
                Prop(sb, 5, "Expiry", action.Expiry.ToString(), comma: true);
                Prop(sb, 5, "Appealable", action.Appealable, comma: false);
                sb.AppendLine(Indent(4) + "}" + (actionIndex == level.Actions.Count - 1 ? string.Empty : ","));
            }

            sb.AppendLine(Indent(3) + "]");
            sb.AppendLine(Indent(2) + "}" + (i == rule.Levels.Count - 1 ? string.Empty : ","));
        }

        sb.AppendLine(Indent(1) + "],");
        Prop(sb, 1, "Source", "RuntimeUI", comma: false);
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void Prop(StringBuilder sb, int indent, string name, string value, bool comma, bool raw = false)
        => sb.AppendLine(Indent(indent) + $"\"{Escape(name)}\": " + (raw ? value : $"\"{Escape(value)}\"") + (comma ? "," : string.Empty));

    private static void Prop(StringBuilder sb, int indent, string name, bool value, bool comma)
        => sb.AppendLine(Indent(indent) + $"\"{Escape(name)}\": " + (value ? "true" : "false") + (comma ? "," : string.Empty));

    private static void Prop(StringBuilder sb, int indent, string name, int value, bool comma)
        => sb.AppendLine(Indent(indent) + $"\"{Escape(name)}\": {value}" + (comma ? "," : string.Empty));

    private static void NullableProp(StringBuilder sb, int indent, string name, string? value, bool comma)
        => sb.AppendLine(Indent(indent) + $"\"{Escape(name)}\": " + (value == null ? "null" : $"\"{Escape(value)}\"") + (comma ? "," : string.Empty));

    private static void ArrayProp(StringBuilder sb, int indent, string name, IEnumerable<string> values, bool comma)
    {
        sb.AppendLine(Indent(indent) + $"\"{Escape(name)}\": [");

        var list = values.ToList();
        for (var i = 0; i < list.Count; i++)
            sb.AppendLine(Indent(indent + 1) + $"\"{Escape(list[i])}\"" + (i == list.Count - 1 ? string.Empty : ","));

        sb.AppendLine(Indent(indent) + "]" + (comma ? "," : string.Empty));
    }

    private static string Indent(int count) => new(' ', count * 2);

    private static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length + 8);

        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (char.IsControl(c))
                        sb.Append("\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }
}
