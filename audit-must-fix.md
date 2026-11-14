# Ponytail Audit — Must Fix (v5.5)

> **Scope:** Bugs, data loss, and security issues that can break real users right now.
> **Target:** Agent-actionable specification. All 14 findings from the previous v5.2 audit were verified resolved in `8820684` (v5.3), `b036da6` (v5.4), and `08ccd22` (v5.5). Each item below is numbered in the order it should be fixed.

---

## Summary Table

| # | Area | File | Core Issue |
|---|---|---|---|
| **1** | Smart Chunk Reuse | `src/NovelTranslator.Core/Translation/TranslationPipeline.cs` | Editing or appending to the last chunk of an already-translated chapter duplicates the old translation when resuming (`preserveExistingTranslation` uses stale `chapter.TranslatedText`) |
| **2** | Auto-Save on Exit | `src/NovelTranslator.App/Views/MainWindow.axaml.cs` / `MainViewModel.cs` | Closing the main window within 1.5s of typing exits without flushing `_autoSaveTimer`, losing recent edits |
| **3** | Paragraph Mode UI Sync | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | Translating a chapter while Paragraph Mode is active never syncs `Editor.ParagraphPairs` on completion or cancel |
| **4** | WebView2 In-Page Redirects | `src/NovelTranslator.App/Views/BrowserImportWindow.axaml.cs` | Clicking links or JS/HTTP redirects inside `BrowserImportWindow` bypass `WebView2Host.Navigate`'s SSRF and scheme filter |

---

## 1. Editing or appending source text in an already-translated chapter duplicates the old translation when resuming (`preserveExistingTranslation` bug)
**Location:**
- [TranslationPipeline.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/TranslationPipeline.cs#L56-L77)
- [TranslationPipeline.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/TranslationPipeline.cs#L214-L225)

- **What this is:** Smart chunk reuse (`forceRetranslate: false`, button **แปลตอนนี้**) that reuses unchanged completed chunks when a user appends or edits paragraphs in `OriginalText`.
- **Problem:** Suppose a chapter has 2 chunks (`chapter.TranslatedText` holds `Chunk 1 + old Chunk 2`) and the user edits or appends text in Chunk 2. At line 56, `needsResplit` is `true`, so `SaveChunksAsync` marks Chunk 1 as completed (`completedCount = 1`) and Chunk 2 as incomplete (`IsCompleted = false`). However, at line 67:
  ```csharp
  bool preserveExistingTranslation = !forceRetranslate && completedCount > 0 && isContiguousPrefix && !string.IsNullOrWhiteSpace(chapter.TranslatedText);
  var appendedParts = preserveExistingTranslation
      ? new List<string> { chapter.TranslatedText.TrimEnd() }
      : chunks.Where(c => c.IsCompleted && !string.IsNullOrWhiteSpace(c.TranslatedText))
              .Select(c => c.TranslatedText.TrimEnd())
              .ToList();
  ```
  Because `completedCount > 0 && isContiguousPrefix` is still true, `preserveExistingTranslation` evaluates to `true` even though `needsResplit` was `true` and Chunk 2 was invalidated. `appendedParts` starts with the **entire old `chapter.TranslatedText` (`Chunk 1 + old Chunk 2`)** and then appends the newly translated Chunk 2 at the end—duplicating Chunk 2.
- **Fix:** Include `!needsResplit` in `preserveExistingTranslation`:
  ```csharp
  bool preserveExistingTranslation = !forceRetranslate && !needsResplit && completedCount > 0 && isContiguousPrefix && !string.IsNullOrWhiteSpace(chapter.TranslatedText);
  ```
  When `needsResplit` is true, `appendedParts` will automatically initialize from the still-valid completed chunks (`chunks.Where(c => c.IsCompleted ...)`). Add a unit test in [NovelTranslatorTests.cs](file:///d:/Work/Other/ReadLator/tests/NovelTranslator.Tests/NovelTranslatorTests.cs) verifying that modifying Chunk 2 of a 2-chunk translated chapter replaces Chunk 2 instead of duplicating it.
- **If we skip it:** Editing or adding paragraphs to the end of an already-translated multi-chunk chapter and clicking **แปลตอนนี้** duplicates the modified chunk in `TranslatedText`.

---

## 2. Closing the main window within 1.5 seconds of typing loses unsaved chapter edits
**Location:**
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L52-L65)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L355-L365)

- **What this is:** Debounced auto-save (`_autoSaveTimer`, 1.5 seconds by default and configurable in Settings) triggered when editing `OriginalText`, `TranslatedText`, or `ChapterTitleEdit`.
- **Problem:** `MainWindow` has no `Closing` event handler to flush `_autoSaveTimer`. While switching chapters or opening Search & Replace flushes pending edits, closing the app window (`Alt+F4` or clicking `✕`) while `_autoSaveTimer.IsEnabled` is true exits immediately before the timer ticks.
- **Fix:**
  1. Add a synchronous or fast flush method on `MainViewModel` (e.g., `FlushPendingSaveAsync()` or `FlushPendingSave()`): if `_autoSaveTimer.IsEnabled`, stop `_autoSaveTimer` and save `SelectedChapter` via `_repository.SaveChapterAsync(SelectedChapter)`.
  2. Override `OnClosing(WindowClosingEventArgs e)` (or hook `Closing`) in [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs) to flush pending edits before the window closes.
- **If we skip it:** Any typing or paste made right before closing the window is silently lost.

---

## 3. Translating a chapter while in Paragraph Mode leaves the Paragraph Mode UI blank or stale after completion
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L758-L774)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L953-L976)

