# Ponytail Audit — Should Fix (v6.8)

> **Scope:** Duplicate glossary entries, dead code deletion (`-310` lines), regex hot-loop speed, false-positive English ratio validation, and UI state synchronization.
> **Target:** Agent-actionable specification. Numbered 8–13 (continuing from `audit-must-fix.md` items 1–7).

---

## Summary Table

| # | Area | File | Core Issue |
|---|---|---|---|
| **8** | Glossary CSV Import Deduplication | `src/NovelTranslator.App/ViewModels/GlossaryViewModel.cs` | `ImportCsvAsync` calls `BulkInsertGlossaryEntriesAsync` directly without checking `_allEntries` or duplicates within the CSV, duplicating existing terms on re-import |
| **9** | Dead Dialog & Unused Commands (`-310` lines) | `AutoGlossary*`, `MainWindow.axaml.cs`, `MainViewModel.cs`, `SettingsViewModel.cs` | Unreachable `AutoGlossaryDialog` / `AutoGlossaryViewModel` / `RequestOpenAutoGlossary` plus unused `RetranslateParagraphAsync(string)`, `MoveChapterUpAsync`, `MoveChapterDownAsync`, and `SelectCategory` |
| **10** | Glossary Matching Hot Loop | `src/NovelTranslator.Core/Glossary/GlossaryService.cs` | `FindMatchingEntries` compiles/evaluates a `\b...\b` `Regex.IsMatch` for every glossary term on every paragraph without a fast substring pre-check |
| **11** | OutputValidator Thai Glossary Inflation | `src/NovelTranslator.Core/Translation/OutputValidator.cs` | `Validate` strips Thai `TargetTerm` strings before computing `latinRatio`, shrinking `nonWhitespaceCount` and causing false English-leftover retries |
| **12** | Glossary Window Refresh | `src/NovelTranslator.App/Views/MainWindow.axaml.cs` | Closing `GlossaryWindow` from the selection popup never refreshes `Editor.ActiveGlossary`, and neither handler re-syncs `Editor.ParagraphPairs` in Paragraph Mode |
| **13** | Starter Chapter Import Sync | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | Overwriting starter Chapter 1 in `ImportTxtFilesAsync` skips stopping `_autoSaveTimer`, word count update, and Paragraph Mode sync; `ImportWebChapterInternalAsync` can overwrite a translated chapter if `OriginalText` is empty |

---

