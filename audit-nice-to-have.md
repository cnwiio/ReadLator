# Ponytail Audit — Nice to Have (v4.6)

## What this repo does
**NovelTranslator (ReadLator)** is a Windows desktop app (.NET 10, Avalonia UI 11, SQLite with FTS5, OpenAI-compatible SSE streaming) for translating foreign web novels into Thai using local or cloud LLMs, with smart chunking, glossary injection, paragraph alignment, and EPUB/TXT/URL import & export.
- **Assumed load**: Single desktop user, 1–50 stories, 100–3,000 chapters per story (~5,000–20,000 chars per chapter), glossaries of 100–2,000 entries, and streaming LLM responses over slow or unstable connections.
- **Graphify state**: 889 nodes, 2,044 edges, 42 communities (`graphify-out/graph.json`, updated at `f73d1f4` v4.6).

---

## Nice to have (cleanup, duplication, minor UX polish)

### 1. `src/NovelTranslator.App/ViewModels/MainViewModel.cs:174-185` & `src/NovelTranslator.App/ViewModels/SettingsViewModel.cs:193-204` — Duplicated `AvailableEditorFonts` list
- **What this is**: The 9-item list of selectable Thai/English editor fonts (`Sarabun`, `TH Sarabun New`, `Prompt`, etc.).
- **Problem**: Defined twice in `MainViewModel` and `SettingsViewModel`.
- **Fix**: Move the array to `public static IReadOnlyList<string> DefaultEditorFonts { get; }` on `AppSettings` and reference it from both ViewModels.
- **If we skip it**: Adding or renaming a font requires updating two files.

### 2. `src/NovelTranslator.App/Views/MainWindow.axaml.cs:87-88,107-108,146-147,879-880` — Repeated `new NovelDatabase()` + `new NovelRepository()` in dialog handlers
- **What this is**: Code-behind handlers that open the Glossary, Auto-Glossary, and Search & Replace windows.
- **Problem**: Four handlers in `MainWindow.axaml.cs` each instantiate a new `NovelDatabase()` and `NovelRepository()` instead of reusing the repository already created by `MainViewModel`.
- **Fix**: Expose `Repository` on `MainViewModel` (or construct child ViewModels via factory methods on `MainViewModel`) and pass that single instance.
- **If we skip it**: Minor code duplication in `MainWindow.axaml.cs`.

### 3. `src/NovelTranslator.Core/Llm/LlmClient.cs:55-68` — Missing fast-fail message when `ModelName` is empty
- **What this is**: Sends streaming chat completion requests (`StreamChatAsync`).
- **Problem**: If `_settings.ModelName` is empty (e.g., on first launch before selecting a model), `StreamChatAsync` still sends `"model": ""` to the server and surfaces a raw HTTP 400 JSON error.
- **Fix**: Guard at the start of `StreamChatAsync`: `if (string.IsNullOrWhiteSpace(_settings.ModelName)) throw new InvalidOperationException("กรุณาเลือกโมเดล AI ในหน้าตั้งค่าก่อนเริ่มแปล");`.
- **If we skip it**: New users who click Translate before choosing a model see a raw HTTP 400 error instead of a friendly prompt.

### 4. `src/NovelTranslator.App/NovelTranslator.App.csproj:11` — Empty `Models/` folder inclusion
- **What this is**: `<Folder Include="Models\" />` in `NovelTranslator.App.csproj`.
- **Problem**: `src/NovelTranslator.App/Models` is an empty directory because all domain models live in `NovelTranslator.Core.Models`.
- **Fix**: Delete the empty `src/NovelTranslator.App/Models` directory and remove `<Folder Include="Models\" />` from `NovelTranslator.App.csproj`.
- **If we skip it**: Harmless empty folder in IDE solution tree.

### 5. `tests/NovelTranslator.Tests/ResourceBenchmarkTests.cs:172` — Blocking `.Result` call triggers `xUnit1031` compiler warning
- **What this is**: A synchronous benchmark lambda calling `_repository.GetGlossaryByStoryAsync(_storyId).Result`.
- **Problem**: Emits compiler warning `xUnit1031: Test methods should not use blocking task operations, as they can cause deadlocks` on every test build.
- **Fix**: Use `.GetAwaiter().GetResult()` inside the synchronous `MeasureResource` action or add an `async` overload `MeasureResourceAsync(Func<Task>)`.
- **If we skip it**: Every `dotnet test` build prints an `xUnit1031` warning.

---

## Verdict
- **5 nice-to-have items** for maintainability and polish. See [audit-must-fix.md](file:///d:/Work/Other/ReadLator/audit-must-fix.md) and [audit-should-fix.md](file:///d:/Work/Other/ReadLator/audit-should-fix.md) for higher-priority findings.

## Not checked
- Dark/light theme visual contrast ratios across custom Avalonia styles.
