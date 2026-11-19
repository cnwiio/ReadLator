using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NovelTranslator.Core.Export;
using Xunit;

namespace NovelTranslator.Tests;

public class WebChapterImportServiceTests
{
    [Fact]
    public void ExtractFromHtml_ExtractsTitleAndReadableParagraphs()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head><title>Chapter 12: The Awakening - NovelSite</title></head>
            <body>
                <nav><a href="/">Home</a> | <a href="/list">Chapters</a></nav>
                <article>
                    <h1>Chapter 12: The Awakening</h1>
                    <p>The night was quiet across the northern valley as Arthur stepped onto the balcony, letting the cold mountain air clear his mind after hours of studying old maps.</p>
                    <p>Far below, the lanterns of the harbor flickered against the rising mist, signaling the arrival of the courier ship that had been delayed for three days by the eastern storm.</p>
                    <p>He tightened his cloak and descended the stone staircase toward the courtyard, where the watchman was already waiting with a sealed letter in his gloved hand.</p>
                    <p>Whatever news had crossed the sea tonight, it would decide whether the garrison marched at dawn or held the pass through the winter.</p>
                </article>
                <footer>Copyright NovelSite</footer>
            </body>
            </html>
            """;

        var result = WebChapterImportService.ExtractFromHtml("https://example.com/ch12", html);

        Assert.Equal("ตอนที่ 12: The Awakening", result.Title);
        Assert.Contains("Arthur stepped onto the balcony", result.Content);
        Assert.Contains("descended the stone staircase", result.Content);
        Assert.DoesNotContain("Copyright NovelSite", result.Content);
    }

    [Fact]
    public async Task FetchReadableChapterAsync_RejectsNonHttpUrl()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            WebChapterImportService.FetchReadableChapterAsync("file:///C:/secret.txt"));
    }

    [Fact]
    public void ExtractFromHtml_ThrowsOnEmptyHtml()
    {
        Assert.Throws<InvalidOperationException>(() =>
            WebChapterImportService.ExtractFromHtml("https://example.com/empty", "   "));
    }

    [Theory]
    [InlineData("http://localhost:11434/api/tags")]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://192.168.1.1/status")]
    [InlineData("http://10.0.0.5/internal")]
    public async Task FetchReadableChapterAsync_RejectsLoopbackAndPrivateHosts(string url)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            WebChapterImportService.FetchReadableChapterAsync(url));
    }

    [Fact]
    public async Task FetchReadableChapterAsync_RejectsNonHtmlContentType()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 })
            };
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return resp;
        }));

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            WebChapterImportService.FetchReadableChapterAsync("https://example.com/file.bin", client));
    }

    [Fact]
    public async Task FetchReadableChapterAsync_RejectsOversizedContentLengthHeader()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("<html></html>")))
            };
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            resp.Content.Headers.ContentLength = WebChapterImportService.MaxResponseBytes + 1;
            return resp;
        }));

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            WebChapterImportService.FetchReadableChapterAsync("https://example.com/huge", client));
    }

    [Fact]
    public async Task FetchReadableChapterAsync_RejectsStreamExceedingMaxBytes()
    {
        byte[] oversized = new byte[WebChapterImportService.MaxResponseBytes + 1024];
        Array.Fill(oversized, (byte)'a');

        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(oversized))
            };
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return resp;
        }));

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            WebChapterImportService.FetchReadableChapterAsync("https://example.com/stream", client));
    }

    [Fact]
    public async Task FetchReadableChapterAsync_RejectsRedirectToLoopback()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(req =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://localhost:11434/api/tags"),
                Content = new StringContent("<html><body>hi</body></html>", Encoding.UTF8, "text/html")
            };
            return resp;
        }));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            WebChapterImportService.FetchReadableChapterAsync("https://example.com/redirect", client));
    }

    [Fact]
    public void ExtractNextChapterUrl_FindsNextLinkAndFallbackUrlIncrement()
    {
        const string htmlWithNextLink = """
            <!DOCTYPE html>
            <html><body>
                <a href="/novel/book/chapter-1">Previous</a>
                <a href="/novel/book/chapter-3?ref=nav#top" class="btn-next">Next Chapter</a>
            </body></html>
            """;

        string? nextFromHtml = WebChapterImportService.ExtractNextChapterUrl(
            "https://example.com/novel/book/chapter-2", htmlWithNextLink);
        Assert.Equal("https://example.com/novel/book/chapter-3?ref=nav", nextFromHtml);

        const string htmlWithoutNextLink = "<html><body><p>Plain chapter body without nav links.</p></body></html>";
        string? nextFromUrl = WebChapterImportService.ExtractNextChapterUrl(
            "https://example.com/novel/book/chapter-009.html", htmlWithoutNextLink);
        Assert.Equal("https://example.com/novel/book/chapter-010.html", nextFromUrl);
    }

    [Fact]
    public void ExtractChapterLinksFromTocHtml_ExtractsOrderedChapterLinks()
    {
        const string tocHtml = """
            <!DOCTYPE html>
            <html><body>
                <header><a href="/login">Login</a> | <a href="https://other.com/chapter-1">External</a></header>
                <ul class="chapters">
                    <li><a href="/book/ch-1">Chapter 1: Dawn</a></li>
                    <li><a href="/book/ch-2">Chapter 2: Noon</a></li>
                    <li><a href="/book/ch-2#comments">Chapter 2: Noon</a></li>
                    <li><a href="/book/ch-3">ตอนที่ 3: Dusk</a></li>
                </ul>
            </body></html>
            """;

        var links = WebChapterImportService.ExtractChapterLinksFromTocHtml("https://example.com/book", tocHtml);

        Assert.Equal(3, links.Count);
        Assert.Equal(("https://example.com/book/ch-1", 1, "Chapter 1: Dawn"), links[0]);
        Assert.Equal(("https://example.com/book/ch-2", 2, "Chapter 2: Noon"), links[1]);
        Assert.Equal(("https://example.com/book/ch-3", 3, "ตอนที่ 3: Dusk"), links[2]);
    }

    [Fact]
    public async Task FetchChaptersBatchAsync_ByCount_FollowsNextLinks()
    {
        static string BuildChapterHtml(int num, string? nextHref)
        {
            string nextAnchor = nextHref != null ? $"<a rel=\"next\" href=\"{nextHref}\">ตอนถัดไป</a>" : "";
            return $$"""
                <!DOCTYPE html>
                <html>
                <head><title>Chapter {{num}}: Trial {{num}} - TestSite</title></head>
                <body>
                    <article>
                        <h1>Chapter {{num}}: Trial {{num}}</h1>
                        <p>Chapter {{num}} opens under a pale lantern sky as the travelers cross the iron bridge and check their supplies before entering the silent valley.</p>
                        <p>They walk along the wet stone road past deserted watchtowers, listening carefully for distant bells carried across the fog by the cold eastern wind.</p>
                        <p>By midnight they reach the old archive gate, where the keeper unlocks the brass seal and hands over the ledger for chapter {{num}}.</p>
                    </article>
                    {{nextAnchor}}
                </body>
                </html>
                """;
        }

        using var client = new HttpClient(new StubHttpMessageHandler(req =>
        {
            string url = req.RequestUri!.ToString();
            string body = url switch
            {
                "https://example.com/story/c1" => BuildChapterHtml(1, "/story/c2"),
                "https://example.com/story/c2" => BuildChapterHtml(2, "/story/c3"),
                "https://example.com/story/c3" => BuildChapterHtml(3, null),
                _ => throw new InvalidOperationException($"Unexpected URL: {url}")
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = req,
                Content = new StringContent(body, Encoding.UTF8, "text/html")
            };
        }));

        var fetched = new System.Collections.Generic.List<(WebChapterResult Chapter, int Current, int Total)>();
        var options = new WebMultiImportOptions("https://example.com/story/c1", WebImportRangeMode.ByCount, Count: 3);

        int count = await WebChapterImportService.FetchChaptersBatchAsync(
            options,
            (chap, cur, total) =>
            {
                fetched.Add((chap, cur, total));
                return Task.CompletedTask;
            },
            client,
            delayMs: 0);

        Assert.Equal(3, count);
        Assert.Equal(3, fetched.Count);
        Assert.Equal("ตอนที่ 1: Trial 1", fetched[0].Chapter.Title);
        Assert.Equal("ตอนที่ 2: Trial 2", fetched[1].Chapter.Title);
        Assert.Equal("ตอนที่ 3: Trial 3", fetched[2].Chapter.Title);
        Assert.Equal((1, 3), (fetched[0].Current, fetched[0].Total));
        Assert.Equal((3, 3), (fetched[2].Current, fetched[2].Total));
    }

    [Fact]
    public async Task FetchChaptersBatchAsync_ByRange_ImportsRequestedSliceFromToc()
    {
        const string tocHtml = """
            <!DOCTYPE html>
            <html><body>
                <h1>My Novel TOC</h1>
                <ul>
                    <li><a href="/novel/chapter-1">Chapter 1: One</a></li>
                    <li><a href="/novel/chapter-2">Chapter 2: Two</a></li>
                    <li><a href="/novel/chapter-3">Chapter 3: Three</a></li>
                    <li><a href="/novel/chapter-4">Chapter 4: Four</a></li>
                    <li><a href="/novel/chapter-5">Chapter 5: Five</a></li>
                </ul>
            </body></html>
            """;

        static string ChapterBody(int n) => $$"""
            <!DOCTYPE html>
            <html>
            <head><title>Chapter {{n}}: Arc {{n}}</title></head>
            <body>
                <article>
                    <h1>Chapter {{n}}: Arc {{n}}</h1>
                    <p>This is the full readable narrative for chapter {{n}}, describing the long march across the ridge and the quiet camp beside the river.</p>
                    <p>Every scout reported clear roads ahead while the quartermaster tallied rations and prepared horses for the next leg of the journey.</p>
                    <p>Before sunrise the signal horn sounded across the camp, marking the completion of stage {{n}} without incident.</p>
                </article>
            </body>
            </html>
            """;

        using var client = new HttpClient(new StubHttpMessageHandler(req =>
        {
            string url = req.RequestUri!.ToString();
            string html = url switch
            {
                "https://example.com/novel" => tocHtml,
                "https://example.com/novel/chapter-2" => ChapterBody(2),
                "https://example.com/novel/chapter-3" => ChapterBody(3),
                "https://example.com/novel/chapter-4" => ChapterBody(4),
                _ => throw new InvalidOperationException($"Unexpected URL: {url}")
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = req,
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            };
        }));

        var fetched = new System.Collections.Generic.List<WebChapterResult>();
        var options = new WebMultiImportOptions(
            "https://example.com/novel",
            WebImportRangeMode.ByRange,
            StartChapter: 2,
            EndChapter: 4);

        int count = await WebChapterImportService.FetchChaptersBatchAsync(
            options,
            (chap, _, _) =>
            {
                fetched.Add(chap);
                return Task.CompletedTask;
            },
            client,
            delayMs: 0);

        Assert.Equal(3, count);
        Assert.Equal(new[] { "ตอนที่ 2: Arc 2", "ตอนที่ 3: Arc 3", "ตอนที่ 4: Arc 4" },
            System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(fetched, c => c.Title)));
    }

    [Fact]
    public void ExtractFromHtml_ExtractsMadaraReadingContentAndStripsHiddenWatermarks()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head><title>บทที่ 2 สังหารล้างหมู่บ้าน - Aileen Novel</title></head>
            <body>
                <div class="c-blog-post">
                    <div class="entry-header"><h1 id="chapter-heading">บทที่ 2: สังหารล้างหมู่บ้าน</h1></div>
                    <div class="entry-content">
                        <div class="reading-content" style="user-select:none">
                            <div class="text-left">
                                <p>สายลมหนาวพัดผ่านหุบเขาอันเงียบสงัดในยามค่ำคืนขณะที่ชายหนุ่มยืนจ้องมองแสงไฟที่ปลายขอบฟ้า</p>
                                <p><span style="display:none">ห้ามคัดลอกมาจากเว็บเถื่อน watermark123</span>เสียงฝีเท้าม้าดังก้องใกล้เข้ามาตามทางเดินหินเก่าแก่ที่ปกคลุมไปด้วยหมอกขาว</p>
                                <p style="opacity:0">ข้อความลวงตา</p>
                                <p>เขากระชับด้ามดาบในมือแน่นพร้อมเตรียมรับมือกับศัตรูที่กำลังจะปรากฏตัวขึ้นเบื้องหน้า</p>
                            </div>
                        </div>
                    </div>
                </div>
            </body>
            </html>
            """;

