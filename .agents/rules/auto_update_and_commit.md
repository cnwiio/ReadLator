---
trigger: always_on
description: Auto-increment program version by +0.1, update graphify knowledge graph, and git commit (without pushing) using Conventional Commits after every code change.
---

## Post-Change Workflow Rules

Whenever code or feature modifications are completed in a task:

1. **Auto-Increment Version (+0.1):**
   - Bump the version by `+0.1` (e.g. `1.0` -> `1.1`, `1.1` -> `1.2`):
     - In `src/NovelTranslator.App/NovelTranslator.App.csproj`: `<Version>X.Y.0</Version>`
     - In `src/NovelTranslator.App/Views/MainWindow.axaml`: `Title="โปรแกรมแปลนิยายด้วย AI (NovelTranslator) vX.Y"`
2. **Update Graphify Knowledge Graph:**
   - Run graphify update using:
     ```powershell
     & "C:\Users\kritg\AppData\Roaming\uv\tools\graphifyy\Scripts\python.exe" -m graphify.cli update .
     ```
     (with `BypassSandbox: true` on Windows) to keep the AST graph and documentation current.
3. **Auto Commit to Git (Do Not Push):**
   - Stage modified files and make a local Git commit using Conventional Commits format:
     ```bash
     git add -A
     git commit -m "<type>: <concise description> (vX.Y)"
     ```
     (e.g. `git commit -m "feat: smart chunk reuse and separate translate button (v1.0)"`).
   - **Never** run `git push` unless the user explicitly commands it.
