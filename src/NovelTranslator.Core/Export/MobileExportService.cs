using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using NovelTranslator.Core.Models;

namespace NovelTranslator.Core.Export;

public enum ExportContentMode
{
    TranslatedOnly,
    Bilingual
}

public static class MobileExportService
{
    public static string BuildMobileHtml(
        Story story,
        IEnumerable<Chapter> chapters,
        ExportContentMode contentMode,
        IReadOnlyList<Story>? allStories = null)
    {
        var orderedChapters = chapters
            .OrderBy(c => c.Ordinal)
            .Where(c => !string.IsNullOrWhiteSpace(c.TranslatedText) || !string.IsNullOrWhiteSpace(c.OriginalText))
            .ToList();

        var chapterDtos = orderedChapters.Select((c, idx) => new
        {
            index = idx,
            title = c.Title,
            html = BuildChapterBodyHtml(c, contentMode)
        }).ToList();

        var jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string chaptersJson = JsonSerializer.Serialize(chapterDtos, jsonOptions)
            .Replace("<", "\\u003c", StringComparison.Ordinal)
            .Replace(">", "\\u003e", StringComparison.Ordinal)
            .Replace("\u2028", "\\u2028", StringComparison.Ordinal)
            .Replace("\u2029", "\\u2029", StringComparison.Ordinal);

        string storyTitleHtml = WebUtility.HtmlEncode(story.Title);
        bool hasMultiStories = allStories != null && allStories.Count > 1;
        string storySelectorHtml = string.Empty;
        if (hasMultiStories)
        {
            var sbStory = new StringBuilder();
            sbStory.Append("<select id=\"storySelect\" class=\"ctrl-select\" onchange=\"switchStory(this.value)\" aria-label=\"เลือกเรื่อง\">");
            foreach (var s in allStories!)
            {
                string selected = s.Id == story.Id ? " selected" : string.Empty;
                sbStory.Append($"<option value=\"{s.Id}\"{selected}>{WebUtility.HtmlEncode(s.Title)}</option>");
            }
            sbStory.Append("</select>");
            storySelectorHtml = sbStory.ToString();
        }

        string modeToggleHtml = contentMode == ExportContentMode.Bilingual
            ? "<button id=\"bilingualBtn\" class=\"ctrl-btn\" onclick=\"toggleOriginal()\">ซ่อนต้นฉบับ</button>"
            : (allStories != null
                ? $"<a class=\"ctrl-btn\" href=\"/?story={story.Id}&amp;mode=bilingual\">แสดงคู่ต้นฉบับ</a>"
                : string.Empty);

        string wifiBilingualLink = allStories != null && contentMode == ExportContentMode.Bilingual
            ? $"<a class=\"ctrl-btn\" href=\"/?story={story.Id}&amp;mode=translated\">โหมดแปลล้วน</a>"
            : string.Empty;

        string initialContent = orderedChapters.Count > 0
            ? string.Empty
            : "<div class=\"empty-state\">ยังไม่มีเนื้อหาสำหรับแสดงผล</div>";

        return $$"""
<!DOCTYPE html>
<html lang="th">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />
<title>{{storyTitleHtml}} — Mobile Reader</title>
<style>
  :root {
    --bg: #FAF9F5;
    --card: #FFFFFF;
    --text: #1C1917;
    --muted: #6B6560;
    --border: #E7E5E0;
    --accent: #2563EB;
  }
  body.theme-dark {
    --bg: #141416;
    --card: #1D1D20;
    --text: #E5E5E7;
    --muted: #9A9AA1;
    --border: #2C2C32;
    --accent: #60A5FA;
  }
  body.theme-sepia {
    --bg: #F4ECD8;
    --card: #FAF4E6;
    --text: #3E3224;
    --muted: #7C6A55;
    --border: #E2D5BA;
    --accent: #9A5B25;
  }
  * { box-sizing: border-box; }
  body {
    margin: 0;
    background: var(--bg);
    color: var(--text);
    font-family: -apple-system, BlinkMacSystemFont, "Sarabun", "Leelawadee UI", "Tahoma", sans-serif;
    line-height: 1.85;
    transition: background-color 0.2s, color 0.2s;
  }
  body.hide-orig .orig-p { display: none; }
  .topbar {
    position: sticky;
    top: 0;
    z-index: 20;
    background: var(--card);
    border-bottom: 1px solid var(--border);
    padding: 10px 14px;
    display: flex;
    flex-direction: column;
    gap: 8px;
  }
  .topbar-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    flex-wrap: wrap;
  }
  .story-title {
    font-size: 15px;
    font-weight: 600;
    margin: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .ctrl-group {
    display: flex;
    align-items: center;
    gap: 6px;
    flex-wrap: wrap;
  }
  .ctrl-btn, .ctrl-select {
    background: var(--bg);
    color: var(--text);
    border: 1px solid var(--border);
    border-radius: 8px;
    padding: 6px 10px;
    font-size: 13px;
    text-decoration: none;
    cursor: pointer;
    min-height: 34px;
  }
  .ctrl-select { max-width: 100%; flex: 1; min-width: 140px; }
  .ctrl-btn.active { border-color: var(--accent); color: var(--accent); font-weight: 600; }
  .reader-wrap {
    max-width: 740px;
    margin: 0 auto;
    padding: 20px 18px 64px;
  }
  .chapter-heading {
    font-size: 1.35em;
    font-weight: 700;
    margin: 8px 0 22px;
    line-height: 1.4;
  }
  .chapter-body { font-size: 18px; }
  .pair { margin-bottom: 18px; }
  .trans-p { margin: 0 0 16px; }
  .pair .trans-p { margin-bottom: 0; }
  .orig-p {
    margin: 0 0 6px;
    color: var(--muted);
    font-size: 0.86em;
    line-height: 1.6;
  }
  .untranslated-note {
    color: var(--muted);
    font-style: italic;
    margin: 0 0 12px;
    font-size: 0.9em;
  }
  .nav-bar {
    display: flex;
    justify-content: space-between;
    gap: 12px;
    margin-top: 32px;
    padding-top: 18px;
    border-top: 1px solid var(--border);
  }
  .nav-bar .ctrl-btn {
    flex: 1;
    text-align: center;
    padding: 10px 12px;
    font-weight: 500;
  }
  .ctrl-btn:disabled { opacity: 0.4; cursor: default; }
  .empty-state {
    text-align: center;
    padding: 48px 16px;
    color: var(--muted);
  }
</style>
</head>
<body>
  <header class="topbar">
    <div class="topbar-row">
      {{(hasMultiStories ? storySelectorHtml : $"<h1 class=\"story-title\">{storyTitleHtml}</h1>")}}
      <div class="ctrl-group">
        <button class="ctrl-btn" onclick="changeFontSize(-2)" aria-label="ลดขนาดตัวอักษร">A-</button>
        <button class="ctrl-btn" onclick="changeFontSize(2)" aria-label="เพิ่มขนาดตัวอักษร">A+</button>
        <button id="tLight" class="ctrl-btn" onclick="setTheme('light')">สว่าง</button>
        <button id="tSepia" class="ctrl-btn" onclick="setTheme('sepia')">ถนอมตา</button>
        <button id="tDark" class="ctrl-btn" onclick="setTheme('dark')">มืด</button>
      </div>
    </div>
    <div class="topbar-row">
      <select id="chapterSelect" class="ctrl-select" onchange="goToChapter(parseInt(this.value, 10))" aria-label="เลือกตอน"></select>
      <div class="ctrl-group">
        {{modeToggleHtml}}
        {{wifiBilingualLink}}
      </div>
    </div>
  </header>

  <main class="reader-wrap">
    <h2 id="chapterTitle" class="chapter-heading"></h2>
    <article id="chapterBody" class="chapter-body">{{initialContent}}</article>
    <nav class="nav-bar">
      <button id="prevBtn" class="ctrl-btn" onclick="prevChapter()">← ตอนก่อนหน้า</button>
      <button id="nextBtn" class="ctrl-btn" onclick="nextChapter()">ตอนถัดไป →</button>
    </nav>
  </main>

<script>
const CHAPTERS = {{chaptersJson}};
const STORY_KEY = 'nt_reader_ch_{{story.Id}}';
const THEME_KEY = 'nt_reader_theme';
const SIZE_KEY = 'nt_reader_size';
const MODE_PARAM = '{{(contentMode == ExportContentMode.Bilingual ? "bilingual" : "translated")}}';
let currentIdx = 0;
let fontSize = 18;

function init() {
  const savedTheme = localStorage.getItem(THEME_KEY) || 'light';
  setTheme(savedTheme);
  const savedSize = parseInt(localStorage.getItem(SIZE_KEY) || '18', 10);
  if (savedSize >= 14 && savedSize <= 28) fontSize = savedSize;
  document.getElementById('chapterBody').style.fontSize = fontSize + 'px';

  const select = document.getElementById('chapterSelect');
  CHAPTERS.forEach((ch, idx) => {
    const opt = document.createElement('option');
    opt.value = idx;
    opt.textContent = ch.title;
    select.appendChild(opt);
  });

  if (CHAPTERS.length === 0) {
    document.getElementById('prevBtn').disabled = true;
    document.getElementById('nextBtn').disabled = true;
    return;
  }

  const savedIdx = parseInt(localStorage.getItem(STORY_KEY) || '0', 10);
  goToChapter(savedIdx >= 0 && savedIdx < CHAPTERS.length ? savedIdx : 0, false);
}

function goToChapter(idx, scroll = true) {
  if (idx < 0 || idx >= CHAPTERS.length) return;
  currentIdx = idx;
  const ch = CHAPTERS[idx];
  document.getElementById('chapterTitle').textContent = ch.title;
  document.getElementById('chapterBody').innerHTML = ch.html;
  document.getElementById('chapterSelect').value = String(idx);
  document.getElementById('prevBtn').disabled = idx <= 0;
  document.getElementById('nextBtn').disabled = idx >= CHAPTERS.length - 1;
  localStorage.setItem(STORY_KEY, String(idx));
  if (scroll) window.scrollTo({ top: 0, behavior: 'smooth' });
}

function prevChapter() { goToChapter(currentIdx - 1); }
function nextChapter() { goToChapter(currentIdx + 1); }

function changeFontSize(delta) {
  fontSize = Math.min(28, Math.max(14, fontSize + delta));
  document.getElementById('chapterBody').style.fontSize = fontSize + 'px';
  localStorage.setItem(SIZE_KEY, String(fontSize));
}

function setTheme(name) {
  document.body.classList.remove('theme-dark', 'theme-sepia');
  if (name === 'dark') document.body.classList.add('theme-dark');
  else if (name === 'sepia') document.body.classList.add('theme-sepia');
  ['tLight', 'tSepia', 'tDark'].forEach(id => document.getElementById(id).classList.remove('active'));
  const map = { light: 'tLight', sepia: 'tSepia', dark: 'tDark' };
  if (map[name]) document.getElementById(map[name]).classList.add('active');
  localStorage.setItem(THEME_KEY, name);
}

function toggleOriginal() {
  const hidden = document.body.classList.toggle('hide-orig');
  const btn = document.getElementById('bilingualBtn');
  if (btn) btn.textContent = hidden ? 'แสดงต้นฉบับ' : 'ซ่อนต้นฉบับ';
}

function switchStory(storyId) {
  window.location.href = '/?story=' + encodeURIComponent(storyId) + '&mode=' + encodeURIComponent(MODE_PARAM);
}

init();
</script>
</body>
</html>
""";
    }

    public static async Task ExportHtmlAsync(
        string filePath,
        Story story,
        IEnumerable<Chapter> chapters,
        ExportContentMode contentMode)
    {
        string html = BuildMobileHtml(story, chapters, contentMode);
        await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);
    }

    public static async Task ExportEpubAsync(
        string filePath,
        Story story,
        IEnumerable<Chapter> chapters,
        ExportContentMode contentMode)
    {
        var orderedChapters = chapters
            .OrderBy(c => c.Ordinal)
            .Where(c => !string.IsNullOrWhiteSpace(c.TranslatedText) || !string.IsNullOrWhiteSpace(c.OriginalText))
            .ToList();

        string bookId = $"urn:uuid:{Guid.NewGuid()}";
        string escapedBookTitle = WebUtility.HtmlEncode(story.Title);
        string lang = string.IsNullOrWhiteSpace(story.TargetLang) ? "th" : (story.TargetLang.Contains("Thai", StringComparison.OrdinalIgnoreCase) ? "th" : "en");

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

        // 1. mimetype (must be first and uncompressed per EPUB spec)
        var mimeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
        await using (var mimeStream = mimeEntry.Open())
        {
            byte[] mimeBytes = Encoding.ASCII.GetBytes("application/epub+zip");
            await mimeStream.WriteAsync(mimeBytes);
        }

        // 2. META-INF/container.xml
        await WriteZipTextAsync(zip, "META-INF/container.xml", """
<?xml version="1.0" encoding="UTF-8"?>
<container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
  <rootfiles>
    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
  </rootfiles>
</container>
""");

        // 3. OEBPS/style.css
        await WriteZipTextAsync(zip, "OEBPS/style.css", """
body { font-family: sans-serif; line-height: 1.75; margin: 1.2em; }
h1 { font-size: 1.35em; margin-bottom: 1.2em; }
p.trans { margin: 0 0 1em 0; text-indent: 1.5em; }
p.orig { margin: 0 0 0.45em 0; color: #666666; font-size: 0.88em; }
p.note { color: #777777; font-style: italic; }
""");

        // 4. Chapters
        var manifestSb = new StringBuilder();
        var spineSb = new StringBuilder();
        var navListSb = new StringBuilder();
        var ncxNavSb = new StringBuilder();

        for (int i = 0; i < orderedChapters.Count; i++)
        {
            var chap = orderedChapters[i];
            int idx = i + 1;
            string fileName = $"chapter_{idx}.xhtml";
            string chapId = $"chap_{idx}";
            string escapedTitle = WebUtility.HtmlEncode(chap.Title);

            string xhtml = BuildChapterXhtml(chap, contentMode, lang);
            await WriteZipTextAsync(zip, $"OEBPS/{fileName}", xhtml);

            manifestSb.AppendLine($"    <item id=\"{chapId}\" href=\"{fileName}\" media-type=\"application/xhtml+xml\"/>");
            spineSb.AppendLine($"    <itemref idref=\"{chapId}\"/>");
            navListSb.AppendLine($"        <li><a href=\"{fileName}\">{escapedTitle}</a></li>");
            ncxNavSb.AppendLine($"    <navPoint id=\"navPoint-{idx}\" playOrder=\"{idx}\"><navLabel><text>{escapedTitle}</text></navLabel><content src=\"{fileName}\"/></navPoint>");
        }

        // 5. OEBPS/nav.xhtml
        string navXhtml = $$"""
<?xml version="1.0" encoding="utf-8"?>
<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops" lang="{{lang}}">
<head>
  <meta charset="utf-8" />
  <title>{{escapedBookTitle}}</title>
  <link rel="stylesheet" type="text/css" href="style.css" />
</head>
<body>
  <nav epub:type="toc" id="toc">
    <h1>สารบัญ — {{escapedBookTitle}}</h1>
    <ol>
{{navListSb}}    </ol>
  </nav>
</body>
</html>
""";
        await WriteZipTextAsync(zip, "OEBPS/nav.xhtml", navXhtml);

        // 6. OEBPS/toc.ncx
        string tocNcx = $$"""
<?xml version="1.0" encoding="UTF-8"?>
<ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
  <head>
    <meta name="dtb:uid" content="{{bookId}}"/>
    <meta name="dtb:depth" content="1"/>
    <meta name="dtb:totalPageCount" content="0"/>
    <meta name="dtb:maxPageNumber" content="0"/>
  </head>
  <docTitle><text>{{escapedBookTitle}}</text></docTitle>
  <navMap>
{{ncxNavSb}}  </navMap>
</ncx>
""";
        await WriteZipTextAsync(zip, "OEBPS/toc.ncx", tocNcx);

        // 7. OEBPS/content.opf
        string modifiedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        string contentOpf = $$"""
<?xml version="1.0" encoding="UTF-8"?>
<package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="bookid">
  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
    <dc:identifier id="bookid">{{bookId}}</dc:identifier>
    <dc:title>{{escapedBookTitle}}</dc:title>
    <dc:language>{{lang}}</dc:language>
    <dc:creator>NovelTranslator</dc:creator>
    <meta property="dcterms:modified">{{modifiedUtc}}</meta>
  </metadata>
  <manifest>
    <item id="style" href="style.css" media-type="text/css"/>
    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
    <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
{{manifestSb}}  </manifest>
  <spine toc="ncx">
    <itemref idref="nav"/>
{{spineSb}}  </spine>
</package>
""";
        await WriteZipTextAsync(zip, "OEBPS/content.opf", contentOpf);
    }

    private static string BuildChapterBodyHtml(Chapter chapter, ExportContentMode contentMode)
    {
        var origParas = SplitParagraphs(chapter.OriginalText);
        var transParas = SplitParagraphs(chapter.TranslatedText);
        var sb = new StringBuilder();

        if (contentMode == ExportContentMode.Bilingual)
        {
            int count = Math.Max(origParas.Length, transParas.Length);
            for (int i = 0; i < count; i++)
            {
                sb.AppendLine("<div class=\"pair\">");
                if (i < origParas.Length)
                {
                    sb.AppendLine($"  <p class=\"orig-p\">{WebUtility.HtmlEncode(origParas[i])}</p>");
                }
                if (i < transParas.Length)
                {
                    sb.AppendLine($"  <p class=\"trans-p\">{WebUtility.HtmlEncode(transParas[i])}</p>");
                }
                sb.AppendLine("</div>");
            }
        }
        else if (transParas.Length > 0)
        {
            foreach (string p in transParas)
            {
                sb.AppendLine($"<p class=\"trans-p\">{WebUtility.HtmlEncode(p)}</p>");
            }
        }
        else
        {
            sb.AppendLine("<p class=\"untranslated-note\">[ตอนนี้ยังไม่ได้แปล]</p>");
            foreach (string p in origParas)
            {
                sb.AppendLine($"<p class=\"orig-p\">{WebUtility.HtmlEncode(p)}</p>");
            }
        }

        return sb.ToString();
    }

    private static string BuildChapterXhtml(Chapter chapter, ExportContentMode contentMode, string lang)
    {
        string escapedTitle = WebUtility.HtmlEncode(chapter.Title);
        var origParas = SplitParagraphs(chapter.OriginalText);
        var transParas = SplitParagraphs(chapter.TranslatedText);
        var bodySb = new StringBuilder();

        if (contentMode == ExportContentMode.Bilingual)
        {
            int count = Math.Max(origParas.Length, transParas.Length);
            for (int i = 0; i < count; i++)
            {
                if (i < origParas.Length)
                {
                    bodySb.AppendLine($"  <p class=\"orig\">{WebUtility.HtmlEncode(origParas[i])}</p>");
                }
                if (i < transParas.Length)
                {
                    bodySb.AppendLine($"  <p class=\"trans\">{WebUtility.HtmlEncode(transParas[i])}</p>");
                }
            }
        }
        else if (transParas.Length > 0)
        {
            foreach (string p in transParas)
            {
                bodySb.AppendLine($"  <p class=\"trans\">{WebUtility.HtmlEncode(p)}</p>");
            }
        }
        else
        {
            bodySb.AppendLine("  <p class=\"note\">[ตอนนี้ยังไม่ได้แปล]</p>");
            foreach (string p in origParas)
            {
                bodySb.AppendLine($"  <p class=\"orig\">{WebUtility.HtmlEncode(p)}</p>");
            }
        }

        return $$"""
<?xml version="1.0" encoding="utf-8"?>
<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml" lang="{{lang}}">
<head>
  <meta charset="utf-8" />
  <title>{{escapedTitle}}</title>
  <link rel="stylesheet" type="text/css" href="style.css" />
</head>
<body>
  <h1>{{escapedTitle}}</h1>
{{bodySb}}</body>
</html>
""";
    }

    private static string[] SplitParagraphs(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();
    }

    private static async Task WriteZipTextAsync(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        await entryStream.WriteAsync(bytes);
    }
}
