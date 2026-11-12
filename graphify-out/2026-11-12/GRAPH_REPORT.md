# Graph Report - ReadLator  (2026-11-11)

## Corpus Check
- Corpus is ~15,053 words - fits in a single context window. You may not need a graph.

## Summary
- 408 nodes · 959 edges · 20 communities (17 shown, 3 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 110 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- App UI & ViewModels
- App UI & ViewModels (2)
- App UI & ViewModels (3)
- App UI & ViewModels (4)
- Glossary Management
- Translation Pipeline & Chunker
- App UI & ViewModels (5)
- App UI & ViewModels (6)
- App UI & ViewModels (7)
- App UI & ViewModels (8)
- Translation Pipeline & Chunker (2)
- App UI & ViewModels (9)
- App UI & ViewModels (10)
- App UI & ViewModels (11)
- LLM Client & Streaming
- App UI & ViewModels (12)
- Documentation & Specifications
- BackendType & Custom
- NovelTranslator Features & Implementation Roadmap (Phases 1-6)
- Getting Started & Run Commands

## God Nodes (most connected - your core abstractions)
1. `MainViewModel` - 59 edges
2. `NovelRepository` - 27 edges
3. `Chapter` - 27 edges
4. `Story` - 25 edges
5. `GlossaryEntry` - 20 edges
6. `GlossaryViewModel` - 19 edges
7. `AppSettings` - 19 edges
8. `LlmClient` - 16 edges
9. `SearchReplaceViewModel` - 14 edges
10. `SettingsViewModel` - 14 edges

## Surprising Connections (you probably didn't know these)
- `The Ladder (Smallest Complete Change)` --semantically_similar_to--> `Smallest Complete Change`  [INFERRED] [semantically similar]
  .agents/skills/ponytail/SKILL.md → AGENTS.md
- `RepositoryTests` --references--> `NovelDatabase`  [EXTRACTED]
  tests/NovelTranslator.Tests/RepositoryTests.cs → src/NovelTranslator.Core/Data/NovelDatabase.cs
- `RepositoryTests` --references--> `NovelRepository`  [EXTRACTED]
  tests/NovelTranslator.Tests/RepositoryTests.cs → src/NovelTranslator.Core/Data/NovelRepository.cs
- `Ponytail Audit Skill` --references--> `Shortcut Comment Convention`  [EXTRACTED]
  .agents/skills/ponytail-audit/SKILL.md → AGENTS.md
- `Ponytail Debt Skill` --references--> `Shortcut Comment Convention`  [EXTRACTED]
  .agents/skills/ponytail-debt/SKILL.md → AGENTS.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **NovelTranslator Specification System** — readme_noveltranslator, spec_system_specification, spec_tech_stack, spec_phases [EXTRACTED 1.00]
- **Ponytail Skill Suite** — _agents_skills_ponytail_skill_ponytail_skill, _agents_skills_ponytail_review_skill_ponytail_review, _agents_skills_ponytail_audit_skill_ponytail_audit, _agents_skills_ponytail_debt_skill_ponytail_debt, _agents_skills_ponytail_gain_skill_ponytail_gain, _agents_skills_ponytail_help_skill_ponytail_help [EXTRACTED 1.00]
- **Shortcut Debt Lifecycle** — agents_shortcut_convention, _agents_skills_ponytail_skill_the_ladder, _agents_skills_ponytail_debt_skill_debt_ledger, _agents_skills_ponytail_audit_skill_ponytail_audit [INFERRED 0.85]

## Communities (20 total, 3 thin omitted)

### Community 0 - "App UI & ViewModels"
Cohesion: 0.06
Nodes (22): MainViewModel, Chapters, RequestConfirmDialog, RequestCopyToClipboard, RequestOpenFilesDialog, RequestOpenGlossary, RequestOpenSearchReplace, RequestOpenSettings (+14 more)

### Community 1 - "App UI & ViewModels (2)"
Cohesion: 0.10
Nodes (10): NovelTranslator.Tests, NovelTranslator.Core.Llm, NovelTranslator.Core.Glossary, NovelTranslator.Core.Models, NovelTranslator.App.ViewModels, NovelTranslator.Core.Translation, NovelTranslator.App.Views, NovelTranslator.Core.Export (+2 more)

### Community 2 - "App UI & ViewModels (3)"
Cohesion: 0.11
Nodes (10): ChatMessage, Content, Role, ILlmClient, ModelInfo, Id, Name, LlmClient (+2 more)

### Community 3 - "App UI & ViewModels (4)"
Cohesion: 0.09
Nodes (19): NovelDatabase, NovelRepository, ChapterChunk, ChapterId, ChunkIndex, HasWarning, Id, IsCompleted (+11 more)

### Community 4 - "Glossary Management"
Cohesion: 0.09
Nodes (18): GlossaryService, ChapterStatus, Edited, NotTranslated, Translated, Translating, GlossaryCategory, Location (+10 more)

### Community 5 - "Translation Pipeline & Chunker"
Cohesion: 0.13
Nodes (9): Chunker, OutputValidator, ValidationResult, CleanedText, HasParagraphMismatchWarning, IsValid, RetryHint, ChunkerTests (+1 more)

### Community 6 - "App UI & ViewModels (5)"
Cohesion: 0.09
Nodes (19): Avalonia (12.1.6), Avalonia.Controls.DataGrid (12.1.2), Avalonia.Desktop (12.1.6), Avalonia.Fonts.Inter (12.1.6), Avalonia.Themes.Fluent (12.1.6), AvaloniaUI.DiagnosticsSupport (2.2.3), CommunityToolkit.Mvvm (8.4.2), coverlet.collector (6.0.4) (+11 more)

### Community 7 - "App UI & ViewModels (6)"
Cohesion: 0.13
Nodes (15): Quality Audit Order of Importance, Ponytail Audit Skill, Shortcut Debt Ledger, Ponytail Debt Skill, Ponytail Gain Skill, Ponytail Benchmark Scoreboard, Ponytail Help Skill, Ponytail Review Skill (+7 more)

### Community 8 - "App UI & ViewModels (7)"
Cohesion: 0.16
Nodes (3): NovelTranslator.App, App, Program

### Community 9 - "App UI & ViewModels (8)"
Cohesion: 0.22
Nodes (5): GlossaryWindow, ViewModel, MainWindow, ViewModel, SearchReplaceWindow

### Community 10 - "Translation Pipeline & Chunker (2)"
Cohesion: 0.15
Nodes (10): AppSettings, ApiKey, Backend, BaseUrl, ContextParagraphCount, LastSelectedChapterId, LastSelectedStoryId, MaxChunkWords (+2 more)

### Community 11 - "App UI & ViewModels (9)"
Cohesion: 0.20
Nodes (8): ReplacePreviewItem, ChapterId, ChapterTitle, MatchSnippet, SearchReplaceViewModel, OnReplaceCompleted, Previews, RequestClose

### Community 12 - "App UI & ViewModels (10)"
Cohesion: 0.29
Nodes (4): SettingsViewModel, AvailableModels, RequestClose, SettingsWindow

### Community 13 - "App UI & ViewModels (11)"
Cohesion: 0.25
Nodes (4): GlossaryViewModel, Entries, RequestClose, ViewModelBase

### Community 14 - "LLM Client & Streaming"
Cohesion: 0.29
Nodes (7): TranslationProgressInfo, CombinedTranslatedTextSoFar, CurrentChunkIndex, FullCurrentChunkText, StatusMessage, StreamingDelta, TotalChunks

### Community 16 - "Documentation & Specifications"
Cohesion: 0.50
Nodes (3): NovelTranslator Project, System Specification, .NET 10 & Avalonia Tech Stack

### Community 17 - "BackendType & Custom"
Cohesion: 0.50
Nodes (4): BackendType, Custom, LmStudio, Ollama

## Knowledge Gaps
- **103 isolated node(s):** `net10.0`, `Avalonia (12.1.6)`, `Avalonia.Controls.DataGrid (12.1.2)`, `Avalonia.Desktop (12.1.6)`, `Avalonia.Themes.Fluent (12.1.6)` (+98 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 138 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **3 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainViewModel` connect `App UI & ViewModels` to `App UI & ViewModels (2)`, `App UI & ViewModels (3)`, `App UI & ViewModels (4)`, `App UI & ViewModels (7)`, `App UI & ViewModels (8)`, `Translation Pipeline & Chunker (2)`, `App UI & ViewModels (9)`, `App UI & ViewModels (10)`, `App UI & ViewModels (11)`?**
  _High betweenness centrality (0.182) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `MainViewModel` (e.g. with `.OnFrameworkInitializationCompleted()` and `.Initialize()`) actually correct?**
  _`MainViewModel` has 2 INFERRED edges - model-reasoned connections that need verification._
- **What connects `net10.0`, `Avalonia (12.1.6)`, `Avalonia.Controls.DataGrid (12.1.2)` to the rest of the system?**
  _103 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `App UI & ViewModels` be split into smaller, more focused modules?**
  _Cohesion score 0.059395801331285206 - nodes in this community are weakly interconnected._
- **Why does `AppSettings` connect `Translation Pipeline & Chunker (2)` to `App UI & ViewModels`, `App UI & ViewModels (2)`, `App UI & ViewModels (3)`, `App UI & ViewModels (8)`, `App UI & ViewModels (10)`, `BackendType & Custom`?**
  _High betweenness centrality (0.065) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `NovelRepository` (e.g. with `.MainWindow_Loaded()` and `.OnAddSelectedToGlossaryClicked()`) actually correct?**
  _`NovelRepository` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Should `App UI & ViewModels (2)` be split into smaller, more focused modules?**
  _Cohesion score 0.09774011299435029 - nodes in this community are weakly interconnected._