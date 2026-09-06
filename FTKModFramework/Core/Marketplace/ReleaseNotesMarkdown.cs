using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FTKModFramework.Core.Marketplace
{
    internal sealed class ReleaseNoteBlock
    {
        internal string Text;
        internal int Heading;
        internal bool Code;
        internal readonly List<ReleaseNoteLink> Links = new List<ReleaseNoteLink>();
    }
    internal sealed class ReleaseNoteLink
    {
        internal string Label;
        internal string Url;
    }

    // A deliberately small Markdown reader. Only tags generated here reach Unity rich text.
    // Tables become labeled records so long archive names remain readable in the narrow pane.
    internal static class ReleaseNotesMarkdown
    {
        internal static List<ReleaseNoteBlock> Parse(string source)
        {
            string[] lines = (source ?? "").Replace("\r", "").Split('\n');
            List<ReleaseNoteBlock> blocks = new List<ReleaseNoteBlock>();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                ReleaseNoteBlock block = new ReleaseNoteBlock();
                if (line.StartsWith("```") || line.StartsWith("~~~"))
                {
                    string fence = line.Substring(0, 3);
                    StringBuilder code = new StringBuilder();
                    while (++i < lines.Length && !lines[i].TrimStart().StartsWith(fence)) code.Append(lines[i]).Append('\n');
                    block.Code = true;
                    block.Text = Escape(code.ToString().TrimEnd('\n'));
                }
                else if (i + 1 < lines.Length && line.IndexOf('|') >= 0 && TableRule(lines[i + 1]))
                {
                    string[] headers = Cells(line);
                    i++;
                    while (i + 1 < lines.Length && lines[i + 1].IndexOf('|') >= 0 && lines[i + 1].Trim().Length > 0)
                    {
                        string[] cells = Cells(lines[++i]);
                        ReleaseNoteBlock row = new ReleaseNoteBlock();
                        StringBuilder record = new StringBuilder();
                        for (int c = 0; c < cells.Length; c++)
                        {
                            if (c > 0) record.Append('\n');
                            if (c < headers.Length) record.Append("<b>").Append(Inline(headers[c], row, 0)).Append(":</b> ");
                            record.Append(Inline(cells[c], row, 0));
                        }
                        row.Text = record.ToString();
                        blocks.Add(row);
                    }
                    continue;
                }
                else
                {
                    int heading = 0;
                    while (heading < line.Length && heading < 6 && line[heading] == '#') heading++;
                    if (heading > 0 && heading < line.Length && line[heading] == ' ')
                    {
                        block.Heading = heading;
                        line = line.Substring(heading).Trim().TrimEnd('#').TrimEnd();
                    }
                    else
                    {
                        StringBuilder paragraph = new StringBuilder(line);
                        while (i + 1 < lines.Length && lines[i + 1].Trim().Length > 0 && !BlockStart(lines[i + 1]) && !(i + 2 < lines.Length && TableRule(lines[i + 2])))
                            paragraph.Append(' ').Append(lines[++i].Trim());
                        line = paragraph.ToString();
                        if (Regex.IsMatch(line, @"^[-*+]\s")) line = "• " + line.Substring(2).TrimStart();
                        if (line.StartsWith("> ")) line = "│ " + line.Substring(2);
                        if (Regex.IsMatch(line, @"^([-*_])\1\1+$")) line = "────────────────";
                    }
                    block.Text = Inline(line, block, 0);
                }
                blocks.Add(block);
            }
            if (blocks.Count == 0) blocks.Add(new ReleaseNoteBlock { Text = "No release notes were provided." });
            return blocks;
        }
        private static bool BlockStart(string line)
        {
            return Regex.IsMatch(line.TrimStart(), @"^(#{1,6}\s|[-*+]\s|\d+[.)]\s|>|```|~~~|\|)");
        }
        private static string[] Cells(string line)
        {
            string[] cells = line.Trim().Trim('|').Split('|');
            for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
            return cells;
        }
        private static bool TableRule(string line)
        {
            string[] cells = Cells(line);
            if (cells.Length < 2) return false;
            foreach (string cell in cells) if (!Regex.IsMatch(cell.Trim(), @"^:?-{3,}:?$")) return false;
            return true;
        }
        internal static bool SafeUrl(string value)
        {
            Uri uri;
            return !string.IsNullOrEmpty(value) && value.IndexOf('\\') < 0 && !Regex.IsMatch(value, @"\s") && Uri.TryCreate(value, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && uri.Host.Length > 0 && uri.UserInfo.Length == 0;
        }
        internal static string Escape(string value) { return value.Replace("<", "‹").Replace(">", "›"); }
        private static string Inline(string text, ReleaseNoteBlock block, int depth)
        {
            if (depth > 6) return Escape(text);
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\\' && i + 1 < text.Length) { result.Append(Escape(text[++i].ToString())); continue; }
                if (ch == '`')
                {
                    int end = text.IndexOf('`', i + 1);
                    if (end > i + 1) { result.Append("<color=#665022>").Append(Escape(text.Substring(i + 1, end - i - 1))).Append("</color>"); i = end; continue; }
                }
                if (ch == '[' || (ch == '!' && i + 1 < text.Length && text[i + 1] == '['))
                {
                    bool image = ch == '!';
                    int start = i + (image ? 2 : 1);
                    int labelEnd = text.IndexOf("](", start, StringComparison.Ordinal);
                    int end = labelEnd < 0 ? -1 : text.IndexOf(')', labelEnd + 2);
                    if (end >= 0)
                    {
                        string label = text.Substring(start, labelEnd - start);
                        string url = text.Substring(labelEnd + 2, end - labelEnd - 2).Trim();
                        result.Append(Inline(label, block, depth + 1));
                        if (SafeUrl(url))
                        {
                            block.Links.Add(new ReleaseNoteLink { Label = label, Url = url });
                            result.Append(" <color=#7A5212>[" + block.Links.Count + "]</color>");
                        }
                        i = end; continue;
                    }
                }
                if (ch == '*' || ch == '_')
                {
                    int count = i + 1 < text.Length && text[i + 1] == ch ? 2 : 1;
                    string marker = new string(ch, count);
                    int end = text.IndexOf(marker, i + count, StringComparison.Ordinal);
                    bool withinWord = ch == '_' && i > 0 && char.IsLetterOrDigit(text[i - 1]);
                    if (!withinWord && end > i + count)
                    {
                        string tag = count == 2 ? "b" : "i";
                        result.Append("<" + tag + ">").Append(Inline(text.Substring(i + count, end - i - count), block, depth + 1)).Append("</" + tag + ">");
                        i = end + count - 1; continue;
                    }
                }
                result.Append(Escape(ch.ToString()));
            }
            return result.ToString();
        }
    }
}
