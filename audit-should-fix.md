# Ponytail Audit — Should Fix (v5.8)

> **Scope:** Data preservation on chunk append, duplicate glossary prevention on CSV/AI import, and UI state synchronization.
> **Target:** Agent-actionable specification. Numbered 5–8 (continuing from `audit-must-fix.md` items 1–4).

---

## Summary Table

| # | Area | File | Core Issue |
|---|---|---|---|
| **5** | Edited Translation Preservation on Append | `src/NovelTranslator.Core/Translation/TranslationPipeline.cs` | Appending new paragraphs to a chapter whose translation was manually edited or Search-and-Replaced overwrites the edited translation with raw `ChapterChunks` |
| **6** | Glossary CSV & AI Deduplication | `src/NovelTranslator.App/ViewModels/GlossaryViewModel.cs` / `GlossaryExtractor.cs` | `ImportCsvAsync` inserts duplicate rows if terms already exist, and `ParseAndFilterResults` allows duplicate terms within the same LLM JSON response |
| **7** | Glossary Window Refresh | `src/NovelTranslator.App/Views/MainWindow.axaml.cs` | Closing `GlossaryWindow` (from toolbar or text-selection popup) never refreshes `Editor.ActiveGlossary` or Paragraph Mode's `MatchedGlossary` |
| **8** | `.txt` Starter Chapter Import Sync | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | Overwriting the starter chapter in `ImportTxtFilesAsync` does not stop `_autoSaveTimer` or sync `Editor.ParagraphPairs` in Paragraph Mode |

---

