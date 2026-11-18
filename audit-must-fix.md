# Ponytail Audit — Must Fix (v7.1)

> **Scope:** Bugs, data loss, concurrent translation collisions, and Search & Replace mismatches verified in the v7.1 codebase (`77bfdd4`).
> **Target:** Agent-actionable specification. All 15 findings from the v6.8 audit were verified resolved in `e2f3128` (v6.9), `2e5342c` (v7.0), and `77bfdd4` (v7.1). Each item below is numbered in priority order (1–6; continued in `audit-should-fix.md` items 7–11 and `audit-nice-to-have.md` item 12).

---

## Summary Table

| # | Area | File | Core Issue |
|---|---|---|---|
| **1** | Filtered Select-All Chapter Delete | `src/NovelTranslator.App/Views/MainWindow.axaml.cs` / `MainViewModel.cs` | `RequestSelectAllChapters` marks all `ViewModel.Chapters` as `IsSelected = true` instead of only `FilteredChapters`, and `DeleteChapterAsync` deletes hidden chapters outside the active filter |
| **2** | Concurrent `Ctrl+Enter` Translation | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `StartTranslationInternalAsync`, `RetranslateAllAsync`, and `RetranslateParagraphInPlaceAsync` do not check `if (IsTranslating) return;`, allowing `Ctrl+Enter` to launch a second parallel translation on the same chapter |
| **3** | Glossary Auto-Select Overwrite & Duplicate Edit | `src/NovelTranslator.App/Views/MainWindow.axaml.cs` / `GlossaryViewModel.cs` | Opening Glossary sets `SelectFirstOnLoad = true` (`SelectedEntry = Entries[0]`), so typing a new term overwrites `Entries[0]`, and editing `SelectedEntry.SourceTerm` does not check for duplicates against other rows |
| **4** | Whole-Story Search & Replace Thai Preview Mismatch | `src/NovelTranslator.Core/Data/NovelRepository.cs` / `SearchReplaceViewModel.cs` | `SearchFtsAsync` skips `SearchFallbackLikeAsync` whenever FTS5 `unicode61` matches ≥1 space-prefixed token, causing `PreviewMatchesAsync` to hide chapters with mid-sentence Thai matches that `ApplyReplaceAsync` replaces |
| **5** | Mid-Retranslate Chapter Switch | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `RetranslateParagraphInPlaceAsync` does not verify that `SelectedChapter` is still the same chapter when the LLM call completes, overwriting the newly selected chapter's text |
| **6** | Stop Button in Glossary Scan & Batch Queue | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `ScanNewTermsAsync` never creates `_translationCts` (so Stop does nothing) or sets `HasError = true` on error, and `StartTranslationInternalAsync` swallows cancellation during Auto-Glossary so `TranslateAllChaptersAsync` continues to the next chapter |

---

## 1. "เลือกทั้งหมด" (Select All) while a chapter filter is active marks hidden chapters as selected and deletes them
**Location:**
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L656-L666)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1440-L1476)

- **What this is:** Filtering the sidebar chapter list by search text or status (e.g. `"ยังไม่แปล"`), clicking **"เลือกทั้งหมด"** (`ToggleSelectAllChaptersCommand`), and clicking **"ลบตอนที่เลือก"** (`DeleteChapterCommand`).
- **Problem:**
  1. `MainWindow.axaml.cs` (`L661`) implements `RequestSelectAllChapters` as:
     ```csharp
     ChapterListBox.SelectAll();
     foreach (var c in ViewModel.Chapters) c.IsSelected = true;
     ```
     Even though `ChapterListBox.ItemsSource` is `ViewModel.FilteredChapters`, the loop sets `c.IsSelected = true` on **every chapter in `ViewModel.Chapters`**, including chapters hidden by the active filter.
  2. `MainViewModel.DeleteChapterAsync` (`L1475`) collects `var checkedChapters = Chapters.Where(c => c.IsSelected).ToList();`, deleting hidden chapters that are not even in `FilteredChapters`.
