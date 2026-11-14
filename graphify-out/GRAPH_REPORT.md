# Graph Report - ReadLator  (2026-11-14)

## Corpus Check
- 77 files · ~50,256 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 17 file(s) not represented in the graph (top: .axaml 9, (none) 2, .ttf 2)

## Summary
- 1024 nodes · 2495 edges · 58 communities (36 shown, 22 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 332 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `08ccd22b`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- MainViewModel
- App UI & ViewModels (2)
- List
- WebView2Host
- .InsertChapterAsync
- Story
- App UI & ViewModels (5)
- App UI & ViewModels (6)
- story.mjs
- MainWindow
- AppSettings
- EditorViewModel
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
- GlossaryEntry
- unslop/SKILL.md
- Effects
- Visuals
- interface
- Task
- auto_update_and_commit.md
- Ponytail Audit — Should Fix (v5.2)
- AutoGlossaryViewModel
- .Benchmark_AllComponents_ResourceUsageComparison
- Window
- .MainWindow_Loaded
- SettingsWindow
- .InitializeMainWindowAsync
- TranslationProgressInfo
- SettingsTests
- .Save
- FtsSearchResult
- .BuildAvaloniaApp
- NovelDatabase
- BackendType
- PointerPressedEventArgs
- ReplacePreviewItem
- ChapterStatus
- SynchronousProgress
- TranslationAppearanceMode
- build-velopack.sh script

## God Nodes (most connected - your core abstractions)
1. `MainViewModel` - 161 edges
2. `Chapter` - 68 edges
3. `SettingsViewModel` - 57 edges
4. `Story` - 48 edges
5. `AppSettings` - 48 edges
6. `MainWindow` - 47 edges
7. `NovelRepository` - 38 edges
8. `GlossaryEntry` - 29 edges
9. `NovelTranslator.Core.Models` - 26 edges
10. `EditorViewModel` - 24 edges

## Surprising Connections (you probably didn't know these)
- `Should fix (risky code without a test, real slowness, duplication, dead code)` --references--> `App`  [INFERRED]
  audit-should-fix.md → src/NovelTranslator.App/App.axaml.cs
- `Should fix (risky code without a test, real slowness, duplication, dead code)` --references--> `EditorViewModel`  [INFERRED]
  audit-should-fix.md → src/NovelTranslator.App/ViewModels/EditorViewModel.cs
- `Should fix (risky code without a test, real slowness, duplication, dead code)` --references--> `MainViewModel`  [INFERRED]
  audit-should-fix.md → src/NovelTranslator.App/ViewModels/MainViewModel.cs
- `Must fix (bugs, security, data loss, breaks at the expected load)` --references--> `BrowserImportWindow`  [INFERRED]
  audit-must-fix.md → src/NovelTranslator.App/Views/BrowserImportWindow.axaml.cs
- `Nice to have (small speed-ups, shorter forms, minor UX polish)` --references--> `FirstRunWindow`  [INFERRED]
  audit-nice-to-have.md → src/NovelTranslator.App/Views/FirstRunWindow.axaml.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **NovelTranslator Specification System** — readme_noveltranslator, spec_system_specification, spec_tech_stack, spec_phases [EXTRACTED 1.00]
- **Ponytail Skill Suite** — _agents_skills_ponytail_skill_ponytail_skill, _agents_skills_ponytail_review_skill_ponytail_review, _agents_skills_ponytail_audit_skill_ponytail_audit, _agents_skills_ponytail_debt_skill_ponytail_debt, _agents_skills_ponytail_gain_skill_ponytail_gain, _agents_skills_ponytail_help_skill_ponytail_help [EXTRACTED 1.00]
- **Shortcut Debt Lifecycle** — agents_shortcut_convention, _agents_skills_ponytail_skill_the_ladder, _agents_skills_ponytail_debt_skill_debt_ledger, _agents_skills_ponytail_audit_skill_ponytail_audit [INFERRED 0.85]

## Communities (58 total, 22 thin omitted)

### Community 0 - "MainViewModel"
Cohesion: 0.04
Nodes (46): MainViewModel, AvailableEditorFonts, BrightnessDisplayText, CanUndoDeleteChapter, Chapters, ChapterSearchFilter, ChapterStatusFilterIndex, DimOverlayOpacity (+38 more)

### Community 1 - "App UI & ViewModels (2)"
Cohesion: 0.08
Nodes (12): NovelTranslator.Tests, NovelTranslator.App, NovelTranslator.Core.Llm, NovelTranslator.Core.Glossary, NovelTranslator.Core.Models, NovelTranslator.App.ViewModels, NovelTranslator.Core.Translation, NovelTranslator.App.Views (+4 more)

### Community 2 - "List"
Cohesion: 0.09
Nodes (11): ChatMessage, Content, Role, ILlmClient, ModelInfo, Id, Name, LlmClient (+3 more)

### Community 3 - "WebView2Host"
Cohesion: 0.06
Nodes (13): Lean, Must fix (bugs, security, data loss, breaks at the expected load), Not checked, Ponytail Audit — Must Fix (v5.2), Verdict, What this repo does, WebView2Host, CoreWebView2 (+5 more)

### Community 5 - "Story"
Cohesion: 0.16
Nodes (8): Story, CreatedAt, Id, SourceLang, StyleNotes, TargetLang, Title, ChapterReorderTests

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

### Community 11 - "EditorViewModel"
Cohesion: 0.09
Nodes (16): EditorViewModel, ActiveGlossary, IsUpdatingFromParagraph, OnParagraphContentChanged, OnRetranslateParagraphAction, ParagraphModeButtonText, ParagraphPairs, ParagraphPairViewModel (+8 more)

### Community 12 - "SettingsViewModel"
Cohesion: 0.06
Nodes (20): SettingsViewModel, ApiKeyPasswordChar, AvailableEditorFonts, AvailableModels, CurrentFontFamily, FocusModeKeyDisplay, FullScreenKeyDisplay, IsAiCategorySelected (+12 more)

### Community 14 - "RelayCommand"
Cohesion: 0.07
Nodes (5): RequestOpenAutoGlossary, RequestOpenSearchReplace, RequestOpenSettings, RequestSaveFileDialog, RequestShowErrorDialog

### Community 15 - "TranslationPipeline"
Cohesion: 0.22
Nodes (4): TranslationPipeline, QueueLlmClient, CallCount, TranslationPipelineTests

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
Cohesion: 0.10
Nodes (15): TextExportImportService, Chapter, EditingTitle, Id, IsEditingTitle, IsSelected, Ordinal, OriginalText (+7 more)

### Community 24 - ".StartTranslationInternalAsync"
Cohesion: 0.18
Nodes (4): StoryListViewModel, Chapters, FilteredChapters, Stories

### Community 25 - "ShortkeysSettings"
Cohesion: 0.17
Nodes (10): ShortkeysSettings, FocusModeKey, FullScreenKey, NextChapterKey, PrevChapterKey, SaveKey, SearchReplaceKey, ToggleSidebarKey (+2 more)

### Community 26 - "GlossaryEntry"
Cohesion: 0.06
Nodes (25): Lean, Nice to have (small speed-ups, shorter forms, minor UX polish), Not checked, Ponytail Audit — Nice to Have (v5.2), Verdict, What this repo does, GlossaryViewModel, Entries (+17 more)

### Community 28 - "Effects"
Cohesion: 0.29
Nodes (7): Dithering, Duotone, Effects, Glyphs, Halftone dots, Implementation, Performance

### Community 29 - "Visuals"
Cohesion: 0.29
Nodes (7): Decide what the visual is, Generate it, Objects for moments, Prompt recipe, Put it on the page, Styles that work, Visuals

### Community 31 - "interface"
Cohesion: 0.40
Nodes (4): interface, default_prompt, display_name, short_description

### Community 32 - "Task"
Cohesion: 0.13
Nodes (3): RequestConfirmDialog, RequestOpenFilesDialog, RequestPromptDialog

### Community 34 - "Ponytail Audit — Should Fix (v5.2)"
Cohesion: 0.29
Nodes (6): Lean, Not checked, Ponytail Audit — Should Fix (v5.2), Should fix (risky code without a test, real slowness, duplication, dead code), Verdict, What this repo does

### Community 35 - "AutoGlossaryViewModel"
Cohesion: 0.32
Nodes (4): AutoGlossaryViewModel, ExtractedTerms, OnTermsAdded, RequestClose

### Community 36 - ".Benchmark_AllComponents_ResourceUsageComparison"
Cohesion: 0.06
Nodes (20): DeletedChapterSnapshot, ChapterChunk, ChapterId, ChunkIndex, HasWarning, Id, IsCompleted, SourceText (+12 more)

### Community 38 - ".MainWindow_Loaded"
Cohesion: 0.15
Nodes (7): ExtractedTerm, Category, IsSelected, Notes, SourceTerm, TargetTerm, GlossaryExtractor

### Community 42 - "TranslationProgressInfo"
Cohesion: 0.25
Nodes (8): TranslationProgressInfo, CombinedTranslatedTextSoFar, CurrentChunkIndex, FullCurrentChunkText, StatusMessage, StreamedWordCount, StreamingDelta, TotalChunks

### Community 45 - "FtsSearchResult"
Cohesion: 0.33
Nodes (6): FtsSearchResult, ChapterId, Score, Snippet, StoryId, Title

### Community 47 - "NovelDatabase"
Cohesion: 0.24
Nodes (3): NovelDatabase, FtsAndGlossaryExtractorTests, NewFeaturesTests

### Community 49 - "BackendType"
Cohesion: 0.50
Nodes (4): BackendType, Custom, LmStudio, Ollama

### Community 52 - "ReplacePreviewItem"
Cohesion: 0.50
Nodes (4): ReplacePreviewItem, ChapterId, ChapterTitle, MatchSnippet

### Community 53 - "ChapterStatus"
Cohesion: 0.40
Nodes (5): ChapterStatus, Edited, NotTranslated, Translated, Translating

### Community 55 - "TranslationAppearanceMode"
Cohesion: 0.67
Nodes (3): TranslationAppearanceMode, PerChunk, WordByWord

## Knowledge Gaps
- **219 isolated node(s):** `CoreWebView2`, `net10.0`, `Avalonia (12.1.6)`, `Avalonia.Controls.DataGrid (12.1.2)`, `Avalonia.Desktop (12.1.6)` (+214 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 413 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **22 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainViewModel` connect `MainViewModel` to `App UI & ViewModels (2)`, `List`, `.InsertChapterAsync`, `Story`, `MainWindow`, `AppSettings`, `EditorViewModel`, `RelayCommand`, `TranslationPipeline`, `Task`, `Chapter`, `.StartTranslationInternalAsync`, `Task`, `Ponytail Audit — Should Fix (v5.2)`, `.Benchmark_AllComponents_ResourceUsageComparison`, `.InitializeMainWindowAsync`, `.Save`, `NovelDatabase`, `.StartRenameChapter`, `.OnSelectedChapterChanged`?**
  _High betweenness centrality (0.181) - this node is a cross-community bridge._
- **Are the 4 inferred relationships involving `MainViewModel` (e.g. with `Should fix (risky code without a test, real slowness, duplication, dead code)` and `.InitializeMainWindowAsync()`) actually correct?**
  _`MainViewModel` has 4 INFERRED edges - model-reasoned connections that need verification._
- **What connects `CoreWebView2`, `net10.0`, `Avalonia (12.1.6)` to the rest of the system?**
  _219 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `MainViewModel` be split into smaller, more focused modules?**
  _Cohesion score 0.03750756200846945 - nodes in this community are weakly interconnected._
- **Why does `AppSettings` connect `AppSettings` to `MainViewModel`, `App UI & ViewModels (2)`, `List`, `Task`, `.Benchmark_AllComponents_ResourceUsageComparison`, `.InsertChapterAsync`, `.MainWindow_Loaded`, `.InitializeMainWindowAsync`, `SettingsTests`, `SettingsViewModel`, `.Save`, `TranslationPipeline`, `NovelDatabase`, `BackendType`, `TranslationAppearanceMode`, `ShortkeysSettings`, `GlossaryEntry`?**
  _High betweenness centrality (0.077) - this node is a cross-community bridge._
- **Are the 26 inferred relationships involving `Chapter` (e.g. with `.AddMultipleChaptersAsync()` and `.CreateChapterAsync()`) actually correct?**
  _`Chapter` has 26 INFERRED edges - model-reasoned connections that need verification._
- **Should `App UI & ViewModels (2)` be split into smaller, more focused modules?**
  _Cohesion score 0.07742574257425743 - nodes in this community are weakly interconnected._