using System;
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

        Assert.Contains("Chapter 12", result.Title);
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
}
