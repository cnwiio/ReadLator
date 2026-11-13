# Graph Report - ReadLator  (2026-11-13)

## Corpus Check
- 67 files · ~42,313 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 14 file(s) not represented in the graph (top: .axaml 7, (none) 2, .ttf 2)

## Summary
- 858 nodes · 1920 edges · 54 communities (31 shown, 23 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 207 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `ba838c97`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- MainViewModel
- App UI & ViewModels (2)
- App UI & ViewModels (3)
- Task
- GlossaryViewModel
- .Validate
- App UI & ViewModels (5)
- App UI & ViewModels (6)
- story.mjs
- MainWindow
- AppSettings
- AutoGlossaryViewModel
- SettingsViewModel
- Chapter
- RelayCommand
- App UI & ViewModels (12)
- Documentation & Specifications
- Unslop
- NovelTranslator Features & Implementation Roadmap (Phases 1-6)
- Getting Started & Run Commands
- Tokens
- Signs of slop
- NovelRepository
- .OnTranslatedTextChanged
- ShortkeysSettings
- GlossaryEntry
- unslop/SKILL.md
- Effects
- Visuals
- interface
- NovelDatabase
- auto_update_and_commit.md
- ChapterChunk
- EditorViewModel
- .Benchmark_AllComponents_ResourceUsageComparison
- .TranslateChapterAsync
- Fact
- .SplitIntoChunks
- .InitializeMainWindowAsync
- ParagraphPairViewModel
- TranslationProgressInfo
- TranslationMemoryMatch
- FtsSearchResult
- .BuildAvaloniaApp
- SettingsTests
- BackendType
- .CopyErrorDetailsAsync
- .OpenGlossaryAsync
- .ScanNewTermsAsync

## God Nodes (most connected - your core abstractions)
1. `MainViewModel` - 142 edges
2. `SettingsViewModel` - 55 edges
3. `Chapter` - 54 edges
4. `NovelRepository` - 38 edges
5. `Story` - 38 edges
6. `MainWindow` - 37 edges
7. `AppSettings` - 30 edges
8. `GlossaryEntry` - 29 edges
9. `EditorViewModel` - 24 edges
10. `NovelTranslator.Core.Models` - 24 edges

## Surprising Connections (you probably didn't know these)
- `The Ladder (Smallest Complete Change)` --semantically_similar_to--> `Smallest Complete Change`  [INFERRED] [semantically similar]
  .agents/skills/ponytail/SKILL.md → AGENTS.md
- `ResourceBenchmarkTests` --references--> `NovelDatabase`  [EXTRACTED]
  tests/NovelTranslator.Tests/ResourceBenchmarkTests.cs → src/NovelTranslator.Core/Data/NovelDatabase.cs
- `ChapterReorderTests` --references--> `NovelRepository`  [EXTRACTED]
  tests/NovelTranslator.Tests/ChapterReorderTests.cs → src/NovelTranslator.Core/Data/NovelRepository.cs
- `FtsAndGlossaryExtractorTests` --references--> `NovelRepository`  [EXTRACTED]
  tests/NovelTranslator.Tests/FtsAndGlossaryExtractorTests.cs → src/NovelTranslator.Core/Data/NovelRepository.cs
- `RepositoryTests` --references--> `NovelRepository`  [EXTRACTED]
  tests/NovelTranslator.Tests/RepositoryTests.cs → src/NovelTranslator.Core/Data/NovelRepository.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **NovelTranslator Specification System** — readme_noveltranslator, spec_system_specification, spec_tech_stack, spec_phases [EXTRACTED 1.00]
- **Ponytail Skill Suite** — _agents_skills_ponytail_skill_ponytail_skill, _agents_skills_ponytail_review_skill_ponytail_review, _agents_skills_ponytail_audit_skill_ponytail_audit, _agents_skills_ponytail_debt_skill_ponytail_debt, _agents_skills_ponytail_gain_skill_ponytail_gain, _agents_skills_ponytail_help_skill_ponytail_help [EXTRACTED 1.00]
- **Shortcut Debt Lifecycle** — agents_shortcut_convention, _agents_skills_ponytail_skill_the_ladder, _agents_skills_ponytail_debt_skill_debt_ledger, _agents_skills_ponytail_audit_skill_ponytail_audit [INFERRED 0.85]

## Communities (54 total, 23 thin omitted)

### Community 0 - "MainViewModel"
Cohesion: 0.04
Nodes (38): MainViewModel, AvailableEditorFonts, Chapters, ChapterSearchFilter, ChapterStatusFilterIndex, Editor, FilteredChapters, FocusModeButtonText (+30 more)

### Community 1 - "App UI & ViewModels (2)"
Cohesion: 0.08
Nodes (11): NovelTranslator.Tests, NovelTranslator.App, NovelTranslator.Core.Llm, NovelTranslator.Core.Glossary, NovelTranslator.Core.Models, NovelTranslator.App.ViewModels, NovelTranslator.Core.Translation, NovelTranslator.App.Views (+3 more)

### Community 2 - "App UI & ViewModels (3)"
Cohesion: 0.08
Nodes (15): ExtractedTerm, Category, IsSelected, Notes, SourceTerm, TargetTerm, GlossaryExtractor, ChatMessage (+7 more)

### Community 3 - "Task"
Cohesion: 0.16
Nodes (8): RequestPromptDialog, Story, CreatedAt, Id, SourceLang, StyleNotes, TargetLang, Title

### Community 4 - "GlossaryViewModel"
Cohesion: 0.08
Nodes (9): GlossaryViewModel, Entries, RequestClose, AutoGlossaryDialog, GlossaryWindow, ViewModel, SearchReplaceWindow, SettingsWindow (+1 more)

### Community 5 - ".Validate"
Cohesion: 0.17
Nodes (7): OutputValidator, ValidationResult, CleanedText, HasParagraphMismatchWarning, IsValid, RetryHint, OutputValidatorTests

### Community 6 - "App UI & ViewModels (5)"
Cohesion: 0.09
Nodes (19): Avalonia (12.1.6), Avalonia.Controls.DataGrid (12.1.2), Avalonia.Desktop (12.1.6), Avalonia.Fonts.Inter (12.1.6), Avalonia.Themes.Fluent (12.1.6), AvaloniaUI.DiagnosticsSupport (2.2.3), CommunityToolkit.Mvvm (8.4.2), coverlet.collector (6.0.4) (+11 more)

### Community 7 - "App UI & ViewModels (6)"
Cohesion: 0.13
Nodes (15): Quality Audit Order of Importance, Ponytail Audit Skill, Shortcut Debt Ledger, Ponytail Debt Skill, Ponytail Gain Skill, Ponytail Benchmark Scoreboard, Ponytail Help Skill, Ponytail Review Skill (+7 more)

### Community 8 - "story.mjs"
Cohesion: 0.05
Nodes (25): args, chrome, positionals, args, chrome, file, frame, frames (+17 more)

### Community 10 - "AppSettings"
Cohesion: 0.12
Nodes (16): AppSettings, ApiKey, AutoSaveDelaySeconds, Backend, BaseUrl, ContextParagraphCount, CustomSystemPrompt, EditorFontFamily (+8 more)

### Community 11 - "AutoGlossaryViewModel"
Cohesion: 0.09
Nodes (18): AutoGlossaryViewModel, ExtractedTerms, OnTermsAdded, RequestClose, ReplacePreviewItem, ChapterId, ChapterTitle, MatchSnippet (+10 more)

### Community 12 - "SettingsViewModel"
Cohesion: 0.06
Nodes (20): SettingsViewModel, ApiKeyPasswordChar, AvailableEditorFonts, AvailableModels, CurrentFontFamily, FocusModeKeyDisplay, FullScreenKeyDisplay, IsAiCategorySelected (+12 more)

### Community 13 - "Chapter"
Cohesion: 0.10
Nodes (16): RequestSaveFileDialog, TextExportImportService, Chapter, EditingTitle, Id, IsEditingTitle, IsSelected, Ordinal (+8 more)

### Community 14 - "RelayCommand"
Cohesion: 0.10
Nodes (3): RequestOpenSearchReplace, RequestOpenSettings, RequestShowErrorDialog

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

### Community 25 - "ShortkeysSettings"
Cohesion: 0.20
Nodes (10): ShortkeysSettings, FocusModeKey, FullScreenKey, NextChapterKey, PrevChapterKey, SaveKey, SearchReplaceKey, ToggleSidebarKey (+2 more)

### Community 26 - "GlossaryEntry"
Cohesion: 0.12
Nodes (14): GlossaryService, GlossaryCategory, Location, Person, Term, GlossaryEntry, Category, CategoryDisplay (+6 more)

### Community 28 - "Effects"
Cohesion: 0.29
Nodes (7): Dithering, Duotone, Effects, Glyphs, Halftone dots, Implementation, Performance

### Community 29 - "Visuals"
Cohesion: 0.29
Nodes (7): Decide what the visual is, Generate it, Objects for moments, Prompt recipe, Put it on the page, Styles that work, Visuals

### Community 31 - "interface"
Cohesion: 0.40
Nodes (4): interface, default_prompt, display_name, short_description

### Community 32 - "NovelDatabase"
Cohesion: 0.20
Nodes (5): RequestOpenFilesDialog, NovelDatabase, ChapterReorderTests, FtsAndGlossaryExtractorTests, RepositoryTests

### Community 34 - "ChapterChunk"
Cohesion: 0.12
Nodes (14): ChapterChunk, ChapterId, ChunkIndex, HasWarning, Id, IsCompleted, SourceText, TranslatedText (+6 more)

### Community 35 - "EditorViewModel"
Cohesion: 0.18
Nodes (8): EditorViewModel, ActiveGlossary, IsUpdatingFromParagraph, OnParagraphContentChanged, OnRetranslateParagraphAction, ParagraphModeButtonText, ParagraphPairs, ParagraphAndEditorTests

### Community 41 - "ParagraphPairViewModel"
Cohesion: 0.25
Nodes (4): ParagraphPairViewModel, MatchedGlossary, OnContentChanged, OnRetranslateRequested

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
- **208 isolated node(s):** `net10.0`, `Avalonia (12.1.6)`, `Avalonia.Controls.DataGrid (12.1.2)`, `Avalonia.Desktop (12.1.6)`, `Avalonia.Themes.Fluent (12.1.6)` (+203 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 376 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **23 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainViewModel` connect `MainViewModel` to `App UI & ViewModels (2)`, `Task`, `MainWindow`, `AppSettings`, `AutoGlossaryViewModel`, `Chapter`, `RelayCommand`, `NovelRepository`, `Task`, `.OnTranslatedTextChanged`, `NovelDatabase`, `EditorViewModel`, `.Benchmark_AllComponents_ResourceUsageComparison`, `.TranslateChapterAsync`, `.SplitIntoChunks`, `.InitializeMainWindowAsync`, `ParagraphPairViewModel`, `.Save`, `.StartRenameChapter`, `.SetParagraphMode`, `.CopyErrorDetailsAsync`, `.OpenGlossaryAsync`, `.ScanNewTermsAsync`?**
  _High betweenness centrality (0.201) - this node is a cross-community bridge._
- **Are the 15 inferred relationships involving `Chapter` (e.g. with `.AddMultipleChaptersAsync()` and `.CreateChapterAsync()`) actually correct?**
  _`Chapter` has 15 INFERRED edges - model-reasoned connections that need verification._
- **What connects `net10.0`, `Avalonia (12.1.6)`, `Avalonia.Controls.DataGrid (12.1.2)` to the rest of the system?**
  _208 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `MainViewModel` be split into smaller, more focused modules?**
  _Cohesion score 0.04336734693877551 - nodes in this community are weakly interconnected._
- **Why does `SettingsViewModel` connect `SettingsViewModel` to `App UI & ViewModels (2)`, `App UI & ViewModels (3)`, `.Benchmark_AllComponents_ResourceUsageComparison`, `GlossaryViewModel`, `AppSettings`, `AutoGlossaryViewModel`, `RelayCommand`, `.SaveSettings`?**
  _High betweenness centrality (0.082) - this node is a cross-community bridge._
- **Should `App UI & ViewModels (2)` be split into smaller, more focused modules?**
  _Cohesion score 0.07992849846782431 - nodes in this community are weakly interconnected._
- **Why does `MainWindow` connect `MainWindow` to `MainViewModel`, `App UI & ViewModels (2)`, `App UI & ViewModels (3)`, `GlossaryViewModel`, `.InitializeMainWindowAsync`, `Chapter`, `.StartRenameChapter`, `NovelRepository`?**
  _High betweenness centrality (0.075) - this node is a cross-community bridge._