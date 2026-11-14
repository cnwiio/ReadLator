# Ponytail Audit — Should Fix (v5.5)

> **Scope:** Edge-case bugs, stale state in backups, and inconsistent cross-chapter context that degrade translation quality or user workflows.
> **Target:** Agent-actionable specification. Numbered 5–8 (continuing from `audit-must-fix.md` items 1–4).

---

## Summary Table

| # | Area | File | Core Issue |
|---|---|---|---|
| **5** | `.txt` Batch Import | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `ImportTxtFilesAsync` into a new story leaves the dummy `"Paste your English story here..."` Chapter 1 in place |
| **6** | Batch Translate All | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `TranslateAllChaptersAsync` translates the default English placeholder text if Chapter 1 was left untouched |
| **7** | Cross-Chapter Context | `src/NovelTranslator.Core/Data/NovelRepository.cs` | `GetPreviousTranslatedChapterAsync` pulls context from non-adjacent chapters (`Ordinal < CurrentOrdinal`) or partially translated chapters |
| **8** | Database Backup | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `BackupDatabaseAsync` copies the SQLite `.db` without flushing pending `_autoSaveTimer` edits first |

---

## 5. `ImportTxtFilesAsync` into a brand-new story leaves the dummy `"Paste your English story here..."` Chapter 1 in place
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1664-L1681)

- **What this is:** Batch `.txt` chapter import (`นำเข้าไฟล์นิยาย (.txt)`).
- **Problem:** When a new story is created, Chapter 1 (`ตอนที่ 1`) is pre-populated with `DefaultPlaceholderText` (`"Paste your English story here..."`). `ImportWebChapterAsync` checks for `DefaultPlaceholderText` and overwrites that starter chapter, but `ImportTxtFilesAsync` does not—it unconditionally starts at `int nextOrdinal = Chapters.Count > 0 ? Chapters.Max(c => c.Ordinal) + 1 : 1;`. Importing `.txt` files into a brand-new story leaves Chapter 1 with the English instructions and imports the user's first `.txt` file as Chapter 2.
- **Fix:** In `ImportTxtFilesAsync`, check if the first file can overwrite an untouched starter chapter (same condition as `ImportWebChapterAsync`: `Chapters.Count == 1 && (string.IsNullOrWhiteSpace(Chapters[0].OriginalText) || Chapters[0].OriginalText.Trim() == DefaultPlaceholderText) && string.IsNullOrWhiteSpace(Chapters[0].TranslatedText)`), update `Chapters[0]` for the first imported file, and select the first imported chapter afterwards.
- **If we skip it:** Every user who creates a new story and imports `.txt` chapters gets a dummy Chapter 1 (`"Paste your English story here..."`) ahead of their actual Chapter 1.

---

## 6. `TranslateAllChaptersAsync` translates the English starter placeholder text if Chapter 1 was left untouched
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L819-L835)

- **What this is:** Batch translation of all untranslated chapters (`แปลทุกตอนที่เหลือ`).
- **Problem:** Line 823 selects pending chapters using:
  ```csharp
  var pending = Chapters.Where(c => c.Status == ChapterStatus.NotTranslated && !string.IsNullOrWhiteSpace(c.OriginalText)).ToList();
  ```
  Unlike `StartTranslationAsync` (which blocks translating `DefaultPlaceholderText` at line 798), `TranslateAllChaptersAsync` does not filter out `c.OriginalText.Trim() == DefaultPlaceholderText`. If Chapter 1 still contains the starter placeholder, batch translation sends the placeholder instructions to the LLM API.
- **Fix:** Add `&& c.OriginalText.Trim() != DefaultPlaceholderText` to the `pending` filter in `TranslateAllChaptersAsync`.
- **If we skip it:** Batch translation wastes API tokens translating the app's own tutorial placeholder text.

---

## 7. Cross-chapter context (`GetPreviousTranslatedChapterAsync`) pulls from non-adjacent chapters or mismatched partial translations
**Location:**
- [NovelRepository.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Data/NovelRepository.cs#L78-L90)
- [TranslationPipeline.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/TranslationPipeline.cs#L99-L104)

- **What this is:** Cross-chapter context continuity—injecting the last `ContextParagraphCount` source and translated paragraphs of the previous chapter into Chunk 1 of the current chapter.
- **Problem:** `GetPreviousTranslatedChapterAsync` runs:
  ```sql
  SELECT * FROM Chapters
  WHERE StoryId = @StoryId AND Ordinal < @CurrentOrdinal AND TRIM(TranslatedText) != ''
  ORDER BY Ordinal DESC LIMIT 1;
  ```
  This causes two subtle bugs:
  1. **Non-adjacent chapter:** If Chapter 1 is translated, Chapter 2 is untranslated, and the user translates Chapter 3, `Ordinal < @CurrentOrdinal` returns **Chapter 1** and tells the LLM those paragraphs immediately preceded Chapter 3.
  2. **Partial/interrupted chapter:** If Chapter 1 was stopped halfway (`Status == ChapterStatus.Translating` or `NotTranslated` with partial `TranslatedText`), `TranslationPipeline` takes `GetLastParagraphs(prevChapter.OriginalText, count)` (the end of Chapter 1's source text) and pairs it with `GetLastParagraphs(prevChapter.TranslatedText, count)` (the middle of Chapter 1's translation)—feeding mismatched source/translation pairs into the prompt.
- **Fix:** Restrict the query in `GetPreviousTranslatedChapterAsync` to the immediately preceding completed chapter:
  ```sql
  SELECT * FROM Chapters
  WHERE StoryId = @StoryId AND Ordinal = @CurrentOrdinal - 1 AND Status IN (2, 3) AND TRIM(TranslatedText) != ''
  LIMIT 1;
  ```
  (`2 = Translated`, `3 = Edited`).
- **If we skip it:** Translating chapters out of order or after cancelling a previous chapter feeds mismatched source and translation paragraphs into the AI prompt.

---

## 8. `BackupDatabaseAsync` does not flush pending auto-save edits before copying the SQLite database
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1850-L1858)

- **What this is:** Exporting a full SQLite `.db` backup file (`สำรองฐานข้อมูล (.db)`).
- **Problem:** If a user edits a chapter and immediately clicks **สำรองฐานข้อมูล (.db)** before `_autoSaveTimer` ticks (or if auto-save is disabled in Settings), `BackupDatabaseAsync` calls `_database.BackupTo(path)` directly without saving the in-memory edits on `SelectedChapter`.
- **Fix:** At the start of `BackupDatabaseAsync`, if `_autoSaveTimer.IsEnabled` (or if `SelectedChapter != null`), stop `_autoSaveTimer` and `await SaveCurrentChapterInternalAsync(isAutoSave: true);` before calling `_database.BackupTo(path)`.
- **If we skip it:** Backing up right after editing produces a `.db` file missing the latest chapter changes.
