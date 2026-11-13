# Ponytail Audit — Must Fix (v4.6)

## What this repo does
**NovelTranslator (ReadLator)** is a Windows desktop app (.NET 10, Avalonia UI 11, SQLite with FTS5, OpenAI-compatible SSE streaming) for translating foreign web novels into Thai using local or cloud LLMs, with smart chunking, glossary injection, paragraph alignment, and EPUB/TXT/URL import & export.
- **Assumed load**: Single desktop user, 1–50 stories, 100–3,000 chapters per story (~5,000–20,000 chars per chapter), glossaries of 100–2,000 entries, and streaming LLM responses over slow or unstable connections.
- **Graphify state**: 889 nodes, 2,044 edges, 42 communities (`graphify-out/graph.json`, updated at `f73d1f4` v4.6).

---

## Must fix (bugs, security, or breaks at normal load)

### 1. `src/NovelTranslator.Core/Export/WebChapterImportService.cs:41-48` — Unbounded response download and unrestricted URL targets in URL Chapter Import
- **What this is**: Downloads a web page from a user-supplied URL (`ImportFromUrlAsync`) to extract the chapter title and body text.
- **Problem**: `_httpClient.GetAsync(uri, cancellationToken)` buffers the entire HTTP response body in memory before checking size or content type, and allows loopback/private-network targets (e.g., `http://localhost:11434` or a multi-gigabyte binary/stream URL). Pasting a link to a large file or endless stream freezes the UI or crashes the process with `OutOfMemoryException`.
- **Fix**:
  1. Call `_httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)`.
  2. Reject non-text/HTML `Content-Type` headers and `ContentLength > 5 * 1024 * 1024` (5 MB) before reading the response stream, and cap stream reads at 5 MB.
- **If we skip it**: Pasting a non-HTML or large URL stalls the app and exhausts memory.

### 2. `src/NovelTranslator.Core/Translation/TranslationPipeline.cs:65-76,208-220` — Out-of-order chunk assembly when resuming with an incomplete early chunk
- **What this is**: Resumes a partially translated chapter while preserving manual user edits to `chapter.TranslatedText`.
- **Problem**: At line 65, `preserveExistingTranslation` is `true` whenever `completedCount > 0 && !string.IsNullOrWhiteSpace(chapter.TranslatedText)`. When `true`, `appendedParts` is initialized with the full existing `chapter.TranslatedText`, and lines 209–215 append every newly translated chunk to the end of `appendedParts`. If chunk 1 was edited or reset to incomplete at the top of the chapter while chunk 2 remained completed (`completedCount > 0`), translating chunk 1 appends chunk 1's translation **after** chunk 2's translation instead of placing it in chunk 1's position.
- **Fix**: Only enable the `preserveExistingTranslation` append shortcut when completed chunks form a contiguous prefix (`bool isContiguousPrefix = chunks.Take(completedCount).All(c => c.IsCompleted);`), or rebuild `chapter.TranslatedText` in chunk index order (`chunks.OrderBy(c => c.ChunkIndex)`).
- **If we skip it**: Editing source text near the top of a partially translated chapter and resuming appends the new opening paragraphs at the very bottom of the chapter.

### 3. `src/NovelTranslator.Core/Data/NovelDatabase.cs:152-158` & `src/NovelTranslator.App/ViewModels/MainViewModel.cs:1662-1675` — Database restore does not clear SQLite connection pools or re-run schema migration
- **What this is**: Restores a `.db` backup file over the active `novels.db` SQLite database (`RestoreFrom`).
- **Problem**: `novels.db` runs with `Journal Mode=WAL` and `Microsoft.Data.Sqlite` connection pooling enabled. `RestoreFrom` overwrites the database via `BackupDatabase`, but neither clears pooled connections nor calls `_database.Initialize()`. Pooled connections can hold stale WAL state, and restoring a backup created before v4.1 (before `Chapters_Fts` was added) will throw `SQLite Error 1: no such table: Chapters_Fts` on the first global search.
- **Fix**: At the end of `NovelDatabase.RestoreFrom(string backupFilePath)` (after closing `source` and `destination`), call `SqliteConnection.ClearAllPools();` followed by `Initialize();`.
- **If we skip it**: Restoring an older backup crashes FTS5 search or serves stale cached pages until the user restarts the app.

### 4. `src/NovelTranslator.Core/Glossary/GlossaryService.cs:71-84` — Glossary CSV import corrupts entries with multi-line notes
- **What this is**: Imports glossary terms from a `.csv` file (`ImportFromCsv`) exported by `ExportToCsv`.
- **Problem**: `ExportToCsv` (`L116-119`) properly wraps `Notes` containing newlines in double quotes, but `ImportFromCsv` (`L71`) reads the file with `File.ReadAllLines(filePath, Encoding.UTF8)`. Any glossary entry whose `Notes` field contains a newline is split across multiple strings before `ParseCsvLine` runs, corrupting that entry and dropping or misaligning the remaining lines.
- **Fix**: Read the file text with `File.ReadAllText(filePath, Encoding.UTF8)` and split records only on newlines where `inQuotes == false` (or parse rows and fields in a single character loop).
- **If we skip it**: Exporting a glossary with multi-line notes and re-importing it silently corrupts the user's glossary.

---

## Lean (what stays as-is)
- Single-project `NovelTranslator.Core` + `NovelTranslator.App` split, raw `Microsoft.Data.Sqlite` queries in `NovelRepository`, atomic `.tmp` settings save in `AppSettings.Save()`, and custom SSE streaming in `LlmClient` are lean, fast, and already well-suited to the app's scale.

## Verdict
- **4 must-fix items** before shipping to users. See [audit-should-fix.md](file:///d:/Work/Other/ReadLator/audit-should-fix.md) and [audit-nice-to-have.md](file:///d:/Work/Other/ReadLator/audit-nice-to-have.md) for the remaining tiers.

## Not checked
- Live end-to-end LLM provider responses (Ollama/LM Studio/OpenRouter) and Windows installer packaging.
