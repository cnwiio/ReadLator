# Ponytail Audit — Nice to Have (v6.8)

> **Scope:** Minor HTTP server efficiency and cleaning up non-test benchmark script + tracked binary build artifacts.
> **Target:** Agent-actionable specification. Numbered 14–15 (continuing from `audit-should-fix.md` items 8–13).

---

## Summary Table

| # | Area | File | Core Issue |
|---|---|---|---|
| **14** | Wi-Fi Server Path Filtering | `src/NovelTranslator.Core/Export/LocalWifiServer.cs` | `HandleClientAsync` only filters `/favicon.ico` and renders the entire story HTML from SQLite on any unknown path such as `/apple-touch-icon.png` or `/manifest.json` |
| **15** | Benchmark Test & Tracked Binlog (`-345` lines) | `tests/NovelTranslator.Tests/ResourceBenchmarkTests.cs`, `build.binlog` | `ResourceBenchmarkTests` runs forced `GC.Collect()`, 50 SQLite inserts, and `Assert.True(true)` on every `dotnet test`, and `build.binlog` is tracked in Git |

---

## 14. `LocalWifiServer` rebuilds the entire story HTML from SQLite on unknown browser paths like `/apple-touch-icon.png`
**Location:**
- [LocalWifiServer.cs](file:///d:/Work/Other/ReadLator/src/NovelTranslator.Core/Export/LocalWifiServer.cs#L120-L160)

- **What this is:** Handling incoming HTTP requests in the local Wi-Fi mobile reader server.
- **Problem:** `HandleClientAsync` (`L123-127`) only short-circuits `/favicon.ico`. Every other non-root path automatically requested by mobile browsers (e.g. `/apple-touch-icon.png`, `/apple-touch-icon-precomposed.png`, `/manifest.json`, `/robots.txt`) falls through to querying all stories and chapters from SQLite and serializing the full story HTML.
- **Fix:** Extract the path before `?` from `rawTarget` (`int q = rawTarget.IndexOf('?'); string path = q >= 0 ? rawTarget[..q] : rawTarget;`) and return `404 Not Found` (or `204 No Content`) whenever `!string.Equals(path, "/", StringComparison.Ordinal)`.
- **If we skip it:** Mobile browsers trigger redundant SQLite queries and multi-megabyte HTML serialization for background icon/metadata requests.

---

## 15. Remove `ResourceBenchmarkTests.cs` from the unit test suite and untrack `build.binlog` from Git
**Location:**
- [ResourceBenchmarkTests.cs](file:///d:/Work/Other/ReadLator/tests/NovelTranslator.Tests/ResourceBenchmarkTests.cs#L1-L345)
- [build.binlog](file:///d:/Work/Other/ReadLator/build.binlog)
- [.gitignore](file:///d:/Work/Other/ReadLator/.gitignore)

- **What this is:** A 345-line manual profiling script marked `[Fact]` inside `NovelTranslator.Tests` and an MSBuild binary log file committed to the repository root.
- **Problem:** `ResourceBenchmarkTests.cs` forces `GC.Collect()` multiple times, inserts 50 chapters into SQLite, contains a duplicate `FindMatchingEntries_Optimized` helper whose `if`/`else` branches both add the entry (`L232-239`), and asserts only `Assert.True(true)` (`L210`). Meanwhile, `build.binlog` is tracked in Git and exposes local build paths and environment variables.
- **Fix:** Delete `tests/NovelTranslator.Tests/ResourceBenchmarkTests.cs` (or mark `[Fact(Skip = "Manual benchmark")]` if kept for profiling), run `git rm --cached build.binlog`, and add `*.binlog` (and optionally `audit-*.md` if not meant to be committed long-term) to `.gitignore`.
- **If we skip it:** Every `dotnet test` run spends time executing a no-assertion GC/DB profiling loop, and local MSBuild binary logs stay tracked in Git.