- **What this is:** Translating a chapter (`StartTranslationCommand` / `RetranslateAllCommand`) while `Editor.IsParagraphMode` (`จับคู่รายย่อหน้า`) is enabled.
- **Problem:** `OnTranslatedTextChanged` skips `Editor.SyncParagraphPairs(OriginalText, value)` whenever `IsTranslating` is `true` (to avoid rebuilding the paragraph list on every streaming token). At line 953 of `StartTranslationInternalAsync`:
  ```csharp
  TranslatedText = SelectedChapter.TranslatedText ?? string.Empty;
  ```
  `IsTranslating` is still `true` (it is only set to `false` in the `finally` block at line 973), and if streaming already set `TranslatedText` to the identical final string, the property setter doesn't even fire `OnTranslatedTextChanged`. `StartTranslationInternalAsync` never calls `Editor.SyncParagraphPairs(OriginalText, TranslatedText)` when translation finishes or is cancelled.
- **Fix:** In `StartTranslationInternalAsync`'s `finally` block (after `IsTranslating = false;` and `_isLoadingChapter = false;`), add:
  ```csharp
  if (Editor.IsParagraphMode)
      Editor.SyncParagraphPairs(OriginalText, TranslatedText);
  ```
- **If we skip it:** Users who click **แปลตอนนี้** while in Paragraph Mode see empty or outdated paragraph boxes even after the status bar reports `"แปลเสร็จสิ้น"`, until they manually toggle Paragraph Mode off and back on.

---

## 4. In-page redirects and clicked links inside `BrowserImportWindow` bypass `WebView2Host.Navigate`'s SSRF/scheme check
**Location:**
- [BrowserImportWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/BrowserImportWindow.axaml.cs#L61-L65)
- [WebView2Host.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Controls/WebView2Host.cs#L82-L91)

- **What this is:** The embedded WebView2 fallback window used to import chapters from bot-protected websites.
- **Problem:** `WebView2Host.Navigate(string url)` blocks non-HTTP(S) schemes and private/loopback hosts (`WebChapterImportService.IsBlockedUri(uri)`), and `WebChapterImportService` checks HTTP redirects in `HttpClient`. However, once a public page loads inside `BrowserImportWindow`, any JavaScript redirect (`window.location = ...`), meta-refresh, HTTP 30x redirect inside WebView2, or user link click goes through `CoreWebView2.NavigationStarting` without calling `WebView2Host.Navigate`. Currently line 61 ignores the event args:
  ```csharp
  core.NavigationStarting += (_, _) => StatusText.Text = "กำลังโหลดหน้าเว็บ...";
  ```
- **Fix:** Inspect `args.Uri` inside `core.NavigationStarting`:
  ```csharp
  core.NavigationStarting += (_, args) =>
  {
      if (args.Uri != "about:blank" &&
          (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) ||
           (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
           WebChapterImportService.IsBlockedUri(uri)))
      {
          args.Cancel = true;
          StatusText.Text = "บล็อกการนำทางไปยังที่อยู่ภายในหรือโปรโตคอลที่ไม่ปลอดภัย";
          return;
      }
      StatusText.Text = "กำลังโหลดหน้าเว็บ...";
  };
  ```
- **If we skip it:** A malicious external page opened in the WebView2 import window can redirect the embedded browser to `http://localhost:11434`, LAN admin pages, or `file://` URIs.
