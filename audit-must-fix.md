# Ponytail Audit — Must Fix (v6.8)

> **Scope:** Bugs, data loss, silent state corruption, and broken core workflows verified in the v6.8 codebase.
> **Target:** Agent-actionable specification. Each item below is numbered in priority order (1–7; continued in `audit-should-fix.md` items 8–13 and `audit-nice-to-have.md` items 14–15).

---

## Summary Table

| # | Area | File | Core Issue |
|---|---|---|---|
| **1** | Translation Cancel / Error Status | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | Cancelling or failing a translation never saves the updated `Status` to SQLite (leaving it stuck as `Translating` in DB) and marks empty cancelled chapters as `Edited` |
| **2** | Mid-Translation Chapter Switch | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `StartTranslationInternalAsync` uses the live `SelectedStory` / `SelectedChapter` properties instead of captured local references, overwriting the wrong chapter if the user switches chapters during translation |
| **3** | Whole-Story Search & Replace | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `OpenSearchReplaceAsync` only reloads `SelectedChapter` after whole-story replace, leaving stale `TranslatedText` and `Status` in `Chapters` in memory |
| **4** | Edited Translation Preservation on Append | `src/NovelTranslator.Core/Translation/TranslationPipeline.cs` | Appending new source paragraphs to an edited or Search-and-Replaced chapter sets `needsResplit = true`, which drops `preserveExistingTranslation` and overwrites earlier manual edits with raw `ChapterChunks` |
| **5** | Settings Cancel Saves Unconfirmed Edits | `src/NovelTranslator.App/ViewModels/SettingsViewModel.cs` | Appearance/shortcut handlers, `OnBackendIndexChanged`, and `TestConnectionAsync` immediately mutate `_settings` and write `settings.json`, and `MainWindow` unconditionally calls `ReloadSettings()`, making Cancel/Esc keep changes |
| **6** | Paragraph Retranslate Context | `src/NovelTranslator.Core/Translation/PromptBuilder.cs` / `TranslationPipeline.cs` | `PromptBuilder.BuildMessages` drops `previousSourceContext` whenever `previousTranslationContext` is `null`, so `RetranslateParagraphAsync` never sends `surroundingSourceContext` to the LLM |
| **7** | Paragraph Mode Empty Slot Shift | `src/NovelTranslator.App/ViewModels/EditorViewModel.cs` | `CombineTranslatedParagraphs` / `CombineSourceParagraphs` filter out empty paragraphs on every keystroke, shifting translations onto the wrong source paragraphs when an earlier paragraph is untranslated |

---

## 1. Cancelling or failing a translation leaves `Chapters.Status` stuck as `Translating` in SQLite (and marks empty cancelled chapters as `Edited`)
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1054-L1068)
- [TranslationPipeline.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/TranslationPipeline.cs#L91-L94)

- **What this is:** Stopping an active translation (`StopTranslationCommand` / `OperationCanceledException`) or handling an LLM/network error (`catch (Exception ex)`).
- **Problem:** At the start of `TranslationPipeline.TranslateChapterAsync` (`L92-93`), `chapter.Status` is set to `ChapterStatus.Translating` and saved to SQLite (`await _repository.UpdateChapterAsync(chapter)`).
  1. When an exception occurs (`MainViewModel.cs:L1061-1068`), `chapter.Status` is never restored in memory or SQLite—leaving the chapter permanently in `ChapterStatus.Translating` (`"กำลังแปล"`).
  2. When the user cancels (`MainViewModel.cs:L1054-1060`), `SelectedChapter.Status` is set to `ChapterStatus.Edited` in memory (even when `TranslatedText` is completely empty, which causes `TranslateAllChaptersAsync` to skip the untranslated chapter!) and `_repository.UpdateChapterAsync` is **never called**, so in SQLite `Status` remains `ChapterStatus.Translating`.
- **Fix:** Using the captured `chapter` reference (see item #2), in both `catch (OperationCanceledException)` and `catch (Exception ex)` in `StartTranslationInternalAsync`, compute the actual status from `chapter.TranslatedText`, persist it to SQLite, and refresh the UI:
  ```csharp
  chapter.Status = string.IsNullOrWhiteSpace(chapter.TranslatedText)
      ? ChapterStatus.NotTranslated
      : ChapterStatus.Edited;
  await _repository.UpdateChapterAsync(chapter);
  if (ReferenceEquals(SelectedChapter, chapter))
  {
      UpdateChapterStatusDisplay(chapter.Status);
  }
  UpdateStoryProgress();
  ```
- **If we skip it:** Cancelled empty chapters are skipped by "Translate All", and any cancelled or failed chapter reappears stuck as `"กำลังแปล"` in SQLite after restart.

---

## 2. Switching chapters or stories while a translation is running overwrites the newly selected chapter's text and status
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L951-L1079)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1651-L1671)