## 5. Appending new paragraphs to a chapter whose translation was manually edited or Search-and-Replaced overwrites the edited translation with raw `ChapterChunks`
**Location:**
- [TranslationPipeline.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/TranslationPipeline.cs#L52-L77)

- **What this is:** Manually editing `TranslatedText` (or running Search & Replace) on a translated chapter, then appending new source paragraphs at the end of `OriginalText` and clicking **แปลตอนนี้** (`forceRetranslate: false`).
- **Problem:** When the user edits `TranslatedText` in the editor or via Search & Replace, `Chapters.TranslatedText` is updated in SQLite, while `ChapterChunks.TranslatedText` still holds the unedited raw LLM output per chunk. If the user later appends new paragraphs to the end of `OriginalText` and clicks **แปลตอนนี้**, `needsResplit` is `true` (`chunks.Count != splitTexts.Count`). Because `preserveExistingTranslation` (`L67`) requires `!needsResplit`, it becomes `false` even when all previously existing chunks (`0..oldCount-1`) are still intact as an unchanged completed prefix. Line 76 then overwrites `chapter.TranslatedText` from `chunks.Select(c => c.TranslatedText)`, wiping out the user's manual edits in the first part of the chapter!
- **Fix:** Before calling `SaveChunksAsync` when `needsResplit` is true, check whether the existing chunks were all completed and form an exact prefix of `splitTexts` (`bool isPureAppend = chunks.Count > 0 && chunks.Count < splitTexts.Count && chunks.All(c => c.IsCompleted && !string.IsNullOrEmpty(c.TranslatedText)) && chunks.Select(c => c.SourceText).SequenceEqual(splitTexts.Take(chunks.Count));`). Allow `preserveExistingTranslation` when `(!needsResplit || isPureAppend)`.
- **If we skip it:** Polishing a translation and later appending more paragraphs to the end of the chapter reverts the polished translation back to the raw unedited chunk text.

---

## 6. `GlossaryViewModel.ImportCsvAsync` and `GlossaryExtractor.ParseAndFilterResults` allow duplicate `SourceTerm` entries
**Location:**
- [GlossaryViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/GlossaryViewModel.cs#L170-L176)
- [GlossaryExtractor.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Glossary/GlossaryExtractor.cs#L125-L153)

- **What this is:** Importing a Glossary CSV file (`นำเข้า CSV`) or extracting new terms via AI (`Auto-Glossary`).
- **Problem:**
  1. `GlossaryViewModel.ImportCsvAsync` calls `_repository.BulkInsertGlossaryEntriesAsync(imported)` directly without checking `_allEntries` or deduplicating within the CSV. If a user exports `glossary.csv`, edits it, and imports it back, every existing term is duplicated in SQLite.
  2. In `GlossaryExtractor.ParseAndFilterResults` (`L138`), `existingLowerTerms.Contains(source.ToLowerInvariant())` checks against existing DB entries, but never adds newly accepted terms into `existingLowerTerms`. If the LLM outputs the same character name twice in one JSON array, both duplicates are returned.
- **Fix:**
  1. In `GlossaryExtractor.ParseAndFilterResults` (`L138`), change `if (existingLowerTerms.Contains(source.ToLowerInvariant())) continue;` to `if (!existingLowerTerms.Add(source.ToLowerInvariant())) continue;`.
  2. In `GlossaryViewModel.ImportCsvAsync`, match imported rows against `_allEntries` by `SourceTerm` (`StringComparison.OrdinalIgnoreCase`): update existing rows via `UpdateGlossaryEntryAsync` and only pass genuinely new, distinct terms to `BulkInsertGlossaryEntriesAsync`.
- **If we skip it:** Re-importing a CSV or receiving repeated terms from the LLM creates duplicate glossary rules.

---

## 7. Closing `GlossaryWindow` (from toolbar or text-selection popup) never refreshes `Editor.ActiveGlossary` or Paragraph Mode's `MatchedGlossary`
**Location:**
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L110-L115)
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L714-L735)

- **What this is:** Adding, editing, or importing glossary terms via **พจนานุกรมคำศัพท์ (Glossary)** (`RequestOpenGlossary`) or the floating **เพิ่มเข้า Glossary** selection button (`OnAddSelectedToGlossaryClicked`).
- **Problem:** When `GlossaryWindow` closes after `await win.ShowDialog(this)`, neither handler refreshes `ViewModel.Editor.ActiveGlossary` or re-syncs `ViewModel.Editor.ParagraphPairs` when Paragraph Mode is active.
- **Fix:** After `await win.ShowDialog(this)` in both `RequestOpenGlossary` and `OnAddSelectedToGlossaryClicked`, call:
  ```csharp
  await ViewModel.Editor.RefreshActiveGlossaryAsync(story.Id, ViewModel.OriginalText);
  if (ViewModel.Editor.IsParagraphMode)
      ViewModel.Editor.SyncParagraphPairs(ViewModel.OriginalText, ViewModel.TranslatedText);
  ```
- **If we skip it:** Terms added via the Glossary window or selection popup are not reflected in `Editor.ActiveGlossary` or `ParagraphPairViewModel.MatchedGlossary` until switching chapters.

---

## 8. Overwriting the starter chapter in `ImportTxtFilesAsync` does not stop `_autoSaveTimer` or sync `Editor.ParagraphPairs`
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1699-L1740)

- **What this is:** Importing `.txt` files into a brand-new story (`ImportTxtFilesAsync`) when Chapter 1 is still the starter placeholder.
- **Problem:** When `ReferenceEquals(SelectedChapter, starter)` is true (`L1709`), `ImportTxtFilesAsync` assigns `ChapterTitleEdit` and `OriginalText` under `_isLoadingChapter = true`, and at line 1738 `SelectedChapter = firstImported` is a no-op because `SelectedChapter` is already `starter`. Consequently, `OnSelectedChapterChanged` does not fire: `_autoSaveTimer` is not stopped beforehand, `Editor.SyncParagraphPairs` is not called if `Editor.IsParagraphMode` is true, and `Editor.RefreshActiveGlossaryAsync` is not called.
- **Fix:** Call `_autoSaveTimer.Stop();` before overwriting `starter`, and inside the `if (ReferenceEquals(SelectedChapter, starter))` block, call `if (Editor.IsParagraphMode) Editor.SyncParagraphPairs(OriginalText, TranslatedText);` and `_ = Editor.RefreshActiveGlossaryAsync(SelectedStory.Id, OriginalText);`.
- **If we skip it:** Importing `.txt` files into a new story while in Paragraph Mode leaves the `"Paste your English story here..."` placeholder displayed in Paragraph Mode for Chapter 1.
