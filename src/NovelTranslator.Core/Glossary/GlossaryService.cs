using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NovelTranslator.Core.Models;

namespace NovelTranslator.Core.Glossary;

public class GlossaryService
{
    /// <summary>
    /// คัดกรองเฉพาะคำศัพท์ที่มีปรากฏอยู่ในข้อความ chunk (แบบ case-insensitive และดูขอบเขตคำ)
    /// </summary>
    public static List<GlossaryEntry> FindMatchingEntries(string chunkText, IEnumerable<GlossaryEntry> allEntries)
    {
        var matched = new List<GlossaryEntry>();
        if (string.IsNullOrWhiteSpace(chunkText)) return matched;

        foreach (var entry in allEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.SourceTerm)) continue;

            string term = entry.SourceTerm.Trim();
            bool useWordBoundary = IsAsciiWordChar(term[0]) && IsAsciiWordChar(term[^1]);

            if (useWordBoundary)
            {
                string pattern = @"\b" + Regex.Escape(term) + @"\b";
                if (Regex.IsMatch(chunkText, pattern, RegexOptions.IgnoreCase))
                {
                    matched.Add(entry);
                }
            }
            else if (chunkText.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                matched.Add(entry);
            }
        }

        return matched;
    }

    private static bool IsAsciiWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    /// <summary>
    /// ส่งออกรายการ Glossary เป็นไฟล์ CSV
    /// รูปแบบ: SourceTerm,TargetTerm,Category,Notes
    /// </summary>
    public static void ExportToCsv(string filePath, IEnumerable<GlossaryEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SourceTerm,TargetTerm,Category,Notes");

        foreach (var entry in entries)
        {
            string src = EscapeCsv(entry.SourceTerm);
            string tgt = EscapeCsv(entry.TargetTerm);
            string cat = EscapeCsv(entry.Category.ToString());
            string notes = EscapeCsv(entry.Notes);
            sb.AppendLine($"{src},{tgt},{cat},{notes}");
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// นำเข้าคำศัพท์จากไฟล์ CSV
    /// </summary>
    public static List<GlossaryEntry> ImportFromCsv(string filePath, long storyId)
    {
        var entries = new List<GlossaryEntry>();
        if (!File.Exists(filePath)) return entries;

        string content = File.ReadAllText(filePath, Encoding.UTF8);
        bool isHeader = true;

        foreach (var parts in ParseCsvRecords(content))
        {
            if (parts.Count == 1 && string.IsNullOrWhiteSpace(parts[0])) continue;
            if (isHeader)
            {
                isHeader = false;
                // ถ้าเป็นบรรทัดหัวข้อ ข้ามไป
                if (parts[0].Trim().Equals("SourceTerm", StringComparison.OrdinalIgnoreCase)) continue;
            }

            if (parts.Count >= 2)
            {
                string src = parts[0].Trim();
                string tgt = parts[1].Trim();
                if (string.IsNullOrEmpty(src)) continue;

                var cat = GlossaryCategory.Term;
                if (parts.Count >= 3 && Enum.TryParse<GlossaryCategory>(parts[2].Trim(), true, out var parsedCat))
                {
                    cat = parsedCat;
                }

                string notes = parts.Count >= 4 ? parts[3].Trim() : "";

                entries.Add(new GlossaryEntry
                {
                    StoryId = storyId,
                    SourceTerm = src,
                    TargetTerm = tgt,
                    Category = cat,
                    Notes = notes
                });
            }
        }

        return entries;
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static List<List<string>> ParseCsvRecords(string content)
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < content.Length && content[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // ข้าม quote ตัวที่สอง
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                row.Add(current.ToString());
                current.Clear();
            }
            else if ((c == '\r' || c == '\n') && !inQuotes)
            {
                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                {
                    i++;
                }
                row.Add(current.ToString());
                current.Clear();
                records.Add(row);
                row = new List<string>();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0 || row.Count > 0)
        {
            row.Add(current.ToString());
            records.Add(row);
        }

        return records;
    }
}
