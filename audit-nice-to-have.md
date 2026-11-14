# Ponytail Audit — Nice to Have (v5.5)

> **Scope:** UI feedback accuracy, duplicate glossary entry prevention, and single-paragraph retranslation context.
> **Target:** Agent-actionable specification. Numbered 9–12 (continuing from `audit-should-fix.md` items 5–8).

---

## Summary Table

| # | Area | File | Core Issue |
|---|---|---|---|
| **9** | Auto-Glossary Cancel Status | `src/NovelTranslator.App/Views/MainWindow.axaml.cs` | Cancelling `AutoGlossaryDialog` still shows `"อัปเดตคำศัพท์เข้า Glossary เรียบร้อยแล้ว"` and never refreshes `Editor.ActiveGlossary` |
| **10** | Glossary Duplicate Terms | `src/NovelTranslator.App/ViewModels/GlossaryViewModel.cs` | `SaveEntryAsync` allows inserting duplicate `SourceTerm` rows when `SelectedEntry == null` |
| **11** | In-Place Paragraph Retranslate | `src/NovelTranslator.App/ViewModels/MainViewModel.cs` | `RetranslateParagraphInPlaceAsync` passes `null` for `surroundingSourceContext` instead of the preceding paragraph |
| **12** | Auto-Glossary Checkbox Sync | `src/NovelTranslator.App/ViewModels/AutoGlossaryViewModel.cs` | `ExtractedTerm.IsSelected` does not raise `PropertyChanged`, so `SelectAll` / `DeselectAll` does not visually update the DataGrid checkboxes |

---

## 9. Cancelling `AutoGlossaryDialog` still shows `"อัปเดตคำศัพท์เข้า Glossary เรียบร้อยแล้ว"` and never refreshes `Editor.ActiveGlossary`
**Location:**
- [MainWindow.axaml.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/Views/MainWindow.axaml.cs#L141-L147)

- **What this is:** Opening the AI-extracted glossary review dialog (`RequestOpenAutoGlossary`).
- **Problem:** In `MainWindow.axaml.cs`:
  ```csharp
  var autoVm = new AutoGlossaryViewModel(ViewModel.Repository, ViewModel.CurrentStoryId, terms);
  var dialog = new AutoGlossaryDialog(autoVm);
  await dialog.ShowDialog(this);
  if (ViewModel != null)
      ViewModel.SaveStatusText = "อัปเดตคำศัพท์เข้า Glossary เรียบร้อยแล้ว";
  ```
  Even if the user clicks **ยกเลิก** (`Cancel`) or closes the dialog without saving, the status bar still displays `"อัปเดตคำศัพท์เข้า Glossary เรียบร้อยแล้ว"`. Conversely, when the user *does* save terms, `ViewModel.Editor.RefreshActiveGlossaryAsync` is not called to update the sidebar's active glossary highlights.
- **Fix:** Wire `autoVm.OnTermsAdded`:
  ```csharp
  bool added = false;
  autoVm.OnTermsAdded = () => added = true;
  var dialog = new AutoGlossaryDialog(autoVm);
  await dialog.ShowDialog(this);
  if (added && ViewModel != null)
  {
      ViewModel.SaveStatusText = "อัปเดตคำศัพท์เข้า Glossary เรียบร้อยแล้ว";
      await ViewModel.Editor.RefreshActiveGlossaryAsync(ViewModel.CurrentStoryId, ViewModel.OriginalText);
  }
  ```
- **If we skip it:** Cancelling Auto-Glossary shows a false success message, and saving new terms does not immediately appear in the active glossary sidebar.

---

## 10. `GlossaryViewModel.SaveEntryAsync` allows inserting duplicate `SourceTerm` entries
**Location:**
- [GlossaryViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/GlossaryViewModel.cs#L103-L118)

- **What this is:** Adding a term manually in the Glossary management dialog.
- **Problem:** While `AutoGlossaryViewModel` and CSV import deduplicate against existing terms, `GlossaryViewModel.SaveEntryAsync` inserts a brand-new row whenever `SelectedEntry == null`, even if `_allEntries` already has an entry with the exact same `SourceTerm` (case-insensitive).
- **Fix:** When `SelectedEntry == null`, check `_allEntries.FirstOrDefault(e => e.SourceTerm.Equals(SourceTerm.Trim(), StringComparison.OrdinalIgnoreCase))` first—if a match exists, update that existing `GlossaryEntry` instead of inserting a duplicate row.
- **If we skip it:** Users can accidentally add two conflicting translations for the same character name or term.

---

## 11. In-place paragraph retranslation (`RetranslateParagraphInPlaceAsync`) does not pass surrounding paragraph context
**Location:**
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1027-L1035)
- [MainViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/MainViewModel.cs#L1097-L1101)

- **What this is:** Re-translating a single paragraph via **แปลย่อหน้านี้ใหม่ ↻** in Paragraph Mode or the Retranslate Dialog.
- **Problem:** `TranslationPipeline.RetranslateParagraphAsync` accepts `string? surroundingSourceContext = null` and includes it in the prompt (`บริบทรอบข้าง (อ้างอิงเท่านั้น ห้ามแปล):`), but both call sites in `MainViewModel.cs` pass `null`.
- **Fix:** In `RetranslateParagraphInPlaceAsync(ParagraphPairItem? pair)`, pass the preceding paragraph's `SourceText` (e.g. `pair.Index > 1 && pair.Index - 2 < Editor.ParagraphPairs.Count ? Editor.ParagraphPairs[pair.Index - 2].SourceText : null`) as `surroundingSourceContext`.
- **If we skip it:** Re-translating a short line of dialogue by itself has no context from the preceding paragraph to know who is speaking.

---

## 12. `AutoGlossaryDialog` `SelectAll` / `DeselectAll` does not notify the DataGrid checkboxes
**Location:**
- [AutoGlossaryViewModel.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.App/ViewModels/AutoGlossaryViewModel.cs#L40-L58)
- [AutoGlossaryExtractor.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Translation/AutoGlossaryExtractor.cs#L14-L21)

- **What this is:** The **เลือกทั้งหมด** (`SelectAll`) and **ไม่เลือกเลย** (`DeselectAll`) buttons and selected-term counter in `AutoGlossaryDialog`.
- **Problem:** `ExtractedTerm` is a plain class that does not implement `INotifyPropertyChanged`. When `SelectAll()` or `DeselectAll()` mutates `t.IsSelected = true/false`, Avalonia's `DataGridCheckBoxColumn` receives no property change notification, so the checkboxes on screen do not visually update. Similarly, clicking an individual checkbox in the grid does not trigger `UpdateStatus()` to update `"พบศัพท์ใหม่ X คำ (เลือก Y คำ)"`.
- **Fix:** Implement `INotifyPropertyChanged` on `ExtractedTerm` for `IsSelected` (or wrap/subscribe in `AutoGlossaryViewModel` so changing `IsSelected` raises `PropertyChanged` and calls `UpdateStatus()`).
- **If we skip it:** Clicking **เลือกทั้งหมด** / **ไม่เลือกเลย** updates the text counter at the bottom, but the checkboxes inside the DataGrid stay visually unchanged until scrolled.
