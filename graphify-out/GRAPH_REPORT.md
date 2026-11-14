# Graph Report - ReadLator  (2026-11-14)

## Corpus Check
- 78 files · ~50,472 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 17 file(s) not represented in the graph (top: .axaml 9, (none) 2, .ttf 2)

## Summary
- 1039 nodes · 2494 edges · 60 communities (36 shown, 24 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 326 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `8b4e7939`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- MainViewModel
- App UI & ViewModels (2)
- List
- .FetchReadableChapterAsync
- .InsertChapterAsync
- Story
- App UI & ViewModels (5)
- App UI & ViewModels (6)
- story.mjs
- MainWindow
- AppSettings
- AutoGlossaryViewModel
- SettingsViewModel
- RoutedEventArgs
- RelayCommand
- TranslationPipeline
- Documentation & Specifications
- Unslop
- NovelTranslator Features & Implementation Roadmap (Phases 1-6)
- Getting Started & Run Commands
- Tokens
- Signs of slop
- Task
- Chapter
- .StartTranslationInternalAsync
- ShortkeysSettings
- GlossaryViewModel
- unslop/SKILL.md
- Effects
- Visuals
- interface
- WebView2Host
- auto_update_and_commit.md
- Should fix (missing tests on risky code, slowness, dead code)
- Ponytail Audit — Must Fix (v4.6)
- .Benchmark_AllComponents_ResourceUsageComparison
- Nice to have (cleanup, duplication, minor UX polish)
- .MainWindow_Loaded
- SettingsWindow
- .InitializeMainWindowAsync
- ExtractedTerm
- TranslationProgressInfo
- GlossaryEntry
- .Save
- FtsSearchResult
- .BuildAvaloniaApp
- Fact
- BackendType
- .SyncParagraphPairs
- .BackupDatabaseAsync
- ChapterStatus
- TranslationAppearanceMode
- build-velopack.sh script
- .CopyErrorDetailsAsync
- .OpenSearchReplaceAsync

## God Nodes (most connected - your core abstractions)
1. `MainViewModel` - 163 edges
2. `Chapter` - 67 edges
3. `SettingsViewModel` - 58 edges
4. `Story` - 47 edges
5. `AppSettings` - 47 edges
6. `MainWindow` - 46 edges
7. `NovelRepository` - 41 edges
8. `GlossaryEntry` - 30 edges
9. `NovelTranslator.Core.Models` - 26 edges
10. `EditorViewModel` - 24 edges

## Surprising Connections (you probably didn't know these)
- `Lean (what stays as-is)` --references--> `App`  [INFERRED]
  audit-must-fix.md → src/NovelTranslator.App/App.axaml.cs
- `1. `src/NovelTranslator.App/ViewModels/MainViewModel.cs:174-185` & `src/NovelTranslator.App/ViewModels/SettingsViewModel.cs:193-204` — Duplicated `AvailableEditorFonts` list` --references--> `MainViewModel`  [INFERRED]
  audit-nice-to-have.md → src/NovelTranslator.App/ViewModels/MainViewModel.cs
- `2. `src/NovelTranslator.App/Views/MainWindow.axaml.cs:87-88,107-108,146-147,879-880` — Repeated `new NovelDatabase()` + `new NovelRepository()` in dialog handlers` --references--> `MainViewModel`  [INFERRED]
  audit-nice-to-have.md → src/NovelTranslator.App/ViewModels/MainViewModel.cs
- `1. `src/NovelTranslator.App/ViewModels/MainViewModel.cs:174-185` & `src/NovelTranslator.App/ViewModels/SettingsViewModel.cs:193-204` — Duplicated `AvailableEditorFonts` list` --references--> `SettingsViewModel`  [INFERRED]
  audit-nice-to-have.md → src/NovelTranslator.App/ViewModels/SettingsViewModel.cs
- `2. `src/NovelTranslator.App/Views/MainWindow.axaml.cs:87-88,107-108,146-147,879-880` — Repeated `new NovelDatabase()` + `new NovelRepository()` in dialog handlers` --references--> `NovelDatabase`  [INFERRED]
  audit-nice-to-have.md → src/NovelTranslator.Core/Data/NovelDatabase.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **NovelTranslator Specification System** — readme_noveltranslator, spec_system_specification, spec_tech_stack, spec_phases [EXTRACTED 1.00]
- **Ponytail Skill Suite** — _agents_skills_ponytail_skill_ponytail_skill, _agents_skills_ponytail_review_skill_ponytail_review, _agents_skills_ponytail_audit_skill_ponytail_audit, _agents_skills_ponytail_debt_skill_ponytail_debt, _agents_skills_ponytail_gain_skill_ponytail_gain, _agents_skills_ponytail_help_skill_ponytail_help [EXTRACTED 1.00]
- **Shortcut Debt Lifecycle** — agents_shortcut_convention, _agents_skills_ponytail_skill_the_ladder, _agents_skills_ponytail_debt_skill_debt_ledger, _agents_skills_ponytail_audit_skill_ponytail_audit [INFERRED 0.85]

## Communities (60 total, 24 thin omitted)

### Community 0 - "MainViewModel"
Cohesion: 0.04
Nodes (44): MainViewModel, AvailableEditorFonts, BrightnessDisplayText, CanUndoDeleteChapter, Chapters, ChapterSearchFilter, ChapterStatusFilterIndex, DimOverlayOpacity (+36 more)

### Community 1 - "App UI & ViewModels (2)"
Cohesion: 0.08
Nodes (12): NovelTranslator.Tests, NovelTranslator.App, NovelTranslator.Core.Llm, NovelTranslator.Core.Glossary, NovelTranslator.Core.Models, NovelTranslator.App.ViewModels, NovelTranslator.Core.Translation, NovelTranslator.App.Views (+4 more)

### Community 2 - "List"
Cohesion: 0.06
Nodes (13): GlossaryService, ChatMessage, Content, Role, ModelInfo, Id, Name, LlmClient (+5 more)

### Community 3 - ".FetchReadableChapterAsync"
Cohesion: 0.12
Nodes (4): SmartChapterNaming, WebChapterImportService, WebChapterResult, WebChapterImportServiceTests

### Community 5 - "Story"
Cohesion: 0.14
Nodes (9): 2. `src/NovelTranslator.App/ViewModels/SearchReplaceViewModel.cs:62-84` — Search preview loads all chapters before checking FTS5 scope and undercounts matches, Story, CreatedAt, Id, SourceLang, StyleNotes, TargetLang, Title (+1 more)

### Community 6 - "App UI & ViewModels (5)"
Cohesion: 0.08
Nodes (22): Avalonia (12.1.6), Avalonia.Controls.DataGrid (12.1.2), Avalonia.Desktop (12.1.6), Avalonia.Fonts.Inter (12.1.6), Avalonia.Themes.Fluent (12.1.6), AvaloniaUI.DiagnosticsSupport (2.2.3), CommunityToolkit.Mvvm (8.4.2), coverlet.collector (6.0.4) (+14 more)

### Community 7 - "App UI & ViewModels (6)"
Cohesion: 0.13
Nodes (15): Quality Audit Order of Importance, Ponytail Audit Skill, Shortcut Debt Ledger, Ponytail Debt Skill, Ponytail Gain Skill, Ponytail Benchmark Scoreboard, Ponytail Help Skill, Ponytail Review Skill (+7 more)

### Community 8 - "story.mjs"
Cohesion: 0.05
Nodes (25): args, chrome, positionals, args, chrome, file, frame, frames (+17 more)

### Community 10 - "AppSettings"
Cohesion: 0.10
Nodes (20): AppSettings, ApiKey, AutoSaveDelaySeconds, Backend, BaseUrl, BrightnessLevel, ContextParagraphCount, CustomSystemPrompt (+12 more)

### Community 11 - "AutoGlossaryViewModel"
Cohesion: 0.05
Nodes (30): AutoGlossaryViewModel, ExtractedTerms, OnTermsAdded, RequestClose, EditorViewModel, ActiveGlossary, IsUpdatingFromParagraph, OnParagraphContentChanged (+22 more)

### Community 12 - "SettingsViewModel"
Cohesion: 0.06
Nodes (20): SettingsViewModel, ApiKeyPasswordChar, AvailableEditorFonts, AvailableModels, CurrentFontFamily, FocusModeKeyDisplay, FullScreenKeyDisplay, IsAiCategorySelected (+12 more)

### Community 13 - "RoutedEventArgs"
Cohesion: 0.18
Nodes (3): AutoGlossaryDialog, BrowserImportWindow, SearchReplaceWindow

### Community 14 - "RelayCommand"
Cohesion: 0.08
Nodes (5): RequestOpenAutoGlossary, RequestOpenBrowserImport, RequestOpenGlossary, RequestOpenSettings, RequestShowErrorDialog

### Community 15 - "TranslationPipeline"
Cohesion: 0.18
Nodes (7): 1. `src/NovelTranslator.Core/Translation/TranslationPipeline.cs:26-235` — Core `TranslationPipeline` has zero unit tests, ILlmClient, TranslationPipeline, FakeStreamingLlmClient, QueueLlmClient, CallCount, TranslationPipelineTests

### Community 16 - "Documentation & Specifications"
Cohesion: 0.50
Nodes (3): NovelTranslator Project, System Specification, .NET 10 & Avalonia Tech Stack

### Community 17 - "Unslop"
Cohesion: 0.12
Nodes (16): 1. Spacing, 2. Color, 3. Icons, 4. Copy, 5. Type, 6. Layout, 7. Details, 8. Visuals (+8 more)

### Community 20 - "Tokens"
Cohesion: 0.14
Nodes (14): Accent, App layout, Buttons, Dark, Depth, Icons, Light, Motion (+6 more)

### Community 21 - "Signs of slop"
Cohesion: 0.18
Nodes (11): Components, Copy, Icons, Landing pages and heroes, Motion, Signs of slop, Spacing, Surfaces and color (+3 more)

### Community 23 - "Chapter"
Cohesion: 0.08
Nodes (16): RequestPromptDialog, TextExportImportService, Chapter, EditingTitle, Id, IsEditingTitle, IsSelected, Ordinal (+8 more)

### Community 25 - "ShortkeysSettings"
Cohesion: 0.18
Nodes (10): ShortkeysSettings, FocusModeKey, FullScreenKey, NextChapterKey, PrevChapterKey, SaveKey, SearchReplaceKey, ToggleSidebarKey (+2 more)

### Community 26 - "GlossaryViewModel"
Cohesion: 0.20
Nodes (5): GlossaryViewModel, Entries, RequestClose, GlossaryWindow, ViewModel

### Community 28 - "Effects"
Cohesion: 0.29
Nodes (7): Dithering, Duotone, Effects, Glyphs, Halftone dots, Implementation, Performance

### Community 29 - "Visuals"
Cohesion: 0.29
Nodes (7): Decide what the visual is, Generate it, Objects for moments, Prompt recipe, Put it on the page, Styles that work, Visuals

### Community 31 - "interface"
Cohesion: 0.40
Nodes (4): interface, default_prompt, display_name, short_description

### Community 34 - "Should fix (missing tests on risky code, slowness, dead code)"
Cohesion: 0.17
Nodes (11): 3. `src/NovelTranslator.App/ViewModels/MainViewModel.cs:811-820` — Fake progress timer clamps total progress to 45%, jumping backwards on chunk 2+, 4. `src/NovelTranslator.App/ViewModels/MainViewModel.cs:1519` — Batch `.txt` import sorts filenames asciibetically instead of naturally, 5. `src/NovelTranslator.App/ViewModels/MainViewModel.cs:956-972,1011-1038` — Single-paragraph retranslation cannot be cancelled, 6. `src/NovelTranslator.Core/Glossary/GlossaryExtractor.cs:100` — Greedy bracket regex fails when AI appends bracketed notes, 7. `src/NovelTranslator.Core/Glossary/GlossaryService.cs:27-37` — `else if (IndexOf)` defeats `\b` word-boundary check in `FilterForChunk`, 8. `src/NovelTranslator.Core/Data/NovelRepository.cs:330-366`, `src/NovelTranslator.Core/Export/TextExportImportService.cs:49-64`, `src/NovelTranslator.App/ViewLocator.cs:1-38`, `src/NovelTranslator.App/ViewModels/ViewModelBase.cs:1-7` — Dead methods and unused template classes, Not checked, Ponytail Audit — Should Fix (v4.6) (+3 more)

### Community 35 - "Ponytail Audit — Must Fix (v4.6)"
Cohesion: 0.18
Nodes (10): 1. `src/NovelTranslator.Core/Export/WebChapterImportService.cs:41-48` — Unbounded response download and unrestricted URL targets in URL Chapter Import, 2. `src/NovelTranslator.Core/Translation/TranslationPipeline.cs:65-76,208-220` — Out-of-order chunk assembly when resuming with an incomplete early chunk, 3. `src/NovelTranslator.Core/Data/NovelDatabase.cs:152-158` & `src/NovelTranslator.App/ViewModels/MainViewModel.cs:1662-1675` — Database restore does not clear SQLite connection pools or re-run schema migration, 4. `src/NovelTranslator.Core/Glossary/GlossaryService.cs:71-84` — Glossary CSV import corrupts entries with multi-line notes, Lean (what stays as-is), Must fix (bugs, security, or breaks at normal load), Not checked, Ponytail Audit — Must Fix (v4.6) (+2 more)

### Community 36 - ".Benchmark_AllComponents_ResourceUsageComparison"
Cohesion: 0.07
Nodes (18): DeletedChapterSnapshot, ChapterChunk, ChapterId, ChunkIndex, HasWarning, Id, IsCompleted, SourceText (+10 more)

### Community 37 - "Nice to have (cleanup, duplication, minor UX polish)"
Cohesion: 0.18
Nodes (10): 1. `src/NovelTranslator.App/ViewModels/MainViewModel.cs:174-185` & `src/NovelTranslator.App/ViewModels/SettingsViewModel.cs:193-204` — Duplicated `AvailableEditorFonts` list, 2. `src/NovelTranslator.App/Views/MainWindow.axaml.cs:87-88,107-108,146-147,879-880` — Repeated `new NovelDatabase()` + `new NovelRepository()` in dialog handlers, 3. `src/NovelTranslator.Core/Llm/LlmClient.cs:55-68` — Missing fast-fail message when `ModelName` is empty, 4. `src/NovelTranslator.App/NovelTranslator.App.csproj:11` — Empty `Models/` folder inclusion, 5. `tests/NovelTranslator.Tests/ResourceBenchmarkTests.cs:172` — Blocking `.Result` call triggers `xUnit1031` compiler warning, Nice to have (cleanup, duplication, minor UX polish), Not checked, Ponytail Audit — Nice to Have (v4.6) (+2 more)

### Community 41 - "ExtractedTerm"
Cohesion: 0.20
Nodes (10): ExtractedTerm, Category, IsSelected, Notes, SourceTerm, TargetTerm, GlossaryCategory, Location (+2 more)

### Community 42 - "TranslationProgressInfo"
Cohesion: 0.15
Nodes (9): TranslationProgressInfo, CombinedTranslatedTextSoFar, CurrentChunkIndex, FullCurrentChunkText, StatusMessage, StreamedWordCount, StreamingDelta, TotalChunks (+1 more)

### Community 43 - "GlossaryEntry"
Cohesion: 0.22
Nodes (8): GlossaryEntry, Category, CategoryDisplay, Id, Notes, SourceTerm, StoryId, TargetTerm

### Community 45 - "FtsSearchResult"
Cohesion: 0.33
Nodes (6): FtsSearchResult, ChapterId, Score, Snippet, StoryId, Title

### Community 47 - "Fact"
Cohesion: 0.13
Nodes (3): NovelDatabase, FtsAndGlossaryExtractorTests, NewFeaturesTests

### Community 49 - "BackendType"
Cohesion: 0.50
Nodes (4): BackendType, Custom, LmStudio, Ollama

### Community 53 - "ChapterStatus"
Cohesion: 0.40
Nodes (5): ChapterStatus, Edited, NotTranslated, Translated, Translating

### Community 55 - "TranslationAppearanceMode"
Cohesion: 0.67
Nodes (3): TranslationAppearanceMode, PerChunk, WordByWord

## Knowledge Gaps
- **220 isolated node(s):** `CoreWebView2`, `net10.0`, `Avalonia (12.1.6)`, `Avalonia.Controls.DataGrid (12.1.2)`, `Avalonia.Desktop (12.1.6)` (+215 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 424 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **24 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainViewModel` connect `MainViewModel` to `App UI & ViewModels (2)`, `List`, `.InsertChapterAsync`, `Story`, `MainWindow`, `AppSettings`, `AutoGlossaryViewModel`, `RelayCommand`, `TranslationPipeline`, `Task`, `Chapter`, `.StartTranslationInternalAsync`, `.Benchmark_AllComponents_ResourceUsageComparison`, `Nice to have (cleanup, duplication, minor UX polish)`, `.InitializeMainWindowAsync`, `.Save`, `Fact`, `KeyEventArgs`, `.SyncParagraphPairs`, `.BackupDatabaseAsync`, `.StartRenameChapter`, `.CopyErrorDetailsAsync`, `.OpenSearchReplaceAsync`?**
  _High betweenness centrality (0.218) - this node is a cross-community bridge._
- **Are the 4 inferred relationships involving `MainViewModel` (e.g. with `1. `src/NovelTranslator.App/ViewModels/MainViewModel.cs:174-185` & `src/NovelTranslator.App/ViewModels/SettingsViewModel.cs:193-204` — Duplicated `AvailableEditorFonts` list` and `2. `src/NovelTranslator.App/Views/MainWindow.axaml.cs:87-88,107-108,146-147,879-880` — Repeated `new NovelDatabase()` + `new NovelRepository()` in dialog handlers`) actually correct?**
  _`MainViewModel` has 4 INFERRED edges - model-reasoned connections that need verification._
- **What connects `CoreWebView2`, `net10.0`, `Avalonia (12.1.6)` to the rest of the system?**
  _220 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `MainViewModel` be split into smaller, more focused modules?**
  _Cohesion score 0.03918722786647315 - nodes in this community are weakly interconnected._
- **Why does `AppSettings` connect `AppSettings` to `MainViewModel`, `App UI & ViewModels (2)`, `List`, `.Benchmark_AllComponents_ResourceUsageComparison`, `Nice to have (cleanup, duplication, minor UX polish)`, `.MainWindow_Loaded`, `.InsertChapterAsync`, `.InitializeMainWindowAsync`, `TranslationProgressInfo`, `SettingsViewModel`, `.Save`, `TranslationPipeline`, `Fact`, `BackendType`, `TranslationAppearanceMode`, `ShortkeysSettings`?**
  _High betweenness centrality (0.089) - this node is a cross-community bridge._
- **Are the 25 inferred relationships involving `Chapter` (e.g. with `.AddMultipleChaptersAsync()` and `.CreateChapterAsync()`) actually correct?**
  _`Chapter` has 25 INFERRED edges - model-reasoned connections that need verification._
- **Should `App UI & ViewModels (2)` be split into smaller, more focused modules?**
  _Cohesion score 0.07610172781984081 - nodes in this community are weakly interconnected._