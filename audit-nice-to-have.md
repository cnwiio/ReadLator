# Ponytail Audit — Nice to Have (v5.8)

> **Scope:** Dead code deletion, input bounds clamping, and minor dialog UX polish.
> **Target:** Agent-actionable specification. Numbered 9–12 (continuing from `audit-should-fix.md` items 5–8).

---

## Summary Table

| # | Area | File | Core Issue |
|---|---|---|---|
| **9** | Dead Code Deletion (`-85` lines) | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `RetranslateParagraphAsync(string)`, `MoveChapterUpAsync()`, and `MoveChapterDownAsync()` are unused anywhere in XAML, code, or tests |
| **10** | Auto-Glossary Pre-flight Check | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `ScanNewTermsAsync` allows scanning when `TranslatedText` is empty or `OriginalText` is `DefaultPlaceholderText`, falsely reporting `"ไม่พบชื่อเฉพาะใหม่ในตอนนี้"` |
| **11** | `AutoGlossaryDialog` Theme & `Esc` | `src/NovelTranslator.App/Views/AutoGlossaryDialog.axaml` / `.cs` | `AutoGlossaryDialog` references non-existent `WindowBgBrush` instead of `MainBgBrush` and lacks an `Escape` key handler |
| **12** | Settings Numeric Clamping | `src/NovelTranslator.App/ViewModels/SettingsViewModel.cs` | `ApplyToSettings()` does not clamp `MaxChunkWords`, `ContextParagraphCount`, or `AutoSaveDelaySeconds` |

---

## 9. Delete 85 lines of dead commands in `MainViewModel`: `RetranslateParagraphAsync(string)`, `MoveChapterUpAsync()`, and `MoveChapterDownAsync()`
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1029-L1071)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1594-L1634)

- **What this is:** Legacy `[RelayCommand]` methods superseded by `RetranslateParagraphInPlaceAsync(ParagraphPairViewModel)` (`L1099`) and drag-and-drop `ReorderChaptersAsync` (`L1502`).
- **Problem:** Neither `RetranslateParagraphCommand`, `MoveChapterUpCommand`, nor `MoveChapterDownCommand` is referenced anywhere in `.axaml`, `.cs`, or `tests/`. In addition, `RetranslateParagraphAsync(string)` (`L1054`) passes the translated text as the prompt message to `RequestPromptDialog` (which shows an empty text input box underneath).
- **Fix:** Delete `RetranslateParagraphAsync(string)` (`L1029-1071`), `MoveChapterUpAsync()` (`L1594-1613`), and `MoveChapterDownAsync()` (`L1615-1634`).
- **If we skip it:** 85 lines of dead code and unused generated `RelayCommand` properties remain in `MainViewModel`.

---

## 10. `ScanNewTermsAsync` on an untranslated or placeholder chapter misleadingly reports `"ไม่พบชื่อเฉพาะใหม่ในตอนนี้"`
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1856-L1869)
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L117-L154)

- **What this is:** Clicking **สแกนหาศัพท์ใหม่ในตอนนี้ (AI)** (`ScanNewTermsCommand`).
- **Problem:** `GlossaryExtractor.ExtractNewTermsAsync` requires both `sourceText` and `translatedText` to be non-empty (`GlossaryExtractor.cs:L60`), returning an empty list immediately when `translatedText` is empty. However, `ScanNewTermsAsync` only checks `if (string.IsNullOrWhiteSpace(OriginalText))`, so clicking it on an untranslated chapter (or on `DefaultPlaceholderText`) enters `RequestOpenAutoGlossary`, queries SQLite, and displays `"ไม่พบชื่อเฉพาะใหม่ในตอนนี้"` instead of telling the user that the chapter must be translated first.
- **Fix:** In `ScanNewTermsAsync` (`MainViewModel.cs:L1859`), check:
  ```csharp
  if (string.IsNullOrWhiteSpace(OriginalText) || OriginalText.Trim() == DefaultPlaceholderText)
  {
      SaveStatusText = "ไม่มีข้อความต้นฉบับสำหรับสแกน";
      return;
  }
  if (string.IsNullOrWhiteSpace(TranslatedText))
  {
      SaveStatusText = "กรุณาแปลตอนนี้ก่อนสแกนหาคำศัพท์";
      return;
  }
  ```
- **If we skip it:** Users who click Auto-Glossary before translating a chapter get a confusing `"ไม่พบชื่อเฉพาะใหม่ในตอนนี้"` message.

---

## 11. `AutoGlossaryDialog` uses an undefined `WindowBgBrush` resource and has no `Escape` key handler
**Location:**
- [AutoGlossaryDialog.axaml](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/AutoGlossaryDialog.axaml#L10)
- [AutoGlossaryDialog.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/AutoGlossaryDialog.axaml.cs#L6-L17)

- **What this is:** The Auto-Glossary review dialog window.
- **Problem:** `AutoGlossaryDialog.axaml:L10` sets `Background="{DynamicResource WindowBgBrush}"`, a brush key that is never defined in `App.axaml` or `App.ApplyTheme` (all other windows use `MainBgBrush`). In addition, unlike `GlossaryWindow`, `SearchReplaceWindow`, and `SettingsWindow`, pressing `Escape` in `AutoGlossaryDialog` does not close the dialog.
- **Fix:** Change `WindowBgBrush` to `MainBgBrush` in [AutoGlossaryDialog.axaml](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/AutoGlossaryDialog.axaml#L10) and add `KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };` in [AutoGlossaryDialog.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/AutoGlossaryDialog.axaml.cs).
- **If we skip it:** `AutoGlossaryDialog` does not match the active theme background (especially in Sepia/Light modes) and cannot be dismissed with `Esc`.

---

## 12. `SettingsViewModel.ApplyToSettings()` does not clamp `MaxChunkWords`, `ContextParagraphCount`, or `AutoSaveDelaySeconds`
**Location:**
- [SettingsViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/SettingsViewModel.cs#L401-L437)

- **What this is:** Saving numeric configuration fields in Settings.
- **Problem:** While `BrightnessLevel` is clamped to `[30.0, 100.0]`, `MaxChunkWords`, `ContextParagraphCount`, and `AutoSaveDelaySeconds` are assigned directly without bounds checking. If a user sets `MaxChunkWords <= 0`, `Chunker.SplitIntoChunks` treats every paragraph as exceeding `maxWords` and splits the chapter into one chunk per sentence.
- **Fix:** Clamp the values in `ApplyToSettings()`:
  ```csharp
  _settings.MaxChunkWords = Math.Clamp(MaxChunkWords, 100, 4000);
  _settings.ContextParagraphCount = Math.Clamp(ContextParagraphCount, 0, 10);
  _settings.AutoSaveDelaySeconds = Math.Clamp(AutoSaveDelaySeconds, 0.5, 30.0);
  ```
- **If we skip it:** Invalid numeric values in Settings can split chapters into hundreds of 1-sentence LLM requests.
