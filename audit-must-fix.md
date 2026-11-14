# Ponytail Audit — Must Fix (v5.2)

## What this repo does
**NovelTranslator (ReadLator)** is a Windows desktop app (.NET 10, Avalonia UI, SQLite with FTS5, WebView2, OpenAI-compatible SSE streaming) that translates foreign web novels into Thai using local or cloud AI models. It supports smart chunking, glossary injection, paragraph-by-paragraph editing, and importing chapters from `.txt` files, URLs (Reader Mode), or an incognito WebView2 browser.
- **Assumed load**: Single desktop user, 1–50 stories, 100–3,000 chapters per story (~5,000–20,000 characters per chapter), glossaries of 100–2,000 terms, and streaming AI responses over slow or unstable connections.
- **Graphify state**: 1,039 nodes, 2,494 edges, 60 communities (`graphify-out/graph.json`, updated at `8b4e793` v5.2).

---

## Must fix (bugs, security, data loss, breaks at the expected load)

1. **Pending auto-save overwrites restored database or writes back deleted chapters** (`src/NovelTranslator.App/ViewModels/MainViewModel.cs:L668-678`, `L1148-1157`, `L1389-1441`, `L1837-1849`)
   - **What this is:** When the user switches away from a chapter while the 1.5-second auto-save timer (`_autoSaveTimer`) is running, `OnSelectedChapterChanging` flushes the outgoing chapter to SQLite.
   - **Problem:** `RestoreDatabaseAsync`, `DeleteStoryAsync`, and `DeleteChapterAsync` do not stop `_autoSaveTimer` before replacing the database or changing `SelectedChapter`. If a user edits text and immediately restores a backup `.db` (or deletes the chapter/story) within 1.5 seconds, `OnSelectedChapterChanging` fires and writes the old in-memory chapter into the newly restored database.
   - **Fix:** Call `_autoSaveTimer.Stop();` at the start of `RestoreDatabaseAsync`, `DeleteStoryAsync`, and `DeleteChapterAsync` before modifying the database or changing `SelectedChapter`.
   - **If we skip it:** Restoring a backup right after editing a chapter silently overwrites the restored chapter with stale text from before the restore.

2. **URL import SSRF guard can be bypassed via HTTP redirect or WebView2 fallback** (`src/NovelTranslator.Core/Export/WebChapterImportService.cs:L21-31`, `src/NovelTranslator.App/ViewModels/MainViewModel.cs:L1693-1699`, `src/NovelTranslator.App/Controls/WebView2Host.cs:L83-98`)
   - **What this is:** Blocks `localhost` and private-network URLs when fetching chapters from the web (`IsBlockedUri`) and falls back to the embedded WebView2 browser when a site blocks bots.
   - **Problem:** `WebChapterImportService` uses a default `HttpClient` that automatically follows HTTP `301`/`302` redirects without re-checking `IsBlockedUri` on the redirected target, and `WebView2Host.Navigate` does not call `IsBlockedUri` at all. In addition, `ImportFromUrlAsync` catches all `InvalidOperationException` errors—so a URL rejected for exceeding 5 MB or having a binary content type (`application/octet-stream`) is automatically opened in WebView2 anyway.
   - **Fix:** Make `WebChapterImportService.IsBlockedUri` `public` and enforce it in `WebView2Host.Navigate` (and on `response.RequestMessage?.RequestUri` after redirects), and only fall back to WebView2 when extraction fails due to empty/JS-rendered HTML or HTTP status errors—not when the file size or media-type check fails.
   - **If we skip it:** A public URL that redirects to `http://localhost:11434` or a huge binary download bypasses the URL safety checks and opens in HttpClient or WebView2.

3. **Closing the WebView2 browser window during startup leaks the controller or crashes** (`src/NovelTranslator.App/Controls/WebView2Host.cs:L28-39`, `L48-81`)
   - **What this is:** Initializes the native Microsoft Edge WebView2 control asynchronously when `BrowserImportWindow` opens.
   - **Problem:** `InitializeAsync` awaits `CoreWebView2Environment.CreateAsync` and `env.CreateCoreWebView2ControllerAsync(_hostHandle.Handle, options)`. If the user closes `BrowserImportWindow` while those awaits are still running, `DestroyNativeControlCore` sets `_hostHandle = null` first; when the await resumes, `_hostHandle.Handle` throws a `NullReferenceException` or creates a `CoreWebView2Controller` that is never closed and leaves its temp user-data folder behind.
   - **Fix:** Capture `IntPtr handle = _hostHandle.Handle;` before the first `await`, and after `CreateCoreWebView2ControllerAsync` returns, check `if (_hostHandle == null)`—if true, immediately call `controller.Close()` and `CleanupTempUserData()` instead of assigning `_controller`.
   - **If we skip it:** Closing the browser import window while it is still loading throws an unhandled exception or leaves background `msedgewebview2.exe` processes and temp directories running.

4. **Opening Search & Replace within 1.5s of typing loses unsaved edits on whole-story replace** (`src/NovelTranslator.App/ViewModels/MainViewModel.cs:L1808-1823`, `src/NovelTranslator.App/ViewModels/SearchReplaceViewModel.cs:L114-144`)
   - **What this is:** Opens the Search & Replace dialog (`Ctrl+F`) and reloads `SelectedChapter` when the dialog closes.
   - **Problem:** `OpenSearchReplaceAsync` does not flush pending auto-save edits before opening the dialog, so running a whole-story replace (`ScopeIndex == 1`) reads the older chapter text from SQLite and overwrites the user's recent typing. Then on close (`L1819`), `TranslatedText = refreshed.TranslatedText` runs without `_isLoadingChapter = true`, which marks the already-saved chapter as `"ยังไม่ได้บันทึก"` (unsaved) and does not refresh `Editor.ParagraphPairs` if Paragraph Mode is active.
   - **Fix:** Flush `_autoSaveTimer` via `await SaveCurrentChapterInternalAsync(isAutoSave: true)` before `RequestOpenSearchReplace`, and reload `refreshed.TranslatedText` using `OnSelectedChapterChanged(refreshed)` (or under `_isLoadingChapter = true` + `Editor.SyncParagraphPairs`).
   - **If we skip it:** Pressing `Ctrl+F` right after editing a paragraph and replacing across the story wipes out the last 1.5 seconds of edits.

---

## Verdict
- **4 must-fix items** (auto-save race on restore/delete, URL redirect/fallback guard, WebView2 close-during-init race, and Search & Replace unsaved-edit flush). See [audit-should-fix.md](file:///d:/Work/Other/ReadLator/audit-should-fix.md) (items 5–10) and [audit-nice-to-have.md](file:///d:/Work/Other/ReadLator/audit-nice-to-have.md) (items 11–14).

## Lean
- `Lean: -350 lines, -0 dependencies possible` across all three files.

## Not checked
- Live end-to-end LLM provider responses (Ollama/LM Studio/OpenRouter) and actual Velopack/Inno Setup installer execution on a clean Windows VM.
