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

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
