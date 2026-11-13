# Ponytail Audit — Should Fix (v4.6)

## What this repo does
**NovelTranslator (ReadLator)** is a Windows desktop app (.NET 10, Avalonia UI 11, SQLite with FTS5, OpenAI-compatible SSE streaming) for translating foreign web novels into Thai using local or cloud LLMs, with smart chunking, glossary injection, paragraph alignment, and EPUB/TXT/URL import & export.
- **Assumed load**: Single desktop user, 1–50 stories, 100–3,000 chapters per story (~5,000–20,000 chars per chapter), glossaries of 100–2,000 entries, and streaming LLM responses over slow or unstable connections.
- **Graphify state**: 889 nodes, 2,044 edges, 42 communities (`graphify-out/graph.json`, updated at `f73d1f4` v4.6).

---

## Should fix (missing tests on risky code, slowness, dead code)

### 1. `src/NovelTranslator.Core/Translation/TranslationPipeline.cs:26-235` — Core `TranslationPipeline` has zero unit tests
- **What this is**: The central translation orchestrator handling chunk splitting, glossary filtering, streaming token callbacks, 2-attempt validation retry, cross-chapter context carry-over, and partial resume.
- **Problem**: `tests/NovelTranslator.Tests/` tests `SmartChunker`, `OutputValidator`, `GlossaryService`, and `NovelRepository`, but has zero tests for `TranslationPipeline`.
- **Fix**: Add `TranslationPipelineTests.cs` using a fake `ILlmClient` to cover: (a) multi-chunk translation assembly, (b) retry when attempt 1 fails `OutputValidator`, and (c) resuming a partially completed chapter.
- **If we skip it**: Regressions in chunk assembly or retry handling are only caught manually in the GUI.

### 2. `src/NovelTranslator.App/ViewModels/SearchReplaceViewModel.cs:62-84` — Search preview loads all chapters before checking FTS5 scope and undercounts matches
- **What this is**: Searches for text across the current chapter (`ScopeIndex == 0`) or entire story (`ScopeIndex == 1`).
- **Problem**:
  1. Line 64 calls `await _repository.GetChaptersByStoryAsync(_storyId)` unconditionally, loading every chapter's full text into memory before line 66 checks `if (ScopeIndex == 1 && !UseRegex && !MatchCase)` and returns early from `SearchFtsAsync`.
  2. `Chapters_Fts` stores one row per chapter, so `ftsResults.Count` reports the number of *chapters* containing the term rather than the total number of occurrences (`พบทั้งหมด {ftsResults.Count} จุด`), whereas `ApplyReplaceAsync` replaces every occurrence inside each matched chapter.
- **Fix**: Move `GetChaptersByStoryAsync` below the FTS5 check, or use FTS5 to filter matching `ChapterId`s and then scan those matched chapters with `BuildRegex()` so every individual match and accurate count is shown.
- **If we skip it**: Whole-story search still loads the entire story table into RAM on every preview and tells the user "1 match found" when a chapter actually has 20 matches that will be replaced.

### 3. `src/NovelTranslator.App/ViewModels/MainViewModel.cs:811-820` — Fake progress timer clamps total progress to 45%, jumping backwards on chunk 2+
- **What this is**: Smoothly increments `ProgressPercentage` while waiting for the first tokens of a chunk.
- **Problem**: Line 817 computes `Math.Min(45.0, basePercent + (spanPercent * (currentSubProgress / 100.0)))`. Because `45.0` caps the **total** percentage instead of the per-chunk sub-progress, as soon as chunk 2 of 2 starts (`basePercent = 50.0`), the progress bar jumps backwards from `50%` to `45%`.
- **Fix**: Cap only the sub-progress fraction: `ProgressPercentage = Math.Min(99.0, basePercent + (spanPercent * (Math.Min(45.0, currentSubProgress) / 100.0)));`.
- **If we skip it**: The progress bar visibly jumps backwards to 45% on every chunk after the halfway mark.

### 4. `src/NovelTranslator.App/ViewModels/MainViewModel.cs:1519` — Batch `.txt` import sorts filenames asciibetically instead of naturally
- **What this is**: Imports multiple `.txt` files at once (`ImportMultipleTextAsync`).
- **Problem**: `files.OrderBy(f => f)` uses standard string sorting, so `Chapter 10.txt` is imported before `Chapter 2.txt`.
- **Fix**: Sort with natural numeric padding: `files.OrderBy(f => Regex.Replace(Path.GetFileName(f), @"\d+", m => m.Value.PadLeft(10, '0')))`.
- **If we skip it**: Importing a folder of unpadded chapter files (`1.txt`..`20.txt`) inserts chapters in the wrong sequence.