- **What this is:** Clicking another chapter in the sidebar or pressing `Alt+Left` / `Alt+Right` while `IsTranslating` is `true`.
- **Problem:** `StartTranslationInternalAsync` references the live `SelectedStory` and `SelectedChapter` properties throughout the method, inside the `Progress<TranslationProgressInfo>` callback (`L986`: `TranslatedText = info.CombinedTranslatedTextSoFar;`), on completion (`L1020-1035`), and on cancellation (`L1058`). If the user clicks Chapter 2 while Chapter 1 is translating, streaming tokens from Chapter 1 overwrite `TranslatedText` while Chapter 2 is displayed, and completion/cancellation reads or mutates `SelectedChapter` (now Chapter 2) instead of Chapter 1.
- **Fix:**
  1. Capture `var story = SelectedStory; var chapter = SelectedChapter;` at the top of `StartTranslationInternalAsync` and pass `(story, chapter)` to `pipeline.TranslateChapterAsync` and `AutoExtractAndSaveGlossaryAsync`.
  2. Guard UI-only property assignments (`TranslatedText`, `UpdateChapterStatusDisplay`, `Editor.SyncParagraphPairs`) in the `progress` callback, completion/error blocks, and `finally` with `if (ReferenceEquals(SelectedChapter, chapter))`.
  3. Optionally guard `PreviousChapter()` and `NextChapter()` with `if (IsTranslating) return;` if chapter switching should be blocked while translating.
- **If we skip it:** Clicking another chapter during translation streams Chapter 1's output into Chapter 2's editor and corrupts Chapter 2's status.

---