- **Fix:**
  1. In `MainWindow.axaml.cs` (`RequestSelectAllChapters`, `L661`), only mark chapters present in `ViewModel.FilteredChapters`:
     ```csharp
     var visible = ViewModel.FilteredChapters.ToHashSet();
     foreach (var c in ViewModel.Chapters) c.IsSelected = visible.Contains(c);
     ```
  2. In `MainViewModel.RefreshFilteredChapters()` (`L711`), clear `IsSelected` on chapters not in `FilteredChapters` (`var visible = FilteredChapters.ToHashSet(); foreach (var c in Chapters) if (!visible.Contains(c)) c.IsSelected = false;`).
  3. In `MainViewModel.DeleteChapterAsync()` (`L1475`), filter against visible chapters: `var checkedChapters = FilteredChapters.Where(c => c.IsSelected).ToList();`.
- **If we skip it:** Filtering to "Not Translated", clicking "Select All", and clicking "Delete Selected" deletes every translated chapter in the story too.

---

## 2. Pressing `Ctrl+Enter` during an active translation starts a second parallel translation on the same chapter
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L877-L881)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L918-L930)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L950-L977)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1151-L1161)

- **What this is:** Pressing the translate shortcut (`Ctrl+Enter`) or triggering `RetranslateAllAsync` / `RetranslateParagraphInPlaceAsync` while `IsTranslating` is already `true`.
- **Problem:** While the toolbar button binds `IsEnabled="{Binding !IsTranslating}"`, the window `KeyBinding` (`MainWindow.axaml.cs:L782`) invokes `StartTranslationCommand` directly, and neither `StartTranslationAsync`, `RetranslateAllAsync`, `StartTranslationInternalAsync`, nor `RetranslateParagraphInPlaceAsync` checks `if (IsTranslating) return;`. Pressing `Ctrl+Enter` mid-translation overwrites `_translationCts = new CancellationTokenSource();` without cancelling the running task and launches a second concurrent `TranslateChapterAsync` pipeline writing to the same chapter and SQLite chunks. In addition, `_autoSaveTimer` is not stopped when translation starts, so a pending auto-save tick can fire 1.5s later during streaming.
- **Fix:**
  1. Add `if (IsTranslating) return;` at the top of `StartTranslationInternalAsync` (`L952`), `RetranslateAllAsync` (`L920`), and `RetranslateParagraphInPlaceAsync` (`L1153`).
  2. Call `_autoSaveTimer.Stop();` at the start of `StartTranslationInternalAsync` before mutating `chapter.OriginalText` / `chapter.TranslatedText`.
- **If we skip it:** Pressing `Ctrl+Enter` while a translation is running starts a second AI stream that collides with the first in the editor and database.

---

