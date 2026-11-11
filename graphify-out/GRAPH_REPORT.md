# Graph Report - ReadLator  (2026-11-11)

## Corpus Check
- Corpus is ~3,848 words - fits in a single context window. You may not need a graph.

## Summary
- 22 nodes · 29 edges · 4 communities
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 3 edges (avg confidence: 0.92)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Audit & Code Review
- Debt Ledger & Benchmarks
- Core Skill & Help Interface
- Lazy Senior Dev Rules

## God Nodes (most connected - your core abstractions)
1. `Ponytail Help Skill` - 7 edges
2. `Ponytail Audit Skill` - 5 edges
3. `Shortcut Comment Convention` - 4 edges
4. `Ponytail Review Skill` - 4 edges
5. `Ponytail Debt Skill` - 4 edges
6. `Ponytail Gain Skill` - 4 edges
7. `Smallest Complete Change` - 2 edges
8. `Ponytail Skill` - 2 edges
9. `The Ladder (Smallest Complete Change)` - 2 edges
10. `Ponytail Modes (Lite, Full, Ultra)` - 2 edges

## Surprising Connections (you probably didn't know these)
- `The Ladder (Smallest Complete Change)` --semantically_similar_to--> `Smallest Complete Change`  [INFERRED] [semantically similar]
  .agents/skills/ponytail/SKILL.md → AGENTS.md
- `Ponytail Audit Skill` --references--> `Shortcut Comment Convention`  [EXTRACTED]
  .agents/skills/ponytail-audit/SKILL.md → AGENTS.md
- `Ponytail Debt Skill` --references--> `Shortcut Comment Convention`  [EXTRACTED]
  .agents/skills/ponytail-debt/SKILL.md → AGENTS.md
- `Ponytail Review Skill` --references--> `Shortcut Comment Convention`  [EXTRACTED]
  .agents/skills/ponytail-review/SKILL.md → AGENTS.md
- `Ponytail Help Skill` --references--> `Ponytail Skill`  [EXTRACTED]
  .agents/skills/ponytail-help/SKILL.md → .agents/skills/ponytail/SKILL.md

## Hyperedges (group relationships)
- **Ponytail Skill Suite** — _agents_skills_ponytail_skill_ponytail_skill, _agents_skills_ponytail_review_skill_ponytail_review, _agents_skills_ponytail_audit_skill_ponytail_audit, _agents_skills_ponytail_debt_skill_ponytail_debt, _agents_skills_ponytail_gain_skill_ponytail_gain, _agents_skills_ponytail_help_skill_ponytail_help [EXTRACTED 1.00]
- **Shortcut Debt Lifecycle** — agents_shortcut_convention, _agents_skills_ponytail_skill_the_ladder, _agents_skills_ponytail_debt_skill_debt_ledger, _agents_skills_ponytail_audit_skill_ponytail_audit [INFERRED 0.85]

## Communities (4 total, 0 thin omitted)

### Community 0 - "Audit & Code Review"
Cohesion: 0.33
Nodes (4): Quality Audit Order of Importance, Ponytail Audit Skill, Ponytail Review Skill, Quality Review Findings Categories

### Community 1 - "Debt Ledger & Benchmarks"
Cohesion: 0.33
Nodes (4): Shortcut Debt Ledger, Ponytail Debt Skill, Ponytail Gain Skill, Ponytail Benchmark Scoreboard

### Community 2 - "Core Skill & Help Interface"
Cohesion: 0.50
Nodes (3): Ponytail Help Skill, Ponytail Modes (Lite, Full, Ultra), Ponytail Skill

### Community 3 - "Lazy Senior Dev Rules"
Cohesion: 0.40
Nodes (4): The Ladder (Smallest Complete Change), Lazy Senior Developer Mode, Shortcut Comment Convention, Smallest Complete Change

## Knowledge Gaps
- **5 isolated node(s):** `Lazy Senior Developer Mode`, `Quality Audit Order of Importance`, `Quality Review Findings Categories`, `Shortcut Debt Ledger`, `Ponytail Benchmark Scoreboard`
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 6 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Ponytail Help Skill` connect `Core Skill & Help Interface` to `Audit & Code Review`, `Debt Ledger & Benchmarks`?**
  _High betweenness centrality (0.355) - this node is a cross-community bridge._
- **What connects `Lazy Senior Developer Mode`, `Quality Audit Order of Importance`, `Quality Review Findings Categories` to the rest of the system?**
  _5 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Why does `Shortcut Comment Convention` connect `Lazy Senior Dev Rules` to `Audit & Code Review`, `Debt Ledger & Benchmarks`?**
  _High betweenness centrality (0.258) - this node is a cross-community bridge._
- **Why does `Ponytail Audit Skill` connect `Audit & Code Review` to `Debt Ledger & Benchmarks`, `Core Skill & Help Interface`, `Lazy Senior Dev Rules`?**
  _High betweenness centrality (0.242) - this node is a cross-community bridge._