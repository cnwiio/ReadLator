using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SmartReader;

namespace NovelTranslator.Core.Export;

public sealed record WebChapterResult(string Title, string Content);

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

        string html;
        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"เว็บไซต์ตอบกลับรหัส {(int)response.StatusCode} ({response.ReasonPhrase}) — เว็บอาจมีระบบป้องกันบอท กรุณาคัดลอกข้อความมาวางเอง");
            }

            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!string.IsNullOrWhiteSpace(mediaType) &&
                !mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) &&
                !mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"URL ไม่ใช่หน้าเว็บ HTML หรือข้อความ ({mediaType})");
            }

            if (response.Content.Headers.ContentLength > MaxResponseBytes)
            {
                throw new InvalidOperationException("หน้าเว็บมีขนาดใหญ่เกิน 5 MB");
            }

            html = await ReadContentWithLimitAsync(response.Content, MaxResponseBytes, cancellationToken);
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

        return ExtractFromHtml(uri.ToString(), html);
    }

    public static WebChapterResult ExtractFromHtml(string url, string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidOperationException("หน้าเว็บไม่มีเนื้อหา HTML");
        }

        var reader = new Reader(url, html);
        var article = reader.GetArticle();

        if (article == null ||
            string.IsNullOrWhiteSpace(article.TextContent) ||
            (!article.IsReadable && article.TextContent.Trim().Length < 80))
        {
            throw new InvalidOperationException(
                "ไม่พบเนื้อหาบทความในหน้าเว็บนี้ (เว็บอาจโหลดเนื้อหาด้วย JavaScript หรือมีระบบป้องกัน) กรุณาคัดลอกข้อความมาวางเอง");
        }

        string normalized = NormalizeText(article.TextContent);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("เนื้อหาที่ดึงได้ว่างเปล่า กรุณาคัดลอกข้อความมาวางเอง");
        }

        string title = SmartChapterNaming.ResolveTitle(article.Title, normalized, 0);
        return new WebChapterResult(title, normalized);
    }

    private static string NormalizeText(string raw)
    {
        var lines = raw.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(l => l.Trim());

        string joined = string.Join("\n", lines);
        return Regex.Replace(joined, @"\n{3,}", "\n\n").Trim();
    }

    private static bool IsBlockedUri(Uri uri)
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
                throw new InvalidOperationException("หน้าเว็บมีขนาดใหญ่เกิน 5 MB");
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