## 3. Opening the Glossary window pre-selects the first row, so typing a new term overwrites the newest existing entry
**Location:**
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L119-L129)
- [GlossaryViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/GlossaryViewModel.cs#L55-L66)
- [GlossaryViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/GlossaryViewModel.cs#L99-L153)

- **What this is:** Opening **พจนานุกรมคำศัพท์ (Glossary)** from the menu or notification banner and clicking **"บันทึกคำศัพท์"** (`SaveEntryAsync`).
- **Problem:**
  1. `MainWindow.axaml.cs` (`L121`) creates `new GlossaryViewModel(ViewModel.Repository, story) { SelectFirstOnLoad = true }`, which automatically sets `SelectedEntry = Entries[0]` on open and populates the bottom form with the newest entry. Because the save button always says **"บันทึกคำศัพท์"**, a user who opens Glossary, types a new term into the form without clicking **"ล้างฟอร์ม"** first, and clicks **"บันทึกคำศัพท์"** overwrites `Entries[0]` instead of inserting a new term.
  2. When editing an existing entry (`SelectedEntry != null`, `GlossaryViewModel.cs:L140-150`), if the user changes `SourceTerm` to match another existing entry in `_allEntries` (`e.Id != SelectedEntry.Id`), `SaveEntryAsync` updates the row in SQLite without checking for duplicates, creating two rows with the same `SourceTerm`.
- **Fix:**
  1. Remove `SelectFirstOnLoad = true` in `MainWindow.axaml.cs` (`L121`) (and remove the unused `SelectFirstOnLoad` property from `GlossaryViewModel`) so the bottom form opens empty and ready to add a new term unless the user clicks a row in the table.
  2. In `GlossaryViewModel.SaveEntryAsync` (`else` branch, `L140-150`), check if another entry (`e.Id != SelectedEntry.Id && e.SourceTerm.Equals(trimmedSource, StringComparison.OrdinalIgnoreCase)`) already exists: if so, update that entry (or merge and delete the duplicate row) instead of creating duplicate `SourceTerm` rows.
- **If we skip it:** Users opening the Glossary to add a word overwrite their most recent glossary entry, or create duplicate terms when renaming an entry.

---

## 4. Whole-story Search & Replace preview misses Thai words inside sentences whenever FTS5 matches at least one space-prefixed token
**Location:**
- [NovelRepository.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Data/NovelRepository.cs#L303-L340)
- [SearchReplaceViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/SearchReplaceViewModel.cs#L52-L114)

- **What this is:** Clicking **"ดูตัวอย่างจุดที่พบ"** (`PreviewMatchesAsync`) with scope set to **"ทั้งเรื่อง"** (`ScopeIndex == 1`).
- **Problem:**
  1. `NovelRepository.SearchFtsAsync` queries `Chapters_Fts` (`tokenize = 'unicode61'`, which only splits tokens on whitespace/punctuation) and only falls back to `SearchFallbackLikeAsync` (`LIKE '%query%'`) when `list.Count == 0` (`NovelRepository.cs:L331`). Because Thai text does not use spaces between words, if a search term like `"กระบี่"` appears after a space in Chapter 1 (`"กระบี่ เล่มนี้"`) and mid-sentence in Chapter 2 (`"สำนักกระบี่"`), FTS5 returns only Chapter 1 (`list.Count == 1`), skipping the `LIKE` fallback and omitting Chapter 2 from the preview—even though clicking **"แทนที่ทั้งหมด"** (`ApplyReplaceAsync`, which uses `GetChaptersByStoryAsync`) replaces both Chapter 1 and Chapter 2.
  2. In both `PreviewMatchesAsync` (`L63`) and `ApplyReplaceAsync` (`L111`), `if (ScopeIndex == 0 && _currentChapter != null)` falls through to the whole-story branch if `_currentChapter` is `null` while `ScopeIndex == 0` ("เฉพาะตอนนี้").
- **Fix:**
  1. In `SearchReplaceViewModel.PreviewMatchesAsync` and `ApplyReplaceAsync`, if `ScopeIndex == 0`, return early when `_currentChapter == null` instead of falling through to all chapters; when `ScopeIndex == 1`, use `await _repository.GetChaptersByStoryAsync(_story.Id)` in `PreviewMatchesAsync` just like `ApplyReplaceAsync` does (1 SQLite query instead of 1 FTS query + N `GetChapterByIdAsync` queries, and 100% consistent substring matching).
  2. In `NovelRepository.SearchFtsAsync` (`L330-335`), when `list.Count < limit`, also query `SearchFallbackLikeAsync` and append any non-duplicate `ChapterId` matches up to `limit` so mid-sentence Thai matches are never dropped when another chapter happens to match a token prefix.
- **If we skip it:** Whole-story Search & Replace preview hides chapters where a Thai word sits inside a sentence without a space before it, disagreeing with what "Replace All" actually modifies.

---

## 5. Switching chapters while a single paragraph is retranslating corrupts the newly selected chapter's text
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1151-L1205)

- **What this is:** Right-clicking a paragraph in Paragraph Mode and clicking **"แปลย่อหน้านี้ใหม่"** (`RetranslateParagraphInPlaceAsync`), then selecting another chapter before the AI finishes.
- **Problem:** `RetranslateParagraphInPlaceAsync` does not capture `var story = SelectedStory; var chapter = SelectedChapter;` or verify that `SelectedChapter` is still `chapter` after `await Task.Run(...)` completes (`L1183-1189`). If the user switched to Chapter 2 while Paragraph 1 of Chapter 1 was retranslating, setting `pair.TranslatedText = newTranslation.Trim()` and `TranslatedText = Editor.CombineTranslatedParagraphs()` overwrites `TranslatedText` and calls `MarkUnsavedChanges()` while Chapter 2 is active.
- **Fix:** Capture `var story = SelectedStory; var chapter = SelectedChapter;` at the top of `RetranslateParagraphInPlaceAsync`, pass `story` to `pipeline.RetranslateParagraphAsync`, and when it returns:
  ```csharp
  if (!string.IsNullOrWhiteSpace(newTranslation) &&
      ReferenceEquals(SelectedChapter, chapter) &&
      Editor.ParagraphPairs.Contains(pair))
  {
      pair.TranslatedText = newTranslation.Trim();
      TranslatedText = Editor.CombineTranslatedParagraphs();
      ProgressText = $"แปลใหม่ย่อหน้าที่ {pair.Index} สำเร็จแล้ว";
      MarkUnsavedChanges();
  }
  ```
- **If we skip it:** Switching chapters during a single-paragraph retranslation overwrites the newly selected chapter's translation.

---

## 6. Clicking "หยุด" (Stop) does nothing during manual AI Glossary scans and fails to stop "Translate All" during the Auto-Glossary step
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L883-L915)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1033-L1053)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L2131-L2179)