## 3. Whole-story Search & Replace (`ScopeIndex == 1`) updates SQLite but leaves stale `TranslatedText` and `Status` in `MainViewModel.Chapters` in memory
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L2221-L2256)
- [SearchReplaceViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/SearchReplaceViewModel.cs#L107-L137)

- **What this is:** Running Search & Replace (`ค้นหาและแทนที่คำแปล`) with scope set to **"ทั้งเรื่อง"** (`ScopeIndex == 1`).
- **Problem:** `SearchReplaceViewModel.ApplyReplaceAsync` loads fresh `Chapter` instances via `_repository.GetChaptersByStoryAsync(_story.Id)` and updates them in SQLite. When the dialog closes, `MainViewModel.OpenSearchReplaceAsync` (`L2233-2254`) only reloads `SelectedChapter` via `_repository.GetChapterByIdAsync(SelectedChapter.Id)`. All other `Chapter` instances in `MainViewModel.Chapters` keep their **old un-replaced `TranslatedText` and old `Status`** in memory. Clicking any other chapter loads its stale in-memory text, and `ExportStoryAsync` / `ExportMobileHtmlAsync` / `ExportEpubAsync` export the un-replaced in-memory `Chapters`.
- **Fix:** In `OpenSearchReplaceAsync`, reload all chapters for `SelectedStory.Id` (`var freshChapters = await _repository.GetChaptersByStoryAsync(SelectedStory.Id);`), sync `TranslatedText`, `Status`, and `UpdatedAt` onto matching items in `Chapters`, refresh `TranslatedText` and `Editor.SyncParagraphPairs` if `SelectedChapter` is present, and call `UpdateChapterStatusDisplay(SelectedChapter.Status)` and `UpdateStoryProgress()`.
- **If we skip it:** Whole-story Search & Replace changes appear lost whenever the user clicks another chapter or exports the full story.

---

## 4. Appending new paragraphs to a chapter whose translation was manually edited or Search-and-Replaced overwrites the edited translation with raw `ChapterChunks`
**Location:**
- [TranslationPipeline.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/TranslationPipeline.cs#L52-L77)

- **What this is:** Manually editing `TranslatedText` (or running Search & Replace) on a translated chapter, then appending new source paragraphs at the end of `OriginalText` and clicking **"แปลตอนนี้"** (`forceRetranslate: false`).
- **Problem:** When the user edits `TranslatedText` in the editor or via Search & Replace, `Chapters.TranslatedText` is updated in SQLite, while `ChapterChunks.TranslatedText` still holds the unedited raw LLM output per chunk. If the user later appends new paragraphs to the end of `OriginalText` and clicks **"แปลตอนนี้"**, `needsResplit` is `true` (`chunks.Count != splitTexts.Count`). Because `preserveExistingTranslation` (`L67`) requires `!needsResplit`, it becomes `false` even when all previously existing chunks (`0..oldCount-1`) are still intact as an unchanged completed prefix. Line 76 then overwrites `chapter.TranslatedText` from `chunks.Select(c => c.TranslatedText)`, wiping out the user's manual edits in the first part of the chapter.
- **Fix:** Before calling `SaveChunksAsync` when `needsResplit` is true, check whether existing chunks were all completed and form an exact prefix of `splitTexts`:
  ```csharp
  bool isPureAppend = !forceRetranslate
      && chunks.Count > 0
      && chunks.Count < splitTexts.Count
      && chunks.All(c => c.IsCompleted && !string.IsNullOrEmpty(c.TranslatedText))
      && chunks.Select(c => c.SourceText).SequenceEqual(splitTexts.Take(chunks.Count));
  ```
  Allow `preserveExistingTranslation` when `(!needsResplit || isPureAppend)`.
- **If we skip it:** Polishing a translation and later appending more paragraphs to the end of the chapter reverts the polished translation back to the raw unedited chunk text.

---

## 5. Clicking Cancel or pressing `Esc` in Settings still saves and applies unconfirmed changes
**Location:**
- [SettingsViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/SettingsViewModel.cs#L173-L294)
- [SettingsViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/SettingsViewModel.cs#L447-L496)
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L101-L110)

- **What this is:** Opening `SettingsWindow`, modifying settings (appearance, shortcuts, backend, or testing connection), and clicking **"ยกเลิก"** (Cancel) or pressing `Esc`.
- **Problem:**
  1. `ResetShortcut`, `ResetAllShortcuts`, `SetShortcut`, and every appearance `partial void On...Changed` handler (`L173-294`) call `SaveSettings()`, immediately writing **every** field on the form (including half-edited prompts, API keys, and temperatures) to `_settings` and `settings.json`.
  2. `OnBackendIndexChanged` (`L464`) and `TestConnectionAsync` (`L474`) call `ApplyToSettings()`, mutating the live shared `_settings` object even before the user clicks Save.
  3. `MainWindow.axaml.cs` (`L106-109`) calls `ViewModel.ReloadSettings()`, `ApplyEditorFont`, and `ApplyKeyBindings()` unconditionally after `await win.ShowDialog(this)` returns, even when cancelled.
- **Fix:**
  1. Remove `SaveSettings()` / `ApplyToSettings()` calls from property change handlers, shortcut methods, and `OnBackendIndexChanged`.
  2. In `TestConnectionAsync`, build a temporary `AppSettings` copy from the ViewModel fields and pass that copy to `LlmClient` instead of mutating `_settings`.
  3. Only call `SaveSettings()` in `SaveAndClose()` (and expose `public bool IsSaved { get; private set; }`). If the window closes without saving, restore `App.ApplyTheme(_settings.ThemeMode)` and skip `ReloadSettings()`.
- **If we skip it:** Clicking Cancel in Settings silently persists and applies unconfirmed edits to `settings.json` and the live app.

---

## 6. `RetranslateParagraphAsync` silently drops `surroundingSourceContext` because `PromptBuilder.BuildMessages` requires `previousTranslationContext` to be non-empty
**Location:**
- [PromptBuilder.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/PromptBuilder.cs#L155-L164)
- [TranslationPipeline.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/TranslationPipeline.cs#L290-L309)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1192-L1201)

- **What this is:** Re-translating a single paragraph in Paragraph Mode (**"แปลย่อหน้านี้ใหม่"** via `RetranslateParagraphInPlaceAsync`).
- **Problem:** In `MainViewModel.RetranslateParagraphInPlaceAsync` (`L1192-1201`), the preceding paragraph's `SourceText` is passed as `surroundingSourceContext` to `pipeline.RetranslateParagraphAsync`, which calls:
  ```csharp
  PromptBuilder.BuildMessages(story, selectedSourceParagraph, matchedGlossary, surroundingSourceContext, null, null, _settings.CustomSystemPrompt, null, _settings.PovMode);
  ```
  However, `PromptBuilder.BuildMessages` (`L156`) checks:
  ```csharp
  if (!string.IsNullOrWhiteSpace(previousSourceContext) && !string.IsNullOrWhiteSpace(previousTranslationContext))
  ```
  Because `previousTranslationContext` is `null`, this condition is always `false`, so `surroundingSourceContext` is silently dropped and never sent to the LLM.
- **Fix:**
  1. In `PromptBuilder.BuildMessages` (`L156-164`), emit the `### บริบทย่อหน้าก่อนหน้า` block whenever `!string.IsNullOrWhiteSpace(previousSourceContext)` is true, and append `[คำแปลก่อนหน้า]` inside it when `!string.IsNullOrWhiteSpace(previousTranslationContext)` is also true.
  2. Pass the preceding paragraph's `TranslatedText` (`Editor.ParagraphPairs[pair.Index - 2].TranslatedText`) into `RetranslateParagraphAsync` (via an optional parameter) so both source and translated context are available.
  3. Add a unit test in `PromptBuilderTests.cs` / `TranslationPipelineTests.cs` verifying that `RetranslateParagraphAsync` includes `surroundingSourceContext` in the built prompt.
- **If we skip it:** Single-paragraph retranslation receives zero context from the preceding paragraph despite `MainViewModel` passing `surroundingContext`.

---

## 7. Editing in Paragraph Mode shifts translations onto the wrong paragraphs when any earlier paragraph is empty
**Location:**
- [EditorViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/EditorViewModel.cs#L107-L115)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L374-L378)

- **What this is:** Typing or retranslating paragraphs in Paragraph Mode (`จับคู่รายย่อหน้า`), where each source paragraph is paired by index with its translated paragraph.
- **Problem:** Every keystroke inside a paragraph triggers `OnParagraphContentChanged`, which sets `TranslatedText = Editor.CombineTranslatedParagraphs()`. `CombineTranslatedParagraphs()` (`L109`) calls `.Where(t => !string.IsNullOrEmpty(t))`, stripping all empty slots—including empty slots in the middle. If Paragraph 2 is empty (untranslated) and the user edits Paragraph 3, joining drops slot 2 so Paragraph 3's translation shifts up to slot 2 on the next `SyncParagraphPairs`.
- **Fix:** Preserve intermediate paragraph slots when combining (trim only trailing empty paragraphs, or keep empty slots up to the last non-empty translated index so `SplitParagraphs` / pairing stays aligned; only strip completely empty slots if the chapter has no trailing non-empty paragraphs). Add a unit test in `ParagraphAndEditorTests.cs`.
- **If we skip it:** Editing any partially translated chapter in Paragraph Mode shifts subsequent translations onto the wrong source paragraphs.
