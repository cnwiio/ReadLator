# Ponytail Audit — Should Fix (v5.2)

## What this repo does
**NovelTranslator (ReadLator)** is a Windows desktop app (.NET 10, Avalonia UI, SQLite with FTS5, WebView2, OpenAI-compatible SSE streaming) that translates foreign web novels into Thai using local or cloud AI models. It supports smart chunking, glossary injection, paragraph-by-paragraph editing, and importing chapters from `.txt` files, URLs (Reader Mode), or an incognito WebView2 browser.
- **Assumed load**: Single desktop user, 1–50 stories, 100–3,000 chapters per story (~5,000–20,000 characters per chapter), glossaries of 100–2,000 terms, and streaming AI responses over slow or unstable connections.
- **Graphify state**: 1,039 nodes, 2,494 edges, 60 communities (`graphify-out/graph.json`, updated at `8b4e793` v5.2).

---

## Should fix (risky code without a test, real slowness, duplication, dead code)

5. **Importing a URL into a brand-new story leaves the English placeholder in Chapter 1 and creates Chapter 2** (`src/NovelTranslator.App/ViewModels/MainViewModel.cs:L614-625`, `L1714-1747`)
   - **What this is:** When a new story has no chapters, `OnSelectedStoryChanged` creates `"ตอนที่ 1"` with placeholder text `"Paste your English story here...\n\nThen click the 'แปล' button!"`.
   - **Problem:** `ImportWebChapterAsync` only reuses the current chapter when `string.IsNullOrWhiteSpace(OriginalText)` is true (`L1718`), and there is no unit test for `ImportWebChapterAsync`. Because Chapter 1 contains the starter placeholder text, importing a chapter from a URL into a new story leaves dummy Chapter 1 untouched and inserts the imported chapter as Chapter 2.
   - **Fix:** Extract a constant `DefaultPlaceholderText = "Paste your English story here...\n\nThen click the 'แปล' button!"` and treat `OriginalText.Trim() == DefaultPlaceholderText` the same as empty in `ImportWebChapterAsync`, plus add a test in `NewFeaturesTests.cs`.
   - **If we skip it:** Every new story that imports its first chapter from a URL ends up with a junk `"ตอนที่ 1"` ahead of the real first chapter.

6. **Whole-story Search Preview caps FTS5 at 100 chapters and loads all chapters into RAM when 0 matches are found** (`src/NovelTranslator.App/ViewModels/SearchReplaceViewModel.cs:L68-84`, `L118-120`)
   - **What this is:** Searches all chapters in a story (`ScopeIndex == 1`) before running Search & Replace.
   - **Problem:** Line 70 calls `_repository.SearchFtsAsync(_story.Id, SearchText, searchTranslated: true, limit: 100)`, which caps preview results at 100 chapters even though `ApplyReplaceAsync` (`L120`) modifies all chapters in the story. Worse, when `ftsResults.Count == 0` (the search word does not exist anywhere), lines 80–83 fall back to `GetChaptersByStoryAsync(_story.Id)` and load every chapter in the novel into memory anyway, even though `SearchFtsAsync` already ran a `LIKE` fallback query internally.
   - **Fix:** Remove the `else { chapters = await _repository.GetChaptersByStoryAsync(_story.Id); }` branch (use an empty list when `ftsResults.Count == 0`) and increase `limit` in `SearchFtsAsync` to `10000`.
   - **If we skip it:** Searching for a word with 0 matches needlessly reads the entire novel from SQLite into memory, and novels with >100 matching chapters preview fewer chapters than `ApplyReplaceAsync` actually overwrites.

