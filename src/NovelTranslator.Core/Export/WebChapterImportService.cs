using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using SmartReader;

namespace NovelTranslator.Core.Export;

public enum WebImportRangeMode
{
    ByCount,
    ByRange
}

public sealed record WebMultiImportOptions(
    string Url,
    WebImportRangeMode Mode = WebImportRangeMode.ByCount,
    int Count = 1,
    int StartChapter = 1,
    int EndChapter = 10)
{
    public int EffectiveCount => Mode == WebImportRangeMode.ByRange
        ? Math.Clamp(EndChapter - StartChapter + 1, 1, 200)
        : Math.Clamp(Count, 1, 200);
}

public sealed record WebChapterResult(string Title, string Content, string? NextChapterUrl = null);

public static class WebChapterImportService
{
    internal const int MaxResponseBytes = 5 * 1024 * 1024;
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("th,en-US;q=0.9,en;q=0.8");
        return client;
    }

    public static Task<WebChapterResult> FetchReadableChapterAsync(string url, CancellationToken cancellationToken = default)
        => FetchReadableChapterAsync(url, HttpClient, cancellationToken);

    internal static async Task<WebChapterResult> FetchReadableChapterAsync(string url, HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var (finalUrl, html) = await FetchRawHtmlAsync(url, httpClient, cancellationToken);
        return ExtractFromHtml(finalUrl, html);
    }

    public static Task<int> FetchChaptersBatchAsync(
        WebMultiImportOptions options,
        Func<WebChapterResult, int, int, Task> onChapterFetched,
        CancellationToken cancellationToken = default)
        => FetchChaptersBatchAsync(options, onChapterFetched, HttpClient, 900, cancellationToken);

    internal static async Task<int> FetchChaptersBatchAsync(
        WebMultiImportOptions options,
        Func<WebChapterResult, int, int, Task> onChapterFetched,
        HttpClient? customClient = null,
        int delayMs = 900,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(onChapterFetched);

        HttpClient client = customClient ?? HttpClient;
        string rawUrl = options.Url?.Trim() ?? string.Empty;
        int totalTarget = options.EffectiveCount;
        int startChapter = Math.Max(1, Math.Min(options.StartChapter, options.EndChapter));
        int endChapter = Math.Max(startChapter, options.EndChapter);
        if (options.Mode == WebImportRangeMode.ByRange)
        {
            totalTarget = Math.Clamp(endChapter - startChapter + 1, 1, 200);
        }

        int imported = 0;

        // กรณี URL เป็นแม่แบบที่มี {n} หรือ {0}
        if (IsTemplateUrl(rawUrl))
        {
            int firstNum = options.Mode == WebImportRangeMode.ByRange ? startChapter : 1;
            for (int i = 0; i < totalTarget; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? targetUrl = TryReplaceUrlChapterNumber(rawUrl, firstNum + i);
                if (string.IsNullOrWhiteSpace(targetUrl)) break;

                if (i > 0 && delayMs > 0)
                {
                    await Task.Delay(delayMs, cancellationToken);
                }

                var chapter = await FetchReadableChapterAsync(targetUrl, client, cancellationToken);
                imported++;
                await onChapterFetched(chapter, imported, totalTarget);
            }
            return imported;
        }

        var (initialUrl, initialHtml) = await FetchRawHtmlAsync(rawUrl, client, cancellationToken);
        bool isTocPage = TryAnalyzePageForBatchImport(initialUrl, initialHtml, options.Mode, out var tocLinks, out var firstArticle);

        if (isTocPage)
        {
            var selected = SelectTocLinksForImport(tocLinks, options.Mode, totalTarget, startChapter, endChapter);
            if (selected.Count == 0)
            {
                throw new InvalidOperationException("ไม่พบตอนที่ตรงกับช่วงที่ระบุในหน้าสารบัญนี้");
            }

            int total = selected.Count;
            for (int i = 0; i < selected.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (i > 0 && delayMs > 0)
                {
                    await Task.Delay(delayMs, cancellationToken);
                }

                var chapter = await FetchReadableChapterAsync(selected[i].Url, client, cancellationToken);
                imported++;
                await onChapterFetched(chapter, imported, total);
            }

            return imported;
        }

        // หน้าเนื้อหาตอนเดียว -> ไล่ตามลิงก์ถัดไปหรือเลขตอนใน URL
        string? startPatternUrl = null;
        bool useRangeUrlPattern = false;
        if (options.Mode == WebImportRangeMode.ByRange)
        {
            int? detectedNum = ExtractUrlChapterNumber(initialUrl);
            if (detectedNum.HasValue && detectedNum.Value != startChapter)
            {
                startPatternUrl = TryReplaceUrlChapterNumber(initialUrl, startChapter);
                useRangeUrlPattern = !string.IsNullOrWhiteSpace(startPatternUrl);
            }
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? currentUrl = useRangeUrlPattern ? startPatternUrl : initialUrl;
        WebChapterResult? currentChapter = useRangeUrlPattern ? null : firstArticle;

        for (int i = 0; i < totalTarget; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (currentChapter == null)
            {
                if (string.IsNullOrWhiteSpace(currentUrl) || !visited.Add(currentUrl))
                {
                    break;
                }

                if (i > 0 && delayMs > 0)
                {
                    await Task.Delay(delayMs, cancellationToken);
                }

                currentChapter = await FetchReadableChapterAsync(currentUrl, client, cancellationToken);
            }
            else
            {
                visited.Add(currentUrl!);
            }

            imported++;
            await onChapterFetched(currentChapter, imported, totalTarget);

            if (i + 1 >= totalTarget) break;

            string? nextUrl = useRangeUrlPattern
                ? TryReplaceUrlChapterNumber(initialUrl, startChapter + i + 1) ?? currentChapter.NextChapterUrl
                : currentChapter.NextChapterUrl;

            if (string.IsNullOrWhiteSpace(nextUrl) || visited.Contains(nextUrl))
            {
                break;
            }

            currentUrl = nextUrl;
            currentChapter = null;
        }

        if (imported == 0)
        {
            throw new InvalidOperationException(
                "ไม่พบเนื้อหาบทความในหน้าเว็บนี้ (เว็บอาจโหลดเนื้อหาด้วย JavaScript หรือมีระบบป้องกัน) กรุณาคัดลอกข้อความมาวางเอง");
        }

        return imported;
    }

    public static bool TryAnalyzePageForBatchImport(
        string pageUrl,
        string html,
        WebImportRangeMode mode,
        out IReadOnlyList<(string Url, int? ChapterNumber, string Title)> tocLinks,
        out WebChapterResult? firstArticle)
    {
        tocLinks = ExtractChapterLinksFromTocHtml(pageUrl, html);
        bool hasArticle = TryExtractArticle(pageUrl, html, out firstArticle, out int unitCount);

        bool isTocPage = tocLinks.Count > 0
            && (!hasArticle || (tocLinks.Count >= 5 && (mode == WebImportRangeMode.ByRange || unitCount < 180)));

        return isTocPage;
    }

    public static IReadOnlyList<(string Url, int? ChapterNumber, string Title)> SelectTocLinksForImport(
        IReadOnlyList<(string Url, int? ChapterNumber, string Title)> tocLinks,
        WebImportRangeMode mode,
        int count,
        int startChapter,
        int endChapter)
    {
        if (tocLinks.Count == 0) return Array.Empty<(string, int?, string)>();

        if (mode == WebImportRangeMode.ByRange)
        {
            int numberedCount = tocLinks.Count(l => l.ChapterNumber.HasValue);
            var matched = tocLinks
                .Where(l => l.ChapterNumber.HasValue && l.ChapterNumber.Value >= startChapter && l.ChapterNumber.Value <= endChapter)
                .ToList();

            if (matched.Count > 0 && (numberedCount >= 2 || matched.Count >= 2))
            {
                if (matched.Count >= 2 && matched[0].ChapterNumber > matched[^1].ChapterNumber)
                {
                    matched.Reverse();
                }
                return matched.Take(200).ToList();
            }

            int take = Math.Clamp(endChapter - startChapter + 1, 1, 200);
            var ordered = tocLinks.ToList();
            if (ordered.Count >= 2 && ordered[0].ChapterNumber.HasValue && ordered[^1].ChapterNumber.HasValue &&
                ordered[0].ChapterNumber > ordered[^1].ChapterNumber)
            {
                ordered.Reverse();
            }
            return ordered.Skip(Math.Max(0, startChapter - 1)).Take(take).ToList();
        }

        return tocLinks.Take(Math.Clamp(count, 1, 200)).ToList();
    }

    private static async Task<(string FinalUrl, string Html)> FetchRawHtmlAsync(string url, HttpClient httpClient, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("ลิงก์ไม่ถูกต้อง กรุณาระบุ URL ที่ขึ้นต้นด้วย http:// หรือ https://", nameof(url));
        }

        if (IsBlockedUri(uri))
        {
            throw new ArgumentException("ไม่อนุญาตให้ดึงข้อมูลจากเครือข่ายภายในหรือ localhost", nameof(url));
        }

        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"เว็บไซต์ตอบกลับรหัส {(int)response.StatusCode} ({response.ReasonPhrase}) — เว็บอาจมีระบบป้องกันบอท กรุณาคัดลอกข้อความมาวางเอง");
            }

            Uri finalUri = response.RequestMessage?.RequestUri ?? uri;
            if (IsBlockedUri(finalUri))
            {
                throw new ArgumentException("ไม่อนุญาตให้ดึงข้อมูลจากเครือข่ายภายในหรือ localhost", nameof(url));
            }

            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!string.IsNullOrWhiteSpace(mediaType) &&
                !mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) &&
                !mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"URL ไม่ใช่หน้าเว็บ HTML หรือข้อความ ({mediaType})");
            }

            if (response.Content.Headers.ContentLength > MaxResponseBytes)
            {
                throw new NotSupportedException("หน้าเว็บมีขนาดใหญ่เกิน 5 MB");
            }

            string html = await ReadContentWithLimitAsync(response.Content, MaxResponseBytes, cancellationToken);
            return (finalUri.ToString(), html);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("เชื่อมต่อเว็บไซต์นานเกินกำหนด กรุณาลองใหม่หรือคัดลอกข้อความมาวางเอง");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == null)
        {
            throw new HttpRequestException(
                $"ไม่สามารถดึงหน้าเว็บได้ ({ex.Message}) — เว็บอาจบล็อกการเข้าถึงหรือต้องใช้เบราว์เซอร์ กรุณาคัดลอกข้อความมาวางเอง", ex);
        }
    }

    public static WebChapterResult ExtractFromHtml(string url, string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidOperationException("หน้าเว็บไม่มีเนื้อหา HTML");
        }

        if (!TryExtractArticle(url, html, out var result, out _))
        {
            throw new InvalidOperationException(
                "ไม่พบเนื้อหาบทความในหน้าเว็บนี้ (เว็บอาจโหลดเนื้อหาด้วย JavaScript หรือมีระบบป้องกัน) กรุณาคัดลอกข้อความมาวางเอง");
        }

        return result!;
    }

    private static bool TryExtractArticle(string url, string html, out WebChapterResult? result, out int unitCount)
    {
        result = null;
        unitCount = 0;

        if (string.IsNullOrWhiteSpace(html)) return false;

        var reader = new Reader(url, html);
        var article = reader.GetArticle();

        if (article == null ||
            string.IsNullOrWhiteSpace(article.TextContent) ||
            (!article.IsReadable && article.TextContent.Trim().Length < 80))
        {
            return false;
        }

        string normalized = NormalizeText(article.TextContent);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        int spaceWords = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        unitCount = Math.Max(spaceWords, normalized.Length / 5);

        string title = SmartChapterNaming.ResolveTitle(article.Title, normalized, 0);
        string? nextUrl = ExtractNextChapterUrl(url, html);
        result = new WebChapterResult(title, normalized, nextUrl);
        return true;
    }

    private static readonly string[] NextTextExact =
    {
        "next", "next chapter", "next page", "next >", "next »", "next →",
        "ตอนถัดไป", "ตอนต่อไป", "ถัดไป", "บทถัดไป",
        "下一章", "下一节", "下一页", "次へ", "次の話", "다음화"
    };

    private static readonly string[] NextAttrMarkers =
    {
        "next-chap", "next_chap", "btn-next", "chr-next", "next_url", "nextchapter", "chap-next", "chapter-next"
    };

    public static string? ExtractNextChapterUrl(string currentUrl, string html)
    {
        if (string.IsNullOrWhiteSpace(currentUrl) || string.IsNullOrWhiteSpace(html))
        {
            return TryIncrementUrlChapterNumber(currentUrl);
        }

        if (!Uri.TryCreate(currentUrl.Trim(), UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        string currentClean = StripFragment(baseUri).ToString();
        var doc = new HtmlParser().ParseDocument(html);

        foreach (var link in doc.QuerySelectorAll("link[rel]"))
        {
            if (HasRelToken(link.GetAttribute("rel"), "next"))
            {
                string? candidate = ResolveSafeUrl(baseUri, link.GetAttribute("href"), currentClean);
                if (candidate != null) return candidate;
            }
        }

        foreach (var anchor in doc.QuerySelectorAll("a[href]"))
        {
            if (HasRelToken(anchor.GetAttribute("rel"), "next"))
            {
                string? candidate = ResolveSafeUrl(baseUri, anchor.GetAttribute("href"), currentClean);
                if (candidate != null) return candidate;
            }
        }

        foreach (var anchor in doc.QuerySelectorAll("a[href]"))
        {
            string text = NormalizeWhitespace(anchor.TextContent).ToLowerInvariant();
            string aria = (anchor.GetAttribute("aria-label") ?? string.Empty).Trim().ToLowerInvariant();
            string title = (anchor.GetAttribute("title") ?? string.Empty).Trim().ToLowerInvariant();
            string id = (anchor.GetAttribute("id") ?? string.Empty).Trim().ToLowerInvariant();
            string cls = (anchor.GetAttribute("class") ?? string.Empty).Trim().ToLowerInvariant();

            if (cls.Contains("disabled") || cls.Contains("prev") || id.Contains("prev"))
            {
                continue;
            }

            bool isTextMatch = (text.Length > 0 && text.Length <= 40 && IsNextLabel(text))
                || (aria.Length > 0 && aria.Length <= 40 && IsNextLabel(aria))
                || (title.Length > 0 && title.Length <= 40 && IsNextLabel(title));

            bool isAttrMatch = NextAttrMarkers.Any(m => id.Contains(m, StringComparison.Ordinal) || cls.Contains(m, StringComparison.Ordinal));

            if (isTextMatch || isAttrMatch)
            {
                string? candidate = ResolveSafeUrl(baseUri, anchor.GetAttribute("href"), currentClean);
                if (candidate != null) return candidate;
            }
        }

        return TryIncrementUrlChapterNumber(currentClean);
    }

    private static bool IsNextLabel(string lowerText)
    {
        if (lowerText.Contains("prev", StringComparison.Ordinal) || lowerText.Contains("ก่อนหน้า", StringComparison.Ordinal))
        {
            return false;
        }

        if (NextTextExact.Contains(lowerText, StringComparer.Ordinal))
        {
            return true;
        }

        return lowerText.StartsWith("next chapter", StringComparison.Ordinal)
            || lowerText.StartsWith("ตอนถัดไป", StringComparison.Ordinal)
            || lowerText.StartsWith("ตอนต่อไป", StringComparison.Ordinal)
            || lowerText.Contains("下一章", StringComparison.Ordinal);
    }

    private static bool HasRelToken(string? rel, string token)
    {
        if (string.IsNullOrWhiteSpace(rel)) return false;
        return rel.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Any(t => t.Equals(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveSafeUrl(Uri baseUri, string? href, string currentCleanUrl)
    {
        if (string.IsNullOrWhiteSpace(href)) return null;
        string trimmed = href.Trim();
        if (trimmed.StartsWith('#')
            || trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!Uri.TryCreate(baseUri, trimmed, out var resolved)) return null;
        if (resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps) return null;
        if (IsBlockedUri(resolved)) return null;

        string clean = StripFragment(resolved).ToString();
        return string.Equals(clean, currentCleanUrl, StringComparison.OrdinalIgnoreCase) ? null : clean;
    }

    private static Uri StripFragment(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.Fragment)) return uri;
        return new UriBuilder(uri) { Fragment = string.Empty }.Uri;
    }

    public static bool IsTemplateUrl(string url)
        => !string.IsNullOrWhiteSpace(url)
            && (url.Contains("{n}", StringComparison.OrdinalIgnoreCase) || url.Contains("{0}", StringComparison.OrdinalIgnoreCase));

    private static readonly Regex UrlNamedChapterRegex = new(
        @"(?i)(chapter|chap|ch|ep|episode|ตอนที่)([-_=/\s]*)(\d+)",
        RegexOptions.Compiled);

    private static readonly Regex UrlTrailingNumberRegex = new(
        @"/(\d+)(/?(?:\.html?)?(?:\?.*)?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? TryIncrementUrlChapterNumber(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || IsTemplateUrl(url)) return null;
        int? current = ExtractUrlChapterNumber(url);
        return current.HasValue ? TryReplaceUrlChapterNumber(url, current.Value + 1) : null;
    }

    public static string? TryReplaceUrlChapterNumber(string? url, int targetChapter)
    {
        if (string.IsNullOrWhiteSpace(url) || targetChapter < 0) return null;

        string trimmed = url.Trim();
        if (IsTemplateUrl(trimmed))
        {
            return Regex.Replace(trimmed, @"\{(?:n|0)\}", targetChapter.ToString(), RegexOptions.IgnoreCase);
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        string pathAndQuery = uri.PathAndQuery;

        var namedMatches = UrlNamedChapterRegex.Matches(pathAndQuery);
        string? newPathAndQuery = null;

        if (namedMatches.Count > 0)
        {
            var last = namedMatches[^1];
            string digits = last.Groups[3].Value;
            string formatted = FormatPreservingPadding(digits, targetChapter);
            newPathAndQuery = string.Concat(
                pathAndQuery.AsSpan(0, last.Groups[3].Index),
                formatted,
                pathAndQuery.AsSpan(last.Groups[3].Index + digits.Length));
        }
        else
        {
            var trailing = UrlTrailingNumberRegex.Match(pathAndQuery);
            if (trailing.Success)
            {
                string digits = trailing.Groups[1].Value;
                string formatted = FormatPreservingPadding(digits, targetChapter);
                newPathAndQuery = string.Concat(
                    pathAndQuery.AsSpan(0, trailing.Groups[1].Index),
                    formatted,
                    pathAndQuery.AsSpan(trailing.Groups[1].Index + digits.Length));
            }
        }

        if (newPathAndQuery == null) return null;
        string origin = uri.GetLeftPart(UriPartial.Authority);
        return origin + newPathAndQuery;
    }

    public static int? ExtractUrlChapterNumber(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        string pq = uri.PathAndQuery;

        var named = UrlNamedChapterRegex.Matches(pq);
        if (named.Count > 0 && int.TryParse(named[^1].Groups[3].Value, out int n))
        {
            return n;
        }

        var trailing = UrlTrailingNumberRegex.Match(pq);
        if (trailing.Success && int.TryParse(trailing.Groups[1].Value, out int t))
        {
            return t;
        }

        return null;
    }

    private static string FormatPreservingPadding(string originalDigits, int value)
    {
        if (originalDigits.Length > 1 && originalDigits.StartsWith('0'))
        {
            return value.ToString($"D{originalDigits.Length}");
        }
        return value.ToString();
    }

    private static readonly Regex TextChapterNumRegex = new(
        @"(?i)(?:chapter|chap|ch\.?|ep\.?|episode|ตอนที่|บทที่|第)\s*0*(\d+)",
        RegexOptions.Compiled);

    private static readonly Regex LeadingNumRegex = new(
        @"^0*(\d{1,5})(?:\s*[:\.\-–—\s]|$)",
        RegexOptions.Compiled);

    private static readonly Regex ChapterKeywordRegex = new(
        @"(?i)(?:chapter|chap|ch\.?\s*\d|ep\.?\s*\d|episode|ตอนที่|บทที่|第\s*\d+|/\d+/?(?:\.html?)?$)",
        RegexOptions.Compiled);

    public static IReadOnlyList<(string Url, int? ChapterNumber, string Title)> ExtractChapterLinksFromTocHtml(string pageUrl, string html)
    {
        if (string.IsNullOrWhiteSpace(pageUrl) || string.IsNullOrWhiteSpace(html) ||
            !Uri.TryCreate(pageUrl.Trim(), UriKind.Absolute, out var baseUri))
        {
            return Array.Empty<(string, int?, string)>();
        }

        string cleanPageUrl = StripFragment(baseUri).ToString();
        string baseHost = NormalizeHost(baseUri.Host);
        var doc = new HtmlParser().ParseDocument(html);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<(string Url, int? ChapterNumber, string Title)>();

        foreach (var anchor in doc.QuerySelectorAll("a[href]"))
        {
            string? resolvedUrl = ResolveSafeUrl(baseUri, anchor.GetAttribute("href"), cleanPageUrl);
            if (resolvedUrl == null) continue;

            if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var resolvedUri)) continue;
            if (!string.Equals(NormalizeHost(resolvedUri.Host), baseHost, StringComparison.OrdinalIgnoreCase)) continue;

            string pqLower = resolvedUri.PathAndQuery.ToLowerInvariant();
            if (pqLower.Contains("/page/", StringComparison.Ordinal)
                || pqLower.Contains("?page=", StringComparison.Ordinal)
                || pqLower.Contains("login", StringComparison.Ordinal)
                || pqLower.Contains("register", StringComparison.Ordinal)
                || pqLower.Contains("comment", StringComparison.Ordinal))
            {
                continue;
            }

            string text = NormalizeWhitespace(anchor.TextContent);
            if (text.Length > 180) continue;

            int? chapterNum = null;
            var tm = TextChapterNumRegex.Match(text);
            if (tm.Success && int.TryParse(tm.Groups[1].Value, out int parsedTextNum))
            {
                chapterNum = parsedTextNum;
            }
            else
            {
                var lm = LeadingNumRegex.Match(text);
                if (lm.Success && int.TryParse(lm.Groups[1].Value, out int parsedLeadNum))
                {
                    chapterNum = parsedLeadNum;
                }
                else
                {
                    chapterNum = ExtractUrlChapterNumber(resolvedUrl);
                }
            }

            bool looksLikeChapter = chapterNum.HasValue
                || ChapterKeywordRegex.IsMatch(text)
                || ChapterKeywordRegex.IsMatch(resolvedUri.PathAndQuery);

            if (!looksLikeChapter) continue;
            if (!seen.Add(resolvedUrl)) continue;

            string title = string.IsNullOrWhiteSpace(text)
                ? (chapterNum.HasValue ? $"ตอนที่ {chapterNum.Value}" : resolvedUri.AbsolutePath)
                : text;

            results.Add((resolvedUrl, chapterNum, title));
        }

        return results;
    }

    private static string NormalizeHost(string host)
        => host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;

    private static string NormalizeWhitespace(string raw)
        => string.IsNullOrWhiteSpace(raw) ? string.Empty : Regex.Replace(raw, @"\s+", " ").Trim();

    private static string NormalizeText(string raw)
    {
        var lines = raw.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(l => l.Trim());

        string joined = string.Join("\n", lines);
        return Regex.Replace(joined, @"\n{3,}", "\n\n").Trim();
    }

    public static bool IsBlockedUri(Uri uri)
    {
        if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(uri.Host, out var ip))
        {
            return false;
        }

        if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
        {
            return true;
        }

        byte[] bytes = ip.GetAddressBytes();
        if (bytes.Length == 4)
        {
            // Reject 0.0.0.0/8, 10.0.0.0/8, 127.0.0.0/8, 169.254.0.0/16, 172.16.0.0/12, 192.168.0.0/16
            return bytes[0] == 0
                || bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        // IPv6 unique local fc00::/7
        return bytes.Length == 16 && (bytes[0] & 0xFE) == 0xFC;
    }

    private static async Task<string> ReadContentWithLimitAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var ms = new MemoryStream();
        byte[] buffer = new byte[8192];
        int totalRead = 0;

        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;

            totalRead += read;
            if (totalRead > maxBytes)
            {
                throw new NotSupportedException("หน้าเว็บมีขนาดใหญ่เกิน 5 MB");
            }

            ms.Write(buffer, 0, read);
        }

        Encoding encoding = Encoding.UTF8;
        string? charset = content.Headers.ContentType?.CharSet?.Trim('"', ' ');
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                encoding = Encoding.GetEncoding(charset);
            }
            catch (ArgumentException)
            {
                encoding = Encoding.UTF8;
            }
        }

        return encoding.GetString(ms.GetBuffer(), 0, (int)ms.Length);
    }
}
