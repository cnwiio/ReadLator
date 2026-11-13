using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NovelTranslator.Core.Export;

/// <summary>
/// ตัวช่วยตั้งชื่อตอนอัตโนมัติจากการนำเข้าไฟล์ .txt หรือ URL
/// </summary>
public static partial class SmartChapterNaming
{
    [GeneratedRegex(@"^(?:Chapter|Ch\.?|Episode|Ep\.?|ตอนที่|บทที่)\s*(\d+)(?:\s*[:\.\-–—]\s*|\s+)?(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ChapterHeadingRegex();

    [GeneratedRegex(@"^第\s*(\d+)\s*[章话節](?:\s*[:\.\-–—]?\s*)(.*)$")]
    private static partial Regex CjkChapterHeadingRegex();

    [GeneratedRegex(@"^(?:ch|chap|chapter|ep|episode)?[_\-\s]*0*(\d+)(?:[_\-\s\.]+(.*))?$", RegexOptions.IgnoreCase)]
    private static partial Regex FileNameNumberRegex();

    [GeneratedRegex(@"\s*(?:\||::|—|–| - )\s*")]
    private static partial Regex TitleSeparatorRegex();

    public static string ResolveTitle(string? rawTitleOrFileName, string? content, int fallbackOrdinal)
    {
        string? fromContent = TryExtractHeadingFromContent(content);
        if (!string.IsNullOrWhiteSpace(fromContent))
        {
            return fromContent;
        }

        string? fromRaw = CleanAndNormalizeRawTitle(rawTitleOrFileName);
        if (!string.IsNullOrWhiteSpace(fromRaw))
        {
            return fromRaw;
        }

        return fallbackOrdinal > 0 ? $"ตอนที่ {fallbackOrdinal}" : "ตอนจากเว็บ";
    }

    private static string? TryExtractHeadingFromContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var lines = content.Replace("\r\n", "\n").Split('\n');
        foreach (string rawLine in lines.Take(5))
        {
            string line = rawLine.Trim().Trim('#', '*', ' ', '\t', '【', '】', '[', ']');
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Length > 120) break;

            string? normalized = TryNormalizeChapterPattern(line);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
            break;
        }

        return null;
    }

    private static string? CleanAndNormalizeRawTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        string text = raw.Trim();
        var segments = TitleSeparatorRegex().Split(text)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        if (segments.Length > 1)
        {
            // เลือกส่วนที่มีรูปแบบเลขตอนก่อน (เช่น "Novel Name - Chapter 12: Rain | RoyalRoad" -> "Chapter 12: Rain")
            string? chapterSeg = segments.FirstOrDefault(s => TryNormalizeChapterPattern(s) != null);
            if (chapterSeg != null)
            {
                return TryNormalizeChapterPattern(chapterSeg)!;
            }
            text = segments[0];
        }

        string? direct = TryNormalizeChapterPattern(text);
        if (direct != null) return direct;

        // กรณีชื่อไฟล์ เช่น "003_The_Trial" หรือ "ch05-awake" หรือ "003"
        string cleanedFile = text.Replace('_', ' ').Trim();
        var fileMatch = FileNameNumberRegex().Match(cleanedFile);
        if (fileMatch.Success && int.TryParse(fileMatch.Groups[1].Value, out int num))
        {
            string rest = fileMatch.Groups[2].Value.Replace('-', ' ').Trim();
            return string.IsNullOrWhiteSpace(rest)
                ? $"ตอนที่ {num}"
                : $"ตอนที่ {num}: {rest}";
        }

        return Regex.Replace(cleanedFile, @"\s{2,}", " ").Trim();
    }

    private static string? TryNormalizeChapterPattern(string input)
    {
        var m = ChapterHeadingRegex().Match(input);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int num))
        {
            string sub = m.Groups[2].Value.Trim();
            return string.IsNullOrWhiteSpace(sub)
                ? $"ตอนที่ {num}"
                : $"ตอนที่ {num}: {sub}";
        }

        var cjk = CjkChapterHeadingRegex().Match(input);
        if (cjk.Success && int.TryParse(cjk.Groups[1].Value, out int cjkNum))
        {
            string sub = cjk.Groups[2].Value.Trim();
            return string.IsNullOrWhiteSpace(sub)
                ? $"ตอนที่ {cjkNum}"
                : $"ตอนที่ {cjkNum}: {sub}";
        }

        return null;
    }
}