7. **Dead class: `TranslationQueueViewModel` is instantiated on `MainViewModel` but never used** (`src/NovelTranslator.App/ViewModels/TranslationQueueViewModel.cs:L1-34`, `src/NovelTranslator.App/ViewModels/MainViewModel.cs:L35`)
   - **What this is:** A sub-ViewModel holding duplicate translation progress and error properties (`IsTranslating`, `ProgressText`, `HasError`).
   - **Problem:** `MainViewModel` creates `public TranslationQueueViewModel Queue { get; } = new();` at line 35, but neither `MainViewModel.cs` nor any `.axaml` file ever reads or writes `Queue`.
   - **Fix:** Delete `src/NovelTranslator.App/ViewModels/TranslationQueueViewModel.cs` and remove `public TranslationQueueViewModel Queue { get; } = new();` from `MainViewModel.cs`.
   - **If we skip it:** 35 lines of dead code duplicate state that actually lives on `MainViewModel`.

8. **Dead members on `EditorViewModel`: `SelectedParagraphPair` and `QuickUpdateGlossaryEntryAsync`** (`src/NovelTranslator.App/ViewModels/EditorViewModel.cs:L25-26`, `L141-146`)
   - **What this is:** Unused property and method on `EditorViewModel`.
   - **Problem:** Grep across the entire repository shows zero callers or XAML bindings for `SelectedParagraphPair` or `QuickUpdateGlossaryEntryAsync`.
   - **Fix:** Delete lines 25–26 and 141–146 from `EditorViewModel.cs`.
   - **If we skip it:** Dead code stays in `EditorViewModel` with no callers or tests.

9. **`Microsoft.Web.WebView2` transitive WPF reference causes 22 lines of `MSB3277` `WindowsBase` warnings on every build, and `Models\` folder is empty** (`src/NovelTranslator.App/NovelTranslator.App.csproj:L11`, `L26`)
   - **What this is:** NuGet package references and folder items in `NovelTranslator.App.csproj`.
   - **Problem:** `Microsoft.Web.WebView2` injects `Microsoft.Web.WebView2.Wpf.dll` and `Microsoft.Web.WebView2.WinForms.dll` via its `.targets` file, causing MSBuild warning `MSB3277: Found conflicts between different versions of "WindowsBase"` on both `NovelTranslator.App` and `NovelTranslator.Tests`, even though the app only uses `Microsoft.Web.WebView2.Core`. Also, `<Folder Include="Models\" />` at line 11 points to an empty folder.
   - **Fix:** Remove `<Folder Include="Models\" />` (and the empty `src/NovelTranslator.App/Models` directory), and exclude the unused `Microsoft.Web.WebView2.Wpf` and `Microsoft.Web.WebView2.WinForms` references in `NovelTranslator.App.csproj`.
   - **If we skip it:** Every build prints 22 lines of `MSB3277` assembly conflict warnings and copies unused WPF/WinForms wrapper DLLs into the output folder.

10. **Installer scripts hardcode version `5.0.0` instead of `5.2.0`** (`installer/build-velopack.ps1:L2`, `installer/build-velopack.sh:L4`, `installer/setup-inno.iss:L6`)
    - **What this is:** Build and packaging scripts for Velopack and Inno Setup installers.
    - **Problem:** `NovelTranslator.App.csproj` is at version `5.2.0`, but all three installer scripts hardcode `5.0.0` as their default version. Running `./installer/build-velopack.ps1` without arguments packages the `5.2.0` binary as `5.0.0`.
    - **Fix:** Read `<Version>` directly from `src/NovelTranslator.App/NovelTranslator.App.csproj` in `build-velopack.ps1` and `build-velopack.sh` when no version argument is supplied, and update `MyAppVersion` in `setup-inno.iss` to `"5.2.0"`.
    - **If we skip it:** Default installer builds produce packages stamped with the wrong version number (`5.0.0`), breaking Velopack update comparisons.

---

## Verdict
- **6 should-fix items** covering starter-chapter URL import, FTS5 0-match fallback, dead ViewModel code, WebView2 `MSB3277` build warnings, and installer version drift. See [audit-must-fix.md](file:///d:/Work/Other/ReadLator/audit-must-fix.md) (items 1–4) and [audit-nice-to-have.md](file:///d:/Work/Other/ReadLator/audit-nice-to-have.md) (items 11–14).

## Lean
- `Lean: -350 lines, -0 dependencies possible` across all three files.

## Not checked
- Real-world latency differences across external LLM API providers.
