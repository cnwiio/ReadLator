# Graph Report - ReadLator  (2026-11-14)

## Corpus Check
- 69 files · ~43,592 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 14 file(s) not represented in the graph (top: .axaml 7, (none) 2, .ttf 2)

## Summary
- 889 nodes · 2044 edges · 42 communities (27 shown, 15 thin omitted)
- Extraction: 88% EXTRACTED · 12% INFERRED · 0% AMBIGUOUS · INFERRED: 243 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `f73d1f47`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- MainViewModel
- App UI & ViewModels (2)
- App UI & ViewModels (3)
- .Validate
- App UI & ViewModels (5)
- App UI & ViewModels (6)
- story.mjs
- MainWindow
- AppSettings
- AutoGlossaryViewModel
- SettingsViewModel
- RelayCommand
- App UI & ViewModels (12)
- Documentation & Specifications
- Unslop
- NovelTranslator Features & Implementation Roadmap (Phases 1-6)
- Getting Started & Run Commands
- Tokens
- Signs of slop
- Task
- Task
- .StartTranslationInternalAsync
- ShortkeysSettings
- GlossaryEntry
- unslop/SKILL.md
- Effects
- Visuals
- interface
- auto_update_and_commit.md
- .Benchmark_AllComponents_ResourceUsageComparison
- .InitializeMainWindowAsync
- TranslationProgressInfo
- TranslationMemoryMatch
- FtsSearchResult
- .BuildAvaloniaApp
- SettingsTests
- BackendType

## God Nodes (most connected - your core abstractions)
1. `MainViewModel` - 146 edges
2. `Chapter` - 60 edges
3. `SettingsViewModel` - 55 edges
4. `Story` - 41 edges
5. `NovelRepository` - 38 edges
6. `MainWindow` - 37 edges
7. `AppSettings` - 32 edges
8. `GlossaryEntry` - 30 edges
9. `EditorViewModel` - 24 edges
10. `NovelTranslator.Core.Models` - 24 edges

## Surprising Connections (you probably didn't know these)
- `The Ladder (Smallest Complete Change)` --semantically_similar_to--> `Smallest Complete Change`  [INFERRED] [semantically similar]
  .agents/skills/ponytail/SKILL.md → AGENTS.md
- `ResourceBenchmarkTests` --references--> `NovelDatabase`  [EXTRACTED]
  tests/NovelTranslator.Tests/ResourceBenchmarkTests.cs → src/NovelTranslator.Core/Data/NovelDatabase.cs
- `ResourceBenchmarkTests` --references--> `NovelRepository`  [EXTRACTED]
  tests/NovelTranslator.Tests/ResourceBenchmarkTests.cs → src/NovelTranslator.Core/Data/NovelRepository.cs
- `ChapterReorderTests` --references--> `NovelDatabase`  [EXTRACTED]
  tests/NovelTranslator.Tests/ChapterReorderTests.cs → src/NovelTranslator.Core/Data/NovelDatabase.cs
- `FtsAndGlossaryExtractorTests` --references--> `NovelDatabase`  [EXTRACTED]
  tests/NovelTranslator.Tests/FtsAndGlossaryExtractorTests.cs → src/NovelTranslator.Core/Data/NovelDatabase.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **NovelTranslator Specification System** — readme_noveltranslator, spec_system_specification, spec_tech_stack, spec_phases [EXTRACTED 1.00]
- **Ponytail Skill Suite** — _agents_skills_ponytail_skill_ponytail_skill, _agents_skills_ponytail_review_skill_ponytail_review, _agents_skills_ponytail_audit_skill_ponytail_audit, _agents_skills_ponytail_debt_skill_ponytail_debt, _agents_skills_ponytail_gain_skill_ponytail_gain, _agents_skills_ponytail_help_skill_ponytail_help [EXTRACTED 1.00]
- **Shortcut Debt Lifecycle** — agents_shortcut_convention, _agents_skills_ponytail_skill_the_ladder, _agents_skills_ponytail_debt_skill_debt_ledger, _agents_skills_ponytail_audit_skill_ponytail_audit [INFERRED 0.85]