## 8. `GlossaryViewModel.ImportCsvAsync` duplicates every existing `SourceTerm` on CSV re-import
**Location:**
- [GlossaryViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/GlossaryViewModel.cs#L176-L182)

- **What this is:** Importing a Glossary CSV file (**"นำเข้า CSV"**).
- **Problem:** `GlossaryViewModel.ImportCsvAsync` passes all parsed rows directly to `_repository.BulkInsertGlossaryEntriesAsync(imported)` without checking `_allEntries` or deduplicating within the CSV. If a user exports `glossary.csv`, edits a couple of terms, and imports it back, every existing term in that story is duplicated in SQLite.
- **Fix:** Match imported rows against `_allEntries` by `SourceTerm` (`StringComparison.OrdinalIgnoreCase`, deduplicating within the CSV first): update existing rows via `_repository.UpdateGlossaryEntryAsync` and only pass genuinely new, distinct entries to `BulkInsertGlossaryEntriesAsync`.
- **If we skip it:** Re-importing an updated CSV creates duplicate glossary rules in SQLite and bloats LLM prompts.

---

## 9. Delete 310 lines of unreachable Auto-Glossary dialog and unused commands in `NovelTranslator.App`
**Location:**
- [AutoGlossaryViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/AutoGlossaryViewModel.cs#L1-L91)
- [AutoGlossaryDialog.axaml](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/AutoGlossaryDialog.axaml#L1-L53)
- [AutoGlossaryDialog.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/AutoGlossaryDialog.axaml.cs#L1-L17)
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L120-L175)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L332)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1107-L1150)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1673-L1713)
- [SettingsViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/SettingsViewModel.cs#L418-L425)

- **What this is:** Legacy dialog UI and `[RelayCommand]` methods replaced by auto-save glossary extraction (v6.7), in-place paragraph retranslation (`RetranslateParagraphInPlaceAsync`), drag-and-drop chapter reordering (`ReorderChaptersAsync`), and ListBox category selection.
- **Problem:** Grep across `src/` and `tests/` confirms zero callers for:
  1. `RequestOpenAutoGlossary`, `AutoGlossaryViewModel`, and `AutoGlossaryDialog` (218 lines; `ScanNewTermsAsync` now calls `AutoExtractAndSaveGlossaryAsync` directly).
  2. `MainViewModel.RetranslateParagraphAsync(string)` (`L1107-1150`, 44 lines).
  3. `MainViewModel.MoveChapterUpAsync()` and `MoveChapterDownAsync()` (`L1673-1713`, 40 lines).
  4. `SettingsViewModel.SelectCategory(string)` (`L418-425`, 8 lines).
- **Fix:** Delete `AutoGlossaryViewModel.cs`, `AutoGlossaryDialog.axaml`, `AutoGlossaryDialog.axaml.cs`, `RequestOpenAutoGlossary` (property + `MainWindow.axaml.cs` handler), `RetranslateParagraphAsync(string)`, `MoveChapterUpAsync()`, `MoveChapterDownAsync()`, and `SelectCategory(string)`.
- **If we skip it:** 310 lines of dead code and unused generated `RelayCommand` properties remain in the codebase.

---

## 10. `GlossaryService.FindMatchingEntries` builds and evaluates a regex for every glossary term on every paragraph
**Location:**
- [GlossaryService.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Glossary/GlossaryService.cs#L16-L43)
- [EditorViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/EditorViewModel.cs#L89-L96)

- **What this is:** Filtering which glossary entries appear in a chunk or paragraph.
- **Problem:** For every ASCII term, `FindMatchingEntries` allocates `"\b" + Regex.Escape(term) + "\b"` and executes `Regex.IsMatch` even when the term does not appear anywhere in `chunkText`. In Paragraph Mode, `EditorViewModel.SyncParagraphPairs` calls `FindMatchingEntries` for every paragraph on the UI thread (e.g., 100 paragraphs × 500 glossary terms = 50,000 regex evaluations per chapter load or mode toggle).
- **Fix:** Add an ordinal case-insensitive substring pre-check before the `useWordBoundary` check in `GlossaryService.FindMatchingEntries`:
  ```csharp
  if (!chunkText.Contains(term, StringComparison.OrdinalIgnoreCase)) continue;
  ```
- **If we skip it:** Opening large chapters or toggling Paragraph Mode with hundreds of glossary entries causes avoidable UI lag and allocations.

---

## 11. `OutputValidator.Validate` removes Thai `TargetTerm` text before measuring the leftover English ratio
**Location:**
- [OutputValidator.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/OutputValidator.cs#L168-L199)

- **What this is:** Checking `MaxEnglishRatioPercent` to detect chunks where the LLM failed to translate English into Thai.
- **Problem:** Lines 173–179 call `textForRatio = textForRatio.Replace(g.TargetTerm, "");` for every entry in `activeGlossary`—even when `TargetTerm` is pure Thai (e.g. `"อาเธอร์"`). Removing Thai characters shrinks `nonWhitespaceCount` while leaving non-glossary English tokens intact, artificially inflating `latinRatio` on short chunks with many glossary terms and triggering false validation failures/retries.
- **Fix:** Only exclude `g.TargetTerm` from `textForRatio` when `g.TargetTerm.Any(char.IsAsciiLetter)` is true. Add a unit test in `OutputValidatorTests.cs` covering a short translated chunk with Thai glossary names and a small allowed English token.
- **If we skip it:** Short dialogue chunks with multiple glossary character names can fail validation and waste an unnecessary AI retry request.

---

## 12. Closing `GlossaryWindow` from the text-selection popup or in Paragraph Mode leaves `ActiveGlossary` and `MatchedGlossary` stale
**Location:**
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L112-L118)
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L1059-L1080)

- **What this is:** Adding, editing, or importing glossary terms via **พจนานุกรมคำศัพท์ (Glossary)** (`RequestOpenGlossary`) or the floating **"เพิ่มเข้า Glossary"** selection popup (`OnAddSelectedToGlossaryClicked`).
- **Problem:** `OnAddSelectedToGlossaryClicked` never calls `ViewModel.Editor.RefreshActiveGlossaryAsync` after `await win.ShowDialog(this)`, and neither handler calls `ViewModel.Editor.SyncParagraphPairs` when Paragraph Mode is active.
- **Fix:** After `await win.ShowDialog(this)` in both `RequestOpenGlossary` and `OnAddSelectedToGlossaryClicked`, run:
  ```csharp
  await ViewModel.Editor.RefreshActiveGlossaryAsync(story.Id, ViewModel.OriginalText);
  if (ViewModel.Editor.IsParagraphMode)
      ViewModel.Editor.SyncParagraphPairs(ViewModel.OriginalText, ViewModel.TranslatedText);
  ```
- **If we skip it:** Terms added via the selection popup or Glossary window are missing from `Editor.ActiveGlossary` and `ParagraphPairViewModel.MatchedGlossary` until switching chapters.

---

## 13. Overwriting the starter chapter in `ImportTxtFilesAsync` and `ImportWebChapterInternalAsync` misses timer, UI, and state sync
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1778-L1818)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1935-L1948)

- **What this is:** Importing `.txt` files or web chapters into a brand-new story whose Chapter 1 is still the starter placeholder.
- **Problem:**
  1. In `ImportTxtFilesAsync`, when `ReferenceEquals(SelectedChapter, starter)` (`L1788`), `OriginalText` is set inside `_isLoadingChapter = true` and `SelectedChapter = firstImported` (`L1817`) is a no-op because `SelectedChapter` is already `starter`. As a result, `_autoSaveTimer` is not stopped, `UpdateWordAndParagraphCounts` relies on `OnOriginalTextChanged`, `Editor.SyncParagraphPairs` is skipped in Paragraph Mode, and `Editor.RefreshActiveGlossaryAsync` is not called.
  2. In `ImportWebChapterInternalAsync` (`L1935`), overwriting `SelectedChapter` only checks `OriginalText` (not `TranslatedText`) and never resets `SelectedChapter.Status = ChapterStatus.NotTranslated` or calls `UpdateChapterStatusDisplay` / `UpdateStoryProgress()`.
- **Fix:**
  1. In `ImportTxtFilesAsync`, call `_autoSaveTimer.Stop();` before overwriting `starter`, and inside `if (ReferenceEquals(SelectedChapter, starter))`, call `if (Editor.IsParagraphMode) Editor.SyncParagraphPairs(OriginalText, TranslatedText);` and `_ = Editor.RefreshActiveGlossaryAsync(SelectedStory.Id, OriginalText);`.
  2. In `ImportWebChapterInternalAsync`, require `string.IsNullOrWhiteSpace(TranslatedText)` before overwriting `SelectedChapter`, stop `_autoSaveTimer`, and reset `SelectedChapter.Status = ChapterStatus.NotTranslated` (plus refresh display/progress).
- **If we skip it:** Importing `.txt` files into a new story while in Paragraph Mode leaves the placeholder text on screen, and importing from URL into a chapter with empty `OriginalText` but existing translation overwrites it.