        var result = WebChapterImportService.ExtractFromHtml("https://example.com/novel/ch-2", html);

        Assert.Equal("ตอนที่ 2: สังหารล้างหมู่บ้าน", result.Title);
        Assert.Contains("สายลมหนาวพัดผ่านหุบเขา", result.Content);
        Assert.Contains("เสียงฝีเท้าม้าดังก้อง", result.Content);
        Assert.DoesNotContain("watermark123", result.Content);
        Assert.DoesNotContain("ข้อความลวงตา", result.Content);
    }

    [Fact]
    public void ExtractFromHtml_ThrowsNotSupportedWhenContentUsesGlyphShieldPuaCipher()
    {
        string cipherBody = string.Concat(Enumerable.Repeat("\uE012\uE035\uE07A\uE09B\uE104\uE120 ", 20));
        string html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <style>@font-face { font-family: 'MadaraGlyphShield-032'; src: url('/mgs-032.woff2'); }</style>
                <title>บทที่ 1 การข้ามเวลาที่น่า - Example</title>
            </head>
            <body>
                <div class="reading-content">
                    <div class="text-left" style="font-family: 'MadaraGlyphShield-032'">
                        <p>{{cipherBody}}</p>
                    </div>
                </div>
            </body>
            </html>
            """;

        var ex = Assert.Throws<NotSupportedException>(() =>
            WebChapterImportService.ExtractFromHtml("https://example.com/novel/ch-1", html));
        Assert.Contains("GlyphShield", ex.Message);
    }

    [Fact]
    public void ExtractFromHtml_AllowsNormalChapterWithStrayIconPuaCharacter()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head><title>บทที่ 3 ทดสอบ - Example</title></head>
            <body>
                <article>
                    <h1>บทที่ 3: ทดสอบ</h1>
                    <p>&#xE001; สายลมหนาวพัดผ่านหุบเขาอันเงียบสงัดในยามค่ำคืนขณะที่ชายหนุ่มยืนจ้องมองแสงไฟที่ปลายขอบฟ้า</p>
                    <p>เสียงฝีเท้าม้าดังก้องใกล้เข้ามาตามทางเดินหินเก่าแก่ที่ปกคลุมไปด้วยหมอกขาวในหุบเขาแห่งความมืด</p>
                </article>
            </body>
            </html>
            """;

        var result = WebChapterImportService.ExtractFromHtml("https://example.com/novel/ch-3", html);
        Assert.Contains("สายลมหนาวพัดผ่านหุบเขา", result.Content);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