## Communities (42 total, 15 thin omitted)

### Community 0 - "MainViewModel"
Cohesion: 0.04
Nodes (42): MainViewModel, AvailableEditorFonts, Chapters, ChapterSearchFilter, ChapterStatusFilterIndex, Editor, FilteredChapters, FocusModeButtonText (+34 more)

### Community 1 - "App UI & ViewModels (2)"
Cohesion: 0.08
Nodes (11): NovelTranslator.Tests, NovelTranslator.App, NovelTranslator.Core.Llm, NovelTranslator.Core.Glossary, NovelTranslator.Core.Models, NovelTranslator.App.ViewModels, NovelTranslator.Core.Translation, NovelTranslator.App.Views (+3 more)

### Community 2 - "App UI & ViewModels (3)"
Cohesion: 0.07
Nodes (19): WebChapterImportService, WebChapterResult, ExtractedTerm, Category, IsSelected, Notes, SourceTerm, TargetTerm (+11 more)

### Community 5 - ".Validate"
Cohesion: 0.17
Nodes (7): OutputValidator, ValidationResult, CleanedText, HasParagraphMismatchWarning, IsValid, RetryHint, OutputValidatorTests

### Community 6 - "App UI & ViewModels (5)"
Cohesion: 0.09
Nodes (20): Avalonia (12.1.6), Avalonia.Controls.DataGrid (12.1.2), Avalonia.Desktop (12.1.6), Avalonia.Fonts.Inter (12.1.6), Avalonia.Themes.Fluent (12.1.6), AvaloniaUI.DiagnosticsSupport (2.2.3), CommunityToolkit.Mvvm (8.4.2), coverlet.collector (6.0.4) (+12 more)

### Community 7 - "App UI & ViewModels (6)"
Cohesion: 0.13
Nodes (15): Quality Audit Order of Importance, Ponytail Audit Skill, Shortcut Debt Ledger, Ponytail Debt Skill, Ponytail Gain Skill, Ponytail Benchmark Scoreboard, Ponytail Help Skill, Ponytail Review Skill (+7 more)

### Community 8 - "story.mjs"
Cohesion: 0.05
Nodes (25): args, chrome, positionals, args, chrome, file, frame, frames (+17 more)

### Community 9 - "MainWindow"
Cohesion: 0.05
Nodes (8): AutoGlossaryDialog, GlossaryWindow, ViewModel, MainWindow, ViewModel, SearchReplaceWindow, SettingsWindow, ViewModel

### Community 10 - "AppSettings"
Cohesion: 0.12
Nodes (16): AppSettings, ApiKey, AutoSaveDelaySeconds, Backend, BaseUrl, ContextParagraphCount, CustomSystemPrompt, EditorFontFamily (+8 more)

### Community 11 - "AutoGlossaryViewModel"
Cohesion: 0.05
Nodes (30): AutoGlossaryViewModel, ExtractedTerms, OnTermsAdded, RequestClose, EditorViewModel, ActiveGlossary, IsUpdatingFromParagraph, OnParagraphContentChanged (+22 more)

### Community 12 - "SettingsViewModel"
Cohesion: 0.06
Nodes (20): SettingsViewModel, ApiKeyPasswordChar, AvailableEditorFonts, AvailableModels, CurrentFontFamily, FocusModeKeyDisplay, FullScreenKeyDisplay, IsAiCategorySelected (+12 more)

### Community 14 - "RelayCommand"
Cohesion: 0.09
Nodes (3): RequestCopyToClipboard, RequestOpenSearchReplace, RequestSaveFileDialog

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

### Community 22 - "Task"
Cohesion: 0.06
Nodes (30): NovelDatabase, NovelRepository, TextExportImportService, Chapter, EditingTitle, Id, IsEditingTitle, IsSelected (+22 more)

### Community 25 - "ShortkeysSettings"
Cohesion: 0.18
Nodes (10): ShortkeysSettings, FocusModeKey, FullScreenKey, NextChapterKey, PrevChapterKey, SaveKey, SearchReplaceKey, ToggleSidebarKey (+2 more)

