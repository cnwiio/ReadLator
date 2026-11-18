using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NovelTranslator.Core.Export;

/// <summary>
/// ตัวช่วยตั้งชื่อตอนอัตโนมัติจากการนำเข้าไฟล์ .txt หรือ URL โดยจัดรูปแบบขึ้นต้นด้วย "ตอนที่ X" เสมอและคัดกรองชื่อเรื่อง/ชื่อเว็บออก
/// </summary>
public static partial class SmartChapterNaming
{
    private static readonly HashSet<string> KnownSiteNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "royalroad", "royal road", "webnovel", "novelbin", "freewebnovel", "lightnovelpub",
        "readnovelfull", "scribblehub", "wuxiaworld", "wattpad", "novelupdates", "boxnovel",
        "allnovelfull", "novelfull", "readlightnovel", "mtlnovel", "dek-d", "dekd",
        "readawrite", "fictionlog", "tunwalai", "kawebook", "novelsite", "testsite",
        "novelbin.me", "novelbin.com", "royalroad.com", "webnovel.com"
    };

    private static readonly HashSet<string> GenericPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "raw_dump", "raw dump", "index", "chapter", "untitled", "ตอนจากเว็บ",
        "home", "table of contents", "toc"
    };

    private static readonly Dictionary<string, int> EnglishWordNumbers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
        ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10,
        ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13, ["fourteen"] = 14, ["fifteen"] = 15,
        ["sixteen"] = 16, ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19, ["twenty"] = 20
    };

    private const string NumPattern = @"(?<num>\d+|One|Two|Three|Four|Five|Six|Seven|Eight|Nine|Ten|Eleven|Twelve|Thirteen|Fourteen|Fifteen|Sixteen|Seventeen|Eighteen|Nineteen|Twenty)";

    [GeneratedRegex(@"^(?:Chapter|Chap\.?|Ch\.?|Episode|Ep\.?|ตอนที่|บทที่)\s*0*" + NumPattern + @"\b(?:\s*[:\.\-–—]\s*|\s+)?(?<sub>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex StartChapterHeadingRegex();

    [GeneratedRegex(@"^(?<prefix>.+?)\s*(?:[:\|\-–—]\s*|\s+)(?:Chapter|Chap\.?|Ch\.?|Episode|Ep\.?|ตอนที่|บทที่)\s*0*" + NumPattern + @"\b(?:\s*[:\.\-–—]\s*|\s+)?(?<sub>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex InlineChapterHeadingRegex();

    [GeneratedRegex(@"^第\s*0*(?<num>\d+)\s*[章话節回](?:\s*[:\.\-–—]?\s*)(?<sub>.*)$")]
    private static partial Regex StartCjkChapterHeadingRegex();

    [GeneratedRegex(@"^(?<prefix>.+?)\s*(?:[:\|\-–—]\s*|\s+)第\s*0*(?<num>\d+)\s*[章话節回](?:\s*[:\.\-–—]?\s*)(?<sub>.*)$")]
    private static partial Regex InlineCjkChapterHeadingRegex();

    [GeneratedRegex(@"^(?:ch|chap|chapter|ep|episode)?[_\-\s]*0*(\d+)(?:[_\-\s\.]+(.*))?$", RegexOptions.IgnoreCase)]
    private static partial Regex FileNameNumberRegex();

    [GeneratedRegex(@"\s*(?:\||::|—|–| - )\s*")]
    private static partial Regex TitleSeparatorRegex();

    public static string ResolveTitle(
        string? rawTitleOrFileName,
        string? content,
        int fallbackOrdinal,
        string? storyTitle = null,
        string? sourceUrl = null)
    {
        var (contentNum, contentSub, contentDetectedPrefix) = TryExtractHeadingFromContent(content, storyTitle);
        string? effectiveStoryTitle = !string.IsNullOrWhiteSpace(storyTitle) ? storyTitle : contentDetectedPrefix;

        var (rawNum, rawSub) = ExtractFromRawTitle(rawTitleOrFileName, effectiveStoryTitle);

        int? urlNum = !string.IsNullOrWhiteSpace(sourceUrl)
            ? WebChapterImportService.ExtractUrlChapterNumber(sourceUrl)
            : null;

        int resolvedNum = contentNum
            ?? rawNum
            ?? urlNum
            ?? (fallbackOrdinal > 0 ? fallbackOrdinal : 1);

        string resolvedSub = !string.IsNullOrWhiteSpace(contentSub)
            ? contentSub
            : (contentNum == null || rawNum == null || contentNum == rawNum ? rawSub : string.Empty);

        resolvedSub = CleanSubtitle(resolvedSub, effectiveStoryTitle);

        return string.IsNullOrWhiteSpace(resolvedSub)
            ? $"ตอนที่ {resolvedNum}"
            : $"ตอนที่ {resolvedNum}: {resolvedSub}";
    }

    private static (int? Num, string Sub, string? DetectedPrefix) TryExtractHeadingFromContent(string? content, string? storyTitle)
    {
        if (string.IsNullOrWhiteSpace(content)) return (null, string.Empty, null);

        var lines = content.Replace("\r\n", "\n").Split('\n');
        foreach (string rawLine in lines.Take(5))
        {
            string line = rawLine.Trim().Trim('#', '*', ' ', '\t', '【', '】', '[', ']');
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Length > 120) break;

            if (IsMatchingStoryOrSiteTitle(line, storyTitle))
            {
                continue;
            }

            if (TryExtractChapterPattern(line, storyTitle, out int num, out string sub, out string? prefix))
            {
                return (num, sub, prefix);
            }

            break;
        }

        return (null, string.Empty, null);
    }

    private static (int? Num, string Sub) ExtractFromRawTitle(string? raw, string? storyTitle)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, string.Empty);

        string text = raw.Trim();
        var segments = TitleSeparatorRegex().Split(text)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        if (segments.Length > 1)
        {
            for (int i = 0; i < segments.Length; i++)
            {
                if (IsMatchingStoryOrSiteTitle(segments[i], storyTitle)) continue;

                if (TryExtractChapterPattern(segments[i], storyTitle, out int segNum, out string segSub, out string? prefix))
                {
                    string effectiveStory = !string.IsNullOrWhiteSpace(storyTitle) ? storyTitle : prefix ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(segSub) && i + 1 < segments.Length)
                    {
                        string next = segments[i + 1];
                        bool looksLikePrefixStory = i > 0 && NormalizeCompareKey(next) == NormalizeCompareKey(segments[0]);
                        if (!looksLikePrefixStory && !IsMatchingStoryOrSiteTitle(next, effectiveStory))
                        {
                            segSub = next;
                        }
                    }

                    return (segNum, CleanSubtitle(segSub, effectiveStory));
                }
            }

            // ไม่มีรูปแบบ "Chapter X" ใน segment ใดเลย — ตัด segment ที่เป็นชื่อเรื่องหรือชื่อเว็บออก
            var nonStorySegments = segments
                .Where(s => !IsMatchingStoryOrSiteTitle(s, storyTitle))
                .ToArray();

            if (nonStorySegments.Length == 0)
            {
                return (null, string.Empty);
            }

            text = nonStorySegments[0];
        }

        if (IsMatchingStoryOrSiteTitle(text, storyTitle))
        {
            return (null, string.Empty);
        }

        if (TryExtractChapterPattern(text, storyTitle, out int directNum, out string directSub, out string? directPrefix))
        {
            string effectiveStory = !string.IsNullOrWhiteSpace(storyTitle) ? storyTitle : directPrefix ?? string.Empty;
            return (directNum, CleanSubtitle(directSub, effectiveStory));
        }

        // กรณีชื่อไฟล์ เช่น "003_The_Trial", "ch05-awake", "Shadow_Slave_003_The_Trial" หรือ "005"
        string cleanedFile = Regex.Replace(text.Replace('_', ' '), @"\s{2,}", " ").Trim();
        cleanedFile = StripStoryTitleFromText(cleanedFile, storyTitle);
        if (string.IsNullOrWhiteSpace(cleanedFile))
        {
            return (null, string.Empty);
        }

        var fileMatch = FileNameNumberRegex().Match(cleanedFile);
        if (fileMatch.Success && int.TryParse(fileMatch.Groups[1].Value, out int fileNum))
        {
            string rest = fileMatch.Groups[2].Value.Replace('-', ' ').Trim();
            return (fileNum, CleanSubtitle(rest, storyTitle));
        }

        return (null, CleanSubtitle(cleanedFile, storyTitle));
    }

    private static bool TryExtractChapterPattern(
        string input,
        string? storyTitle,
        out int num,
        out string sub,
        out string? detectedPrefix)
    {
        num = 0;
        sub = string.Empty;
        detectedPrefix = null;

        Match m = StartChapterHeadingRegex().Match(input);
        if (!m.Success)
        {
            m = InlineChapterHeadingRegex().Match(input);
        }
        if (!m.Success)
        {
            m = StartCjkChapterHeadingRegex().Match(input);
        }
        if (!m.Success)
        {
            m = InlineCjkChapterHeadingRegex().Match(input);
        }

        if (!m.Success || !TryParseChapterNumber(m.Groups["num"].Value, out num))
        {
            return false;
        }

        string rawPrefix = m.Groups["prefix"].Success ? m.Groups["prefix"].Value.Trim() : string.Empty;
        if (!string.IsNullOrWhiteSpace(rawPrefix))
        {
            detectedPrefix = rawPrefix;
        }

        string? effectiveStory = !string.IsNullOrWhiteSpace(storyTitle) ? storyTitle : detectedPrefix;
        sub = CleanSubtitle(m.Groups["sub"].Value, effectiveStory);
        return true;
    }

    private static bool TryParseChapterNumber(string token, out int num)
    {
        if (int.TryParse(token, out num))
        {
            return num >= 0;
        }
        return EnglishWordNumbers.TryGetValue(token.Trim(), out num);
    }

    internal static bool IsMatchingStoryOrSiteTitle(string? candidate, string? storyTitle)
    {
        string normCandidate = NormalizeCompareKey(candidate);
        if (string.IsNullOrEmpty(normCandidate)) return true;

        if (GenericPlaceholders.Contains(normCandidate) || KnownSiteNames.Contains(normCandidate))
        {
            return true;
        }

        string withoutTld = Regex.Replace(normCandidate, @"\.(?:com|net|org|me|io|co|th)$", string.Empty);
        if (KnownSiteNames.Contains(withoutTld))
        {
            return true;
        }

        string normStory = NormalizeCompareKey(storyTitle);
        return !string.IsNullOrEmpty(normStory) && string.Equals(normCandidate, normStory, StringComparison.OrdinalIgnoreCase);
    }

    internal static string StripStoryTitleFromText(string text, string? storyTitle)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string cleaned = text.Trim().Trim(':', '-', '–', '—', '|', '/', ' ');
        if (IsMatchingStoryOrSiteTitle(cleaned, storyTitle))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(storyTitle) || storyTitle.Trim().Length < 2)
        {
            return cleaned;
        }

        string escaped = Regex.Escape(storyTitle.Trim());
        // ตัดชื่อเรื่องที่อยู่หน้าสุดหรือท้ายสุดคั่นด้วยตัวแบ่งหรือวงเล็บ
        cleaned = Regex.Replace(cleaned, $@"^(?:\[|\()?\s*{escaped}\s*(?:\]|\))?(?:\s*[:\|\-–—/]\s*|\s+)", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, $@"(?:\s*[:\|\-–—/]\s*|\s+)(?:\[|\()?\s*{escaped}\s*(?:\]|\))?$", string.Empty, RegexOptions.IgnoreCase);
        cleaned = cleaned.Trim().Trim(':', '-', '–', '—', '|', '/', ' ');

        return IsMatchingStoryOrSiteTitle(cleaned, storyTitle) ? string.Empty : cleaned;
    }

    private static string CleanSubtitle(string? sub, string? storyTitle)
    {
        if (string.IsNullOrWhiteSpace(sub)) return string.Empty;
        string normalized = Regex.Replace(sub.Trim(), @"\s{2,}", " ");
        return StripStoryTitleFromText(normalized, storyTitle);
    }

    private static string NormalizeCompareKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string trimmed = value.Trim().Trim('[', ']', '(', ')', '{', '}', '"', '\'', '【', '】', '《', '》', ':', '-', '–', '—', '|', '/');
        return Regex.Replace(trimmed, @"\s+", " ").Trim().ToLowerInvariant();
    }
}
