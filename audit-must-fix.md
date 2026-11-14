# Ponytail Audit — Must Fix (v5.8)

> **Scope:** Bugs, data loss, and state corruption that break real user workflows right now.
> **Target:** Agent-actionable specification. All 12 findings from the v5.5 audit were verified resolved in `8ac11a4` (v5.6), `32815b7` (v5.7), and `108f0ce` (v5.8). Each item below is numbered in priority order.

---

## Summary Table

| # | Area | File | Core Issue |
|---|---|---|---|
| **1** | Translation Cancel / Error Status | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | Cancelling or failing a translation never saves the updated `Status` to SQLite (leaving it stuck as `Translating` in DB) and marks empty cancelled chapters as `Edited` |
| **2** | Mid-Translation Chapter Switch | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `StartTranslationInternalAsync` uses the live `SelectedChapter` property instead of a captured local reference, overwriting the wrong chapter if the user switches chapters during translation |
| **3** | Whole-Story Search & Replace | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `OpenSearchReplaceAsync` only reloads `SelectedChapter` after whole-story replace, leaving stale `TranslatedText` and `Status` in `Chapters` in memory |
| **4** | Paragraph Retranslate Context | `src/NovelTranslator.Core/Translation/PromptBuilder.cs` / `TranslationPipeline.cs` | `PromptBuilder.BuildMessages` drops `previousSourceContext` whenever `previousTranslationContext` is `null`, so `RetranslateParagraphAsync` never includes `surroundingSourceContext` in the prompt |

---

## 1. Cancelling or failing a translation leaves `Chapters.Status` stuck as `Translating` in SQLite (and marks empty cancelled chapters as `Edited`)
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L976-L990)
- [TranslationPipeline.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/TranslationPipeline.cs#L91-L94)

- **What this is:** Stopping an active translation (`StopTranslationCommand` / `OperationCanceledException`) or handling an LLM/network error (`catch (Exception ex)`).
- **Problem:** At the start of `TranslationPipeline.TranslateChapterAsync` (`L92-93`), `chapter.Status` is set to `ChapterStatus.Translating` and saved to SQLite (`await _repository.UpdateChapterAsync(chapter)`).
  1. When an exception occurs (`MainViewModel.cs:L983-990`), `chapter.Status` is never restored in memory or SQLite—leaving the chapter permanently in `ChapterStatus.Translating` (`"กำลังแปล"`).
  2. When the user cancels (`MainViewModel.cs:L976-982`), `SelectedChapter.Status` is set to `ChapterStatus.Edited` in memory (even when `TranslatedText` is completely empty, which causes `TranslateAllChaptersAsync` to skip the untranslated chapter!) and `_repository.UpdateChapterAsync` is **never called**, so in SQLite `Status` remains `ChapterStatus.Translating`.
- **Fix:** In both `catch (OperationCanceledException)` and `catch (Exception ex)` in `StartTranslationInternalAsync`, compute the actual status from `chapter.TranslatedText`, persist it to SQLite, and refresh the UI:
  ```csharp
  chapter.Status = string.IsNullOrWhiteSpace(chapter.TranslatedText)
      ? ChapterStatus.NotTranslated
      : ChapterStatus.Edited;
  await _repository.UpdateChapterAsync(chapter);
  UpdateChapterStatusDisplay(chapter.Status);
  UpdateStoryProgress();
  ```
- **If we skip it:** Any cancelled or failed translation leaves the chapter stuck as `"กำลังแปล"` in SQLite after restart, and cancelling before Chunk 1 finishes falsely marks an empty chapter as `"แก้ไขแล้ว"`.

---

## 2. Switching chapters or stories while a translation is running corrupts the newly selected chapter's text and status
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L898-L1001)

- **What this is:** Clicking another chapter in the sidebar or pressing `Alt+Left` / `Alt+Right` while `IsTranslating` is `true`.
- **Problem:** `StartTranslationInternalAsync` references the live `SelectedStory` and `SelectedChapter` properties throughout the method and inside the `Progress<TranslationProgressInfo>` callback (`L933`: `TranslatedText = info.CombinedTranslatedTextSoFar;`). If the user clicks Chapter 2 while Chapter 1 is translating, streaming tokens from Chapter 1 overwrite `TranslatedText` while Chapter 2 is displayed, and completion/cancellation (`L967`, `L980`) reads or mutates `SelectedChapter` (now Chapter 2) instead of Chapter 1.
- **Fix:**
  1. Capture `var story = SelectedStory; var chapter = SelectedChapter;` at the top of `StartTranslationInternalAsync` and pass `(story, chapter)` to `pipeline.TranslateChapterAsync`.
  2. In the `progress` callback and after `await Task.Run`, only assign `TranslatedText`, `UpdateChapterStatusDisplay`, and `Editor.SyncParagraphPairs` when `ReferenceEquals(SelectedChapter, chapter)`.
  3. Guard `PreviousChapter()` and `NextChapter()` with `if (IsTranslating) return;` (or disable chapter switching while translating).
