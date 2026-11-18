# Ponytail Audit — Should Fix (v7.1)

> **Scope:** Workspace state cleanup on deleting the last story, non-modal WebView story isolation, avoiding redundant status/glossary mutations on completed chapters, deduplicating helpers, and removing leftover dead properties.
> **Target:** Agent-actionable specification. Numbered 7–11 (continuing from `audit-must-fix.md` items 1–6).

---

## Summary Table

| # | Area | File | Core Issue |
|---|---|---|---|
| **7** | Delete Last Story Workspace State | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | Deleting the only story leaves `IsStoryDetailsExpanded = true`, stale `Editor.ParagraphPairs` / `ActiveGlossary` / `_lastDeletedChapters`, and an unusable `SelectedStory == null` state |
| **8** | Non-Modal WebView Target Story | `src/NovelTranslator.App/Views/MainWindow.axaml.cs` / `MainViewModel.cs` | Non-modal `BrowserImportWindow` calls `ImportWebChapterAsync`, which reads live `SelectedStory` on each chapter callback instead of the story that opened the browser |
| **9** | Completed Chapter No-Op Translate | `src/NovelTranslator.Core/Translation/TranslationPipeline.cs` / `MainViewModel.cs` | Clicking "แปลตอนนี้" on a 100%-completed `Edited` chapter resets its status from `Edited` to `Translated` and re-runs the AI Glossary scanner even though 0 chunks were translated |
| **10** | Duplicate Paragraph Split & Status Badge | `src/NovelTranslator.App/ViewModels/EditorViewModel.cs` / `MainViewModel.cs` | `EditorViewModel.SplitParagraphs` misses `.Replace('\r', '\n')`, and `MainViewModel.UpdateChapterStatusDisplay` duplicates `Chapter.StatusBadgeText` / `StatusBadgeColor` |
| **11** | Leftover Dead Properties (`-35` lines) | `GlossaryExtractor.cs`, `ParagraphPairViewModel.cs`, `MainViewModel.cs`, `GlossaryServiceTests.cs` | `ExtractedTerm.IsSelected` / `INotifyPropertyChanged`, `ParagraphPairViewModel.IsSelected`, and `MainViewModel.IsHeaderVisible` have zero callers in `src/` |

---

## 7. Deleting the last story leaves the story editor panel open over a broken no-story workspace
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L653-L709)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1229-L1239)

- **What this is:** Clicking **"ลบเรื่อง"** (`DeleteStoryAsync`) when only one story exists in the database.
- **Problem:** `DeleteStoryAsync` deletes the story from SQLite, removes it from `Stories`, and sets `SelectedStory = Stories.FirstOrDefault()` (`null`). When `SelectedStory` becomes `null`:
  1. `IsStoryDetailsExpanded` remains `true`, leaving the story-edit card open on screen for a deleted story.
  2. `_lastDeletedChapters`, `Editor.ParagraphPairs`, and `Editor.ActiveGlossary` are not cleared.
  3. With `SelectedStory == null`, clicking **"เพิ่มตอน"**, **"แปลตอนนี้"**, **"นำเข้าไฟล์นิยาย (.txt)"**, or **"พจนานุกรมคำศัพท์"** silently returns without doing anything until the user restarts the app (when `LoadInitialDataAsync` creates a default story).
- **Fix:** In `DeleteStoryAsync` (`L1235-1239`), set `IsStoryDetailsExpanded = false`, clear `_lastDeletedChapters` (and notify `CanUndoDeleteChapter`), clear `Editor.ParagraphPairs` and `Editor.ActiveGlossary`, and if `Stories.Count == 0`, call `await LoadInitialDataAsync();` so a fresh default story and starter chapter are created immediately.
- **If we skip it:** Deleting the only story leaves the edit popup open over an unusable workspace until the user restarts the app.

---

## 8. Non-modal `BrowserImportWindow` inserts chapters into the wrong story if the user switches stories while it is open
**Location:**
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L138-L150)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1866-L1942)

- **What this is:** Using **เบราว์เซอร์ดึงเนื้อหา (WebView)** (`BrowserImportWindow`) to import chapters while `MainWindow` remains interactive in the background.
- **Problem:** `MainWindow.axaml.cs` opens `BrowserImportWindow` with `win.Show(this)` (non-modal) and passes `result => ViewModel.ImportWebChapterAsync(result)`. `ImportWebChapterInternalAsync` reads the live `SelectedStory` property each time a chapter finishes downloading. If the user switches stories in `MainWindow` while a multi-chapter WebView import is running, subsequent chapters are inserted into the newly selected story.
- **Fix:** Add an optional `Story? targetStory = null` parameter to `ImportWebChapterAsync(WebChapterResult result, Story? targetStory = null)` and `ImportWebChapterInternalAsync`. When opening `BrowserImportWindow` in `MainWindow.axaml.cs` (`L138-150`), capture `var story = ViewModel.SelectedStory;` and pass `result => ViewModel.ImportWebChapterAsync(result, story)`. Inside `ImportWebChapterInternalAsync`, if `targetStory` is no longer in `Stories`, return early; if `!ReferenceEquals(SelectedStory, story)`, insert the chapter into SQLite for `story.Id` without mutating the currently displayed story's `Chapters` collection.
- **If we skip it:** Switching stories in the main window while WebView is importing a batch splits the imported chapters across two stories.

