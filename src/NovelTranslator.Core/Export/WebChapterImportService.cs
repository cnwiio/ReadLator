using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SmartReader;

namespace NovelTranslator.Core.Export;

public sealed record WebChapterResult(string Title, string Content);

public static class WebChapterImportService
{
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

    public static async Task<WebChapterResult> FetchReadableChapterAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("ลิงก์ไม่ถูกต้อง กรุณาระบุ URL ที่ขึ้นต้นด้วย http:// หรือ https://", nameof(url));
        }

        string html;
        try
        {
            using var response = await HttpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"เว็บไซต์ตอบกลับรหัส {(int)response.StatusCode} ({response.ReasonPhrase}) — เว็บอาจมีระบบป้องกันบอท กรุณาคัดลอกข้อความมาวางเอง");
            }

            html = await response.Content.ReadAsStringAsync(cancellationToken);
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

        string title = string.IsNullOrWhiteSpace(article.Title) ? "ตอนจากเว็บ" : article.Title.Trim();
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
}
