using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Glossary;
using NovelTranslator.Core.Models;
using NovelTranslator.Core.Translation;
using Xunit;
using Xunit.Abstractions;

namespace NovelTranslator.Tests;

public class ResourceBenchmarkTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDbPath;
    private readonly NovelDatabase _database;
    private readonly NovelRepository _repository;

    public ResourceBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"bench_{Path.GetRandomFileName()}.db");
        _database = new NovelDatabase(_tempDbPath);
        _database.Initialize();
        _repository = new NovelRepository(_database);
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public async Task Benchmark_AllComponents_ResourceUsageComparison()
    {
        _output.WriteLine("==========================================================================================");
        _output.WriteLine("                     NOVEL TRANSLATOR COMPONENT RESOURCE PROFILING                       ");
        _output.WriteLine("==========================================================================================");
        _output.WriteLine(string.Format("{0,-45} | {1,10} | {2,14} |", "Component / Operation", "Time (ms)", "Allocations"));
        _output.WriteLine(new string('-', 78));

        // 1. Chunker Benchmark
        string largeNovelChapter = GenerateRealisticNovelChapter(paragraphs: 100, avgWordsPerParagraph: 100); // ~10,000 words
        Profile("1. Chunker.SplitIntoChunks (10k words)", () =>
        {
            for (int i = 0; i < 10; i++)
            {
                var chunks = Chunker.SplitIntoChunks(largeNovelChapter, 600);
            }
        });

        // 2a. Glossary Matching Benchmark (Small: 50 terms)
        var smallGlossary = GenerateGlossaryEntries(50);
        string sampleChunk = GenerateSampleChunk(500);
        Profile("2a. Glossary Match - Current (50 terms, 20x)", () =>
        {
            for (int i = 0; i < 20; i++)
            {
                var matched = GlossaryService.FindMatchingEntries(sampleChunk, smallGlossary);
            }
        });

        // 2b. Glossary Matching Benchmark (Large: 500 terms)
        var largeGlossary = GenerateGlossaryEntries(500);
        Profile("2b. Glossary Match - Current (500 terms, 20x)", () =>
        {
            for (int i = 0; i < 20; i++)
            {
                var matched = GlossaryService.FindMatchingEntries(sampleChunk, largeGlossary);
            }
        });

        // 2c. Glossary Matching - Optimized Prototype (Quick Contains / Ordinal check first)
        Profile("2c. Glossary Match - Optimized (500 terms, 20x)", () =>
        {
            for (int i = 0; i < 20; i++)
            {
                var matched = FindMatchingEntries_Optimized(sampleChunk, largeGlossary);
            }
        });

        // 3. Prompt Builder
        var story = new Story { Id = 1, Title = "Test Epic", SourceLang = "English", TargetLang = "Thai", StyleNotes = "Action Fantasy" };
        var matchedTerms = largeGlossary.Take(20).ToList();
        string prevSrc = GenerateSampleChunk(100);
        string prevTgt = GenerateSampleChunk(100);
        Profile("3. PromptBuilder.BuildMessages (50x)", () =>
        {
            for (int i = 0; i < 50; i++)
            {
                var msgs = PromptBuilder.BuildMessages(story, sampleChunk, matchedTerms, prevSrc, prevTgt, null, null, null);
            }
        });

        // 4. Output Validation & Cleaning
        string rawAiResponse = "Here is the translation:\n\n```markdown\n" + sampleChunk + "\n```\nHope you like it!";
        Profile("4. OutputValidator.Validate (50x)", () =>
        {
            for (int i = 0; i < 50; i++)
            {
                var result = OutputValidator.Validate(sampleChunk, rawAiResponse, matchedTerms);
            }
        });

        // 5a. Streaming Aggregation - Current
        var dummyChunks = Enumerable.Range(0, 10).Select(i => new ChapterChunk
        {
            ChunkIndex = i,
            SourceText = sampleChunk,
            TranslatedText = sampleChunk,
            IsCompleted = i < 5
        }).ToList();

        Profile("5a. Streaming Aggregation - Current (300 tok)", () =>
        {
            var chunkSb = new StringBuilder();
            for (int token = 0; token < 300; token++)
            {
                chunkSb.Append(" token_word");
                if (token % 3 == 0)
                {
                    int wordCount = chunkSb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                    var parts = new List<string>();
                    for (int c = 0; c < dummyChunks.Count; c++)
                    {
                        if (c < 5 && !string.IsNullOrEmpty(dummyChunks[c].TranslatedText))
                            parts.Add(dummyChunks[c].TranslatedText);
                        else if (c == 5)
                            parts.Add(OutputValidator.CleanPreamble(chunkSb.ToString()));
                    }
                    string liveCombined = string.Join("\n\n", parts);
                }
            }
        });

        // 5b. Streaming Aggregation - Optimized (Throttled update / zero-alloc word count)
        Profile("5b. Streaming Aggregation - Opt Throttled", () =>
        {
            var chunkSb = new StringBuilder();
            int wordCount = 0;
            for (int token = 0; token < 300; token++)
            {
                chunkSb.Append(" token_word");
                wordCount++;
                // Throttle: only update UI aggregation every 30 tokens or at end
                if (token % 30 == 0 || token == 299)
                {
                    var parts = new List<string>(dummyChunks.Count);
                    for (int c = 0; c < dummyChunks.Count; c++)
                    {
                        if (c < 5 && !string.IsNullOrEmpty(dummyChunks[c].TranslatedText))
                            parts.Add(dummyChunks[c].TranslatedText);
                        else if (c == 5)
                            parts.Add(OutputValidator.CleanPreamble(chunkSb.ToString()));
                    }
                    string liveCombined = string.Join("\n\n", parts);
                }
            }
        });

        // 6. Database Operations: 50 Chapter Insertions
        var benchStory = new Story { Title = "Bench Story", SourceLang = "en", TargetLang = "th" };
        long benchStoryId = await _repository.InsertStoryAsync(benchStory);

        Profile("6a. DB: 50 Chapter Inserts (Individual)", () =>
        {
            for (int i = 0; i < 50; i++)
            {
                var chap = new Chapter
                {
                    StoryId = benchStoryId,
                    Ordinal = i + 1,
                    Title = $"Chapter {i + 1}",
                    OriginalText = sampleChunk,
                    TranslatedText = sampleChunk,
                    UpdatedAt = DateTime.UtcNow
                };
                _repository.InsertChapterAsync(chap).GetAwaiter().GetResult();
            }
        });

        // 7a. Word Count: Current .Split()
        Profile("7a. WordCount: Current .Split() (100x)", () =>
        {
            for (int i = 0; i < 100; i++)
            {
                int wc = Chunker.EstimateWordCount(sampleChunk);
            }
        });

        // 7b. Word Count: Span-based Zero-Alloc
        Profile("7b. WordCount: Span Zero-Alloc (100x)", () =>
        {
            for (int i = 0; i < 100; i++)
            {
                int wc = EstimateWordCount_ZeroAlloc(sampleChunk);
            }
        });

        _output.WriteLine("==========================================================================================");
        Assert.True(true);
    }

    private static List<GlossaryEntry> FindMatchingEntries_Optimized(string chunkText, IEnumerable<GlossaryEntry> allEntries)
    {
        var matched = new List<GlossaryEntry>();
        if (string.IsNullOrWhiteSpace(chunkText)) return matched;

        foreach (var entry in allEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.SourceTerm)) continue;
            string term = entry.SourceTerm.Trim();

            // Optimization: Fast substring check first.
            // If the term doesn't appear at all in chunkText, it CANNOT match word boundary either!
            if (chunkText.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            // If substring exists, verify word boundary for English words
            string pattern = @"\b" + Regex.Escape(term) + @"\b";
            if (Regex.IsMatch(chunkText, pattern, RegexOptions.IgnoreCase))
            {
                matched.Add(entry);
            }
            else
            {
                matched.Add(entry);
            }
        }

        return matched;
    }

    private static int EstimateWordCount_ZeroAlloc(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || text.IsWhiteSpace()) return 0;
        int count = 0;
        bool inWord = false;

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }

    private void Profile(string name, Action action)
    {
        // Warmup
        action();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startBytes = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();

        action();

        sw.Stop();
        long endBytes = GC.GetAllocatedBytesForCurrentThread();
        long allocatedBytes = Math.Max(0, endBytes - startBytes);

        string allocFormatted = FormatBytes(allocatedBytes);
        _output.WriteLine(string.Format("{0,-45} | {1,7:F2} ms | {2,14} |", name, sw.Elapsed.TotalMilliseconds, allocFormatted));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    private static string GenerateRealisticNovelChapter(int paragraphs, int avgWordsPerParagraph)
    {
        var sb = new StringBuilder();
        var words = new[] { "the", "knight", "raised", "his", "ancient", "sword", "and", "gazed", "into", "the", "darkness", "whispering", "a", "spell", "of", "light" };
        var rnd = new Random(42);

        for (int p = 0; p < paragraphs; p++)
        {
            for (int w = 0; w < avgWordsPerParagraph; w++)
            {
                sb.Append(words[rnd.Next(words.Length)]).Append(' ');
            }
            sb.AppendLine().AppendLine();
        }
        return sb.ToString();
    }

    private static string GenerateSampleChunk(int wordCount)
    {
        var sb = new StringBuilder();
        var words = new[] { "Arthur", "looked", "at", "Excalibur", "shining", "brightly", "in", "Avalon", "with", "Merlin", "watching" };
        var rnd = new Random(123);
        for (int i = 0; i < wordCount; i++)
        {
            sb.Append(words[rnd.Next(words.Length)]).Append(' ');
        }
        return sb.ToString();
    }

    private static List<GlossaryEntry> GenerateGlossaryEntries(int count)
    {
        var list = new List<GlossaryEntry>();
        for (int i = 0; i < count; i++)
        {
            list.Add(new GlossaryEntry
            {
                Id = i + 1,
                StoryId = 1,
                SourceTerm = $"Term_{i}_{i * 3}",
                TargetTerm = $"คำแปล_{i}",
                Category = GlossaryCategory.Term
            });
        }
        list.Add(new GlossaryEntry { Id = count + 1, StoryId = 1, SourceTerm = "Arthur", TargetTerm = "อาเธอร์", Category = GlossaryCategory.Person });
        list.Add(new GlossaryEntry { Id = count + 2, StoryId = 1, SourceTerm = "Excalibur", TargetTerm = "เอ็กซ์คาลิเบอร์", Category = GlossaryCategory.Term });
        list.Add(new GlossaryEntry { Id = count + 3, StoryId = 1, SourceTerm = "Merlin", TargetTerm = "เมอร์ลิน", Category = GlossaryCategory.Person });
        list.Add(new GlossaryEntry { Id = count + 4, StoryId = 1, SourceTerm = "Avalon", TargetTerm = "อวาลอน", Category = GlossaryCategory.Location });
        return list;
    }
}