---

## 9. Clicking "แปลตอนนี้" on a finished `Edited` chapter resets its status to `Translated` and re-runs the AI Glossary scanner
**Location:**
- [TranslationPipeline.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/TranslationPipeline.cs#L70-L99)
- [TranslationPipeline.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/TranslationPipeline.cs#L287-L290)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1033-L1053)

- **What this is:** Clicking **"แปลตอนนี้"** (`Ctrl+Enter`, `forceRetranslate: false`) on a chapter whose chunks are already 100% completed (`completedCount == chunks.Count`).
- **Problem:** Even when zero chunks need translating (`!forceRetranslate && chunks.Count > 0 && completedCount == chunks.Count`), `TranslateChapterAsync` overwrites `chapter.Status` in SQLite from `Edited` to `Translating` (`L97`) and then to `Translated` (`L288`), and `StartTranslationInternalAsync` (`L1033`) then runs `AutoExtractAndSaveGlossaryAsync`, making an unnecessary LLM request on an unchanged chapter.
- **Fix:** In `TranslationPipeline.TranslateChapterAsync` (right after `progress.Report` at `L84-92`), if `!forceRetranslate && chunks.Count > 0 && completedCount == chunks.Count`, ensure `chapter.Status` is `Translated` or `Edited` (do not overwrite `Edited` with `Translated`) and return early; or return a `bool anyChunkTranslated` from `TranslateChapterAsync` and only run `AutoExtractAndSaveGlossaryAsync` in `StartTranslationInternalAsync` when at least one chunk was actually translated.
- **If we skip it:** Pressing `Ctrl+Enter` on a manually polished chapter erases its `"แก้ไขแล้ว"` (`Edited`) badge and wastes an AI glossary scan call.

---

## 10. Duplicate `SplitParagraphs` (missing `\r` normalization) and duplicate `StatusBadgeText`/`StatusBadgeColor` mappings
**Location:**
- [EditorViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/EditorViewModel.cs#L120-L138)
- [DomainModels.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Models/DomainModels.cs#L104-L120)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L854-L872)

- **What this is:** Paragraph splitting in `EditorViewModel` and chapter status badge text/color mapping in `MainViewModel`.
- **Problem:**
  1. `EditorViewModel.SplitParagraphs` (`L123`) only calls `text.Replace("\r\n", "\n")` without `.Replace('\r', '\n')`, unlike `Chunker.SplitIntoChunks`, `OutputValidator.CountParagraphs`, and `MobileExportService.SplitParagraphs`.
  2. `MainViewModel.UpdateChapterStatusDisplay` (`L854-872`) duplicates the exact same `switch` tables already defined on `Chapter.StatusBadgeText` and `Chapter.StatusBadgeColor` in `DomainModels.cs` (`L104-120`).
- **Fix:**
  1. Change `text.Replace("\r\n", "\n")` to `text.Replace("\r\n", "\n").Replace('\r', '\n')` in `EditorViewModel.SplitParagraphs` (`L123`) (and in `MainViewModel.UpdateWordAndParagraphCounts` at `L849`).
  2. Expose static helpers `Chapter.GetStatusBadgeText(ChapterStatus)` and `Chapter.GetStatusBadgeColor(ChapterStatus)` on `Chapter` in `DomainModels.cs` and call them from both `Chapter.StatusBadgeText`/`Color` and `MainViewModel.UpdateChapterStatusDisplay`.
- **If we skip it:** Text with bare `\r` line breaks splits differently in Paragraph Mode than in Chunker/Export, and status badge colors/text must be maintained in two places.

---

## 11. Delete leftover dead properties from removed UI (`ExtractedTerm.IsSelected`, `ParagraphPairViewModel.IsSelected`, `MainViewModel.IsHeaderVisible`)
**Location:**
- [GlossaryExtractor.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Glossary/GlossaryExtractor.cs#L16-L39)
- [ParagraphPairViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/ParagraphPairViewModel.cs#L22-L23)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L116)
- [GlossaryServiceTests.cs](file:///d:/Work/Other/ReadLator/tests/NovelTranslator.Tests/GlossaryServiceTests.cs#L43-L53)

- **What this is:** Leftover selection and header-visibility properties whose UI bindings were removed in earlier versions.
- **Problem:** Now that `AutoGlossaryDialog` and `AutoGlossaryViewModel` are deleted, nothing in `src/` reads or writes `ExtractedTerm.IsSelected` or subscribes to `ExtractedTerm.PropertyChanged`. Likewise, `ParagraphPairViewModel.IsSelected` (`_isSelected`) and `MainViewModel.IsHeaderVisible` have zero references in any XAML view, code file, or test.
- **Fix:** Remove `INotifyPropertyChanged` and `IsSelected` from `ExtractedTerm` (and delete `ExtractedTerm_IsSelected_RaisesPropertyChanged` in `GlossaryServiceTests.cs`), remove `[ObservableProperty] private bool _isSelected;` from `ParagraphPairViewModel`, and remove `IsHeaderVisible` from `MainViewModel`.
- **If we skip it:** ~35 lines of dead properties and unused `PropertyChanged` boilerplate remain in Core and ViewModels.