- **If we skip it:** Clicking another chapter during translation streams Chapter 1's output into Chapter 2's editor and corrupts Chapter 2's status.

---

## 3. Whole-story Search & Replace (`ScopeIndex == 1`) updates SQLite but leaves stale `TranslatedText` and `Status` in `MainViewModel.Chapters` in memory
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1872-L1907)
- [SearchReplaceViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/SearchReplaceViewModel.cs#L107-L137)

- **What this is:** Running Search & Replace (`ค้นหาและแทนที่คำแปล`) with scope set to **"ทั้งเรื่อง"** (`ScopeIndex == 1`).
- **Problem:** `SearchReplaceViewModel.ApplyReplaceAsync` loads fresh `Chapter` instances via `_repository.GetChaptersByStoryAsync(_story.Id)` and updates them in SQLite. When the dialog closes, `MainViewModel.OpenSearchReplaceAsync` (`L1884-1905`) only reloads `SelectedChapter` via `_repository.GetChapterByIdAsync(SelectedChapter.Id)`. All other `Chapter` instances in `MainViewModel.Chapters` keep their **old un-replaced `TranslatedText` and old `Status`** in memory. Clicking any other chapter loads its stale in-memory text, and `ExportStoryAsync` (`ส่งออกทั้งเรื่อง (.txt)`) exports the un-replaced in-memory `Chapters`.
- **Fix:** In `OpenSearchReplaceAsync`, reload all chapters for `SelectedStory.Id` (`var freshChapters = await _repository.GetChaptersByStoryAsync(SelectedStory.Id);`), sync `TranslatedText`, `Status`, and `UpdatedAt` onto the matching in-memory `Chapters` items, and call `UpdateChapterStatusDisplay(SelectedChapter.Status)` and `UpdateStoryProgress()`.
- **If we skip it:** Whole-story Search & Replace changes appear lost whenever the user clicks another chapter or exports the full story to `.txt`.

---

## 4. `RetranslateParagraphAsync` silently drops `surroundingSourceContext` because `PromptBuilder.BuildMessages` requires `previousTranslationContext` to be non-empty
**Location:**
- [PromptBuilder.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/PromptBuilder.cs#L109-L117)
- [TranslationPipeline.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/TranslationPipeline.cs#L251-L268)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1113-L1122)

- **What this is:** Re-translating a single paragraph in Paragraph Mode (`แปลย่อหน้านี้ใหม่ ↻` via `RetranslateParagraphInPlaceAsync`).
- **Problem:** In `MainViewModel.RetranslateParagraphInPlaceAsync` (`L1113-1121`), the preceding paragraph's `SourceText` is passed as `surroundingSourceContext` to `pipeline.RetranslateParagraphAsync`, which calls:
  ```csharp
  PromptBuilder.BuildMessages(story, selectedSourceParagraph, matchedGlossary, surroundingSourceContext, null, null, _settings.CustomSystemPrompt);
  ```
  However, `PromptBuilder.BuildMessages` (`L109`) checks:
  ```csharp
  if (!string.IsNullOrWhiteSpace(previousSourceContext) && !string.IsNullOrWhiteSpace(previousTranslationContext))
  ```
  Because `previousTranslationContext` is `null`, this condition is always `false`, so `surroundingSourceContext` is silently dropped and never sent to the LLM!
- **Fix:**
  1. In `PromptBuilder.BuildMessages` (`L109-117`), emit the `### บริบทย่อหน้าก่อนหน้า` block whenever `!string.IsNullOrWhiteSpace(previousSourceContext)` is true, and append `[คำแปลก่อนหน้า]` inside it when `!string.IsNullOrWhiteSpace(previousTranslationContext)` is also true.
  2. Optionally also pass the preceding paragraph's `TranslatedText` (`Editor.ParagraphPairs[pair.Index - 2].TranslatedText`) into `RetranslateParagraphAsync`.
  3. Add a unit test in `PromptBuilderTests.cs` or `TranslationPipelineTests.cs` verifying that `RetranslateParagraphAsync` includes `surroundingSourceContext` in the built prompt.
- **If we skip it:** Single-paragraph retranslation receives zero context from the preceding paragraph despite `MainViewModel` passing `surroundingContext`.
