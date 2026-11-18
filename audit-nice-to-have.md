# Ponytail Audit — Nice to Have (v7.1)

> **Scope:** Merging duplicate `ViewModel.PropertyChanged` subscriptions and eliminating double `settings.json` disk writes on startup and story switching.
> **Target:** Agent-actionable specification. Numbered 12 (continuing from `audit-should-fix.md` items 7–11).

---

## Summary Table

| # | Area | File | Core Issue |
|---|---|---|---|
| **12** | Duplicate `PropertyChanged` Handler & Double Settings Save | `src/NovelTranslator.App/Views/MainWindow.axaml.cs` / `MainViewModel.cs` | `MainWindow_Loaded` attaches two separate `ViewModel.PropertyChanged` handlers, and `OnSelectedStoryChanged` + `OnSelectedChapterChanged` write `settings.json` twice in a row on every startup and story change |

---

## 12. `MainWindow_Loaded` attaches two `ViewModel.PropertyChanged` handlers and writes `settings.json` twice on every startup and story switch
**Location:**
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L79)
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L622-L654)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L667-L702)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L753-L756)

- **What this is:** Property-change event wiring in `MainWindow_Loaded` and saving `LastSelectedStoryId` / `LastSelectedChapterId` when loading a story.
- **Problem:**
  1. `MainWindow_Loaded` subscribes `ViewModel.PropertyChanged += OnViewModelPropertyChanged;` at `L79` and a second anonymous `ViewModel.PropertyChanged += (s, e) => ...` at `L622`, both branching on `e.PropertyName == nameof(MainViewModel.SelectedChapter)`.
  2. `OnSelectedStoryChanged` (`MainViewModel.cs:L667-668`) sets `_settings.LastSelectedStoryId = value.Id; _settings.Save();` and then selects a chapter (`L696` or `L701`), which synchronously triggers `OnSelectedChapterChanged` (`L754-755`) and calls `_settings.Save()` a second time—performing two atomic temp-file writes and `File.Move` operations back-to-back on every app launch and story switch.
- **Fix:**
  1. Move the `TranslatedText` live-scroll and `SelectedChapter` ListBox sync blocks from the anonymous handler (`MainWindow.axaml.cs:L622-654`) into `OnViewModelPropertyChanged` (`L701-738`) so `MainWindow` has a single `PropertyChanged` subscription.
  2. In `MainViewModel.OnSelectedStoryChanged` (`L667-668`), set `_settings.LastSelectedStoryId = value.Id;` without calling `_settings.Save()` immediately, and let `OnSelectedChapterChanged` persist both `LastSelectedStoryId` and `LastSelectedChapterId` in a single `_settings.Save()` call.
- **If we skip it:** Every property change invokes two handlers on `MainWindow`, and every startup or story switch writes `settings.json` to disk twice.