- **What this is:** Clicking the **"หยุด"** button during **"สแกนหาศัพท์ใหม่ในตอนนี้ (AI)"** (`ScanNewTermsAsync`) or during the Auto-Glossary phase of **"แปลทุกตอนที่เหลือ"** (`TranslateAllChaptersAsync`).
- **Problem:**
  1. `ScanNewTermsAsync` (`L2149`) sets `IsTranslating = true` (which displays the **"หยุด"** button) but never creates `_translationCts` or passes a `CancellationToken` to `AutoExtractAndSaveGlossaryAsync`. Clicking **"หยุด"** does nothing. Furthermore, in `catch (Exception ex)` (`L2168`), it sets `LastErrorDetails` without setting `HasError = true` (and never clears `HasError = false` on start), so the **"คัดลอกข้อผิดพลาด"** and **"ดูรายละเอียด"** buttons remain hidden.
  2. In `StartTranslationInternalAsync` (`L1045`), `OperationCanceledException` during `AutoExtractAndSaveGlossaryAsync` is caught and swallowed after `chapter.Status` is already set to `ChapterStatus.Translated`. Consequently, `TranslateAllChaptersAsync` (`L909`: `if (HasError || SelectedChapter.Status != ChapterStatus.Translated)`) does not detect that the user clicked Stop and immediately starts translating the next chapter in the queue.
- **Fix:**
  1. In `ScanNewTermsAsync`, initialize `_translationCts = new CancellationTokenSource()`, set `HasError = false`, pass `_translationCts.Token` to `AutoExtractAndSaveGlossaryAsync`, handle `OperationCanceledException` (`ProgressText = "ยกเลิกการสแกนคำศัพท์แล้ว";`), set `HasError = true` in `catch (Exception ex)`, and dispose/clear `_translationCts` in `finally`.
  2. In `TranslateAllChaptersAsync` (`L909`), check `if (HasError || _translationCts?.IsCancellationRequested == true || pending[i].Status != ChapterStatus.Translated) break;`.
- **If we skip it:** Users cannot cancel a slow AI glossary scan, cannot view its error details, and clicking Stop during the glossary-scan phase of "Translate All" fails to stop the batch.