### 5. `src/NovelTranslator.App/ViewModels/MainViewModel.cs:956-972,1011-1038` — Single-paragraph retranslation cannot be cancelled
- **What this is**: `RetranslateParagraphAsync` and `RetranslateParagraphInPlaceAsync`.
- **Problem**: Both pass `cancellationToken: default` to `pipeline.RetranslateParagraphAsync(...)` instead of creating and wiring `_translationCts`.
- **Fix**: Initialize `_translationCts = new CancellationTokenSource()`, pass `_translationCts.Token`, and handle `OperationCanceledException` just like `StartTranslationAsync`.
- **If we skip it**: If a local or cloud LLM hangs during single-paragraph retranslation, the Stop button cannot cancel it.

### 6. `src/NovelTranslator.Core/Glossary/GlossaryExtractor.cs:100` — Greedy bracket regex fails when AI appends bracketed notes
- **What this is**: Extracts the JSON array `[...]` from the LLM response during Auto-Glossary scanning.
- **Problem**: `Regex.Match(jsonText, @"\[.*\]", RegexOptions.Singleline)` greedily matches from the first `[` to the very last `]` in the response. If the LLM adds a trailing note such as `[หมายเหตุ: ...]`, `match.Value` spans across invalid trailing text and `JsonDocument.Parse` throws `JsonException`, which is caught and silently returns an empty list.
- **Fix**: Strip markdown fences first, then find the matching closing `]` for the first `[` by tracking bracket depth outside quotes.
- **If we skip it**: Auto-Glossary scanning silently returns 0 terms whenever the model adds bracketed commentary after the JSON array.

### 7. `src/NovelTranslator.Core/Glossary/GlossaryService.cs:27-37` — `else if (IndexOf)` defeats `\b` word-boundary check in `FilterForChunk`
- **What this is**: Filters story glossary entries to only those appearing in the current source chunk.
- **Problem**: Line 31 checks `Regex.IsMatch(chunkText, pattern, RegexOptions.IgnoreCase)` with `\b...\b`, but when that returns `false` (for example, glossary term `"cat"` tested against `"caterpillar"`), the `else if (chunkText.IndexOf(entry.SourceTerm, StringComparison.OrdinalIgnoreCase) >= 0)` branch at line 35 immediately runs and adds `"cat"` anyway. It also compiles a new `Regex` per glossary term per chunk for nothing.
- **Fix**: Only use `\b` regex when `entry.SourceTerm` starts and ends with ASCII word characters (without falling back to `IndexOf`), and use `IndexOf` directly for CJK/non-ASCII or symbol-bounded terms.
- **If we skip it**: Short English glossary terms match inside unrelated longer words and bloat the LLM system prompt with irrelevant glossary rules.

### 8. `src/NovelTranslator.Core/Data/NovelRepository.cs:330-366`, `src/NovelTranslator.Core/Export/TextExportImportService.cs:49-64`, `src/NovelTranslator.App/ViewLocator.cs:1-38`, `src/NovelTranslator.App/ViewModels/ViewModelBase.cs:1-7` — Dead methods and unused template classes
- **What this is**: Unreferenced methods and scaffold files identified by Graphify and call-graph search.
- **Problem**:
  - `NovelRepository.FindSimilarTranslationAsync` has no production callers (only called in a test).
  - `TextExportImportService.ExportStoryMultipleFilesAsync` has zero callers anywhere.
  - `ViewModelBase` has zero subclasses (all ViewModels inherit `ObservableObject` directly), so `ViewLocator.Match(object? data) => data is ViewModelBase` always returns `false`.
- **Fix**: Delete `ExportStoryMultipleFilesAsync`, `ViewLocator.cs`, `ViewModelBase.cs` (and `<local:ViewLocator />` in `App.axaml`), and remove `FindSimilarTranslationAsync` unless translation-memory reuse is actively planned.
- **If we skip it**: ~100 lines of dead code and reflection trimmer warnings stay in the build.

---

## Verdict
- **8 should-fix items** covering test gaps, search/progress/import edge cases, and dead code cleanup. See [audit-must-fix.md](file:///d:/Work/Other/ReadLator/audit-must-fix.md) and [audit-nice-to-have.md](file:///d:/Work/Other/ReadLator/audit-nice-to-have.md) for the other priority tiers.

## Not checked
- Real-world latency differences across external LLM API providers.