### Community 26 - "GlossaryEntry"
Cohesion: 0.07
Nodes (22): GlossaryViewModel, Entries, RequestClose, GlossaryService, ChapterStatus, Edited, NotTranslated, Translated (+14 more)

### Community 28 - "Effects"
Cohesion: 0.29
Nodes (7): Dithering, Duotone, Effects, Glyphs, Halftone dots, Implementation, Performance

### Community 29 - "Visuals"
Cohesion: 0.29
Nodes (7): Decide what the visual is, Generate it, Objects for moments, Prompt recipe, Put it on the page, Styles that work, Visuals

### Community 31 - "interface"
Cohesion: 0.40
Nodes (4): interface, default_prompt, display_name, short_description

### Community 36 - ".Benchmark_AllComponents_ResourceUsageComparison"
Cohesion: 0.09
Nodes (12): ChapterChunk, ChapterId, ChunkIndex, HasWarning, Id, IsCompleted, SourceText, TranslatedText (+4 more)

### Community 42 - "TranslationProgressInfo"
Cohesion: 0.25
Nodes (8): TranslationProgressInfo, CombinedTranslatedTextSoFar, CurrentChunkIndex, FullCurrentChunkText, StatusMessage, StreamedWordCount, StreamingDelta, TotalChunks

### Community 43 - "TranslationMemoryMatch"
Cohesion: 0.29
Nodes (7): TranslationMemoryMatch, ChapterId, MatchedSource, MatchedTranslation, Score, StoryId, Title

### Community 45 - "FtsSearchResult"
Cohesion: 0.33
Nodes (6): FtsSearchResult, ChapterId, Score, Snippet, StoryId, Title

### Community 49 - "BackendType"
Cohesion: 0.50
Nodes (4): BackendType, Custom, LmStudio, Ollama

## Knowledge Gaps
- **209 isolated node(s):** `net10.0`, `Avalonia (12.1.6)`, `Avalonia.Controls.DataGrid (12.1.2)`, `Avalonia.Desktop (12.1.6)`, `Avalonia.Themes.Fluent (12.1.6)` (+204 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 380 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **15 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainViewModel` connect `MainViewModel` to `App UI & ViewModels (2)`, `.Benchmark_AllComponents_ResourceUsageComparison`, `.InitializeMainWindowAsync`, `MainWindow`, `AppSettings`, `AutoGlossaryViewModel`, `.Save`, `RelayCommand`, `.CommitRenameChapterAsync`, `.SyncParagraphPairs`, `Task`, `Task`, `.StartTranslationInternalAsync`?**
  _High betweenness centrality (0.195) - this node is a cross-community bridge._
- **Are the 19 inferred relationships involving `Chapter` (e.g. with `.AddMultipleChaptersAsync()` and `.CreateChapterAsync()`) actually correct?**
  _`Chapter` has 19 INFERRED edges - model-reasoned connections that need verification._
- **What connects `net10.0`, `Avalonia (12.1.6)`, `Avalonia.Controls.DataGrid (12.1.2)` to the rest of the system?**
  _209 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `MainViewModel` be split into smaller, more focused modules?**
  _Cohesion score 0.039473684210526314 - nodes in this community are weakly interconnected._
- **Why does `SettingsViewModel` connect `SettingsViewModel` to `App UI & ViewModels (2)`, `.TestConnectionAsync`, `.Benchmark_AllComponents_ResourceUsageComparison`, `MainWindow`, `AppSettings`, `AutoGlossaryViewModel`, `ShortkeysSettings`, `.SaveSettings`?**
  _High betweenness centrality (0.079) - this node is a cross-community bridge._
- **Should `App UI & ViewModels (2)` be split into smaller, more focused modules?**
  _Cohesion score 0.0778786430960344 - nodes in this community are weakly interconnected._
- **Why does `MainWindow` connect `MainWindow` to `MainViewModel`, `App UI & ViewModels (2)`, `.InitializeMainWindowAsync`, `.CommitRenameChapterAsync`, `Task`?**
  _High betweenness centrality (0.073) - this node is a cross-community bridge._