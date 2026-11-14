# Ponytail Audit — Nice to Have (v5.2)

## What this repo does
**NovelTranslator (ReadLator)** is a Windows desktop app (.NET 10, Avalonia UI, SQLite with FTS5, WebView2, OpenAI-compatible SSE streaming) that translates foreign web novels into Thai using local or cloud AI models. It supports smart chunking, glossary injection, paragraph-by-paragraph editing, and importing chapters from `.txt` files, URLs (Reader Mode), or an incognito WebView2 browser.
- **Assumed load**: Single desktop user, 1–50 stories, 100–3,000 chapters per story (~5,000–20,000 characters per chapter), glossaries of 100–2,000 terms, and streaming AI responses over slow or unstable connections.
- **Graphify state**: 1,039 nodes, 2,494 edges, 60 communities (`graphify-out/graph.json`, updated at `8b4e793` v5.2).

---

## Nice to have (small speed-ups, shorter forms, minor UX polish)

11. **Three inline dialog builders in `MainWindow.axaml.cs` copy-paste ~250 lines of identical window chrome** (`src/NovelTranslator.App/Views/MainWindow.axaml.cs:L196-583`)
    - **What this is:** `RequestPromptDialog`, `RequestConfirmDialog`, and `RequestShowErrorDialog` build borderless modal dialogs in C#.
    - **Problem:** Each handler repeats the same 80-line setup for the transparent `Window`, outer `Border` card with shadow, draggable header `Grid`, `✕` close button, and `Escape` key handler.
    - **Fix:** Extract one small helper method `CreateModalDialog(string title, double width, Control body, params Button[] buttons)` in `MainWindow.axaml.cs` and reuse it in all three handlers (~250 fewer lines).
    - **If we skip it:** Any style tweak to modal dialogs must be edited in three places inside `MainWindow.axaml.cs`.

12. **`GlossaryViewModel.ExportCsv` exports only the filtered list instead of all story terms** (`src/NovelTranslator.App/ViewModels/GlossaryViewModel.cs:L163-167`)
    - **What this is:** Exports the story's glossary to a `.csv` file when the user clicks Export CSV.
    - **Problem:** `ExportCsv` passes `Entries` (the currently filtered `ObservableCollection`) instead of `_allEntries`. If the user has text in the search box when clicking Export, non-matching glossary entries are silently left out of the CSV file.
    - **Fix:** Change `GlossaryService.ExportToCsv(filePath, Entries);` to `GlossaryService.ExportToCsv(filePath, _allEntries);`.
    - **If we skip it:** Exporting a glossary while a search filter is typed exports an incomplete CSV.

13. **`TranslationPipeline` allocates full strings and word arrays on every 3rd streamed token** (`src/NovelTranslator.Core/Translation/TranslationPipeline.cs:L160-180`)
    - **What this is:** Computes `StreamedWordCount` and `CombinedTranslatedTextSoFar` while streaming tokens from the AI model.
    - **Problem:** Every 3rd token (`tokenCount % 3 == 0`), line 162 calls `chunkSb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length`, allocating a full copy of the chunk string and a `string[]` array hundreds of times per chunk.
    - **Fix:** Throttle progress reports by elapsed time (e.g., at most once every 80 ms using `Environment.TickCount64`) or use `Chunker.EstimateWordCount` only when reporting.
    - **If we skip it:** Extra short-lived string and array allocations occur during fast token streaming.

14. **`FirstRunWindow` constructor triggers `OnBackendSelectionChanged` before loading saved `BaseUrl`** (`src/NovelTranslator.App/Views/FirstRunWindow.axaml.cs:L23-31`, `L42-51`)
    - **What this is:** Initializes the First-Run Setup window with the current `AppSettings`.
    - **Problem:** Setting `BackendComboBox.SelectedIndex` in the constructor fires `OnBackendSelectionChanged`, which overwrites `BaseUrlBox.Text` with a hardcoded default URL, and if a user later switches providers back and forth in the dialog, any custom port they typed is immediately overwritten.
    - **Fix:** Only overwrite `BaseUrlBox.Text` in `OnBackendSelectionChanged` when the current text is empty or matches one of the three built-in default URLs (matching the pattern used in `SettingsViewModel.OnBackendIndexChanged`).
    - **If we skip it:** Switching the provider dropdown in `FirstRunWindow` wipes out a custom Base URL that the user already typed.

---

## Verdict
- **4 nice-to-have items** for leaner code and smoother edge-case UX. See [audit-must-fix.md](file:///d:/Work/Other/ReadLator/audit-must-fix.md) (items 1–4) and [audit-should-fix.md](file:///d:/Work/Other/ReadLator/audit-should-fix.md) (items 5–10) for higher-priority findings.

## Lean
- `Lean: -350 lines, -0 dependencies possible` across all three files.

## Not checked
- Dark/light/sepia theme contrast ratios across custom Avalonia controls.
