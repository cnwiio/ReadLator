using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NovelTranslator.App.ViewModels;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Models;
using NovelTranslator.Core.Settings;
using Xunit;

namespace NovelTranslator.Tests;

public class FtsAndGlossaryExtractorTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly NovelDatabase _database;
    private readonly NovelRepository _repository;

    public FtsAndGlossaryExtractorTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fts_{Path.GetRandomFileName()}.db");
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
    public async Task FtsSearch_FindsMatchesInTranslatedAndOriginalText()
    {
        var story = new Story { Title = "FTS Story" };
        long storyId = await _repository.InsertStoryAsync(story);

        var ch1 = new Chapter
        {
            StoryId = storyId,
            Ordinal = 1,
            Title = "Chapter 1",
            OriginalText = "The dragon soared into the night sky.",
            TranslatedText = "มังกรทะยานขึ้นสู่ท้องฟ้ายามค่ำคืนอย่างสง่างาม"
        };
        ch1.Id = await _repository.InsertChapterAsync(ch1);

        // 1. Search Translated Text
        var transResults = await _repository.SearchFtsAsync(storyId, "มังกร", searchTranslated: true);
        Assert.NotEmpty(transResults);
        Assert.Equal(ch1.Id, transResults[0].ChapterId);
        Assert.Contains("มังกร", transResults[0].Snippet);

        // 2. Search Original Text
        var origResults = await _repository.SearchFtsAsync(storyId, "dragon", searchTranslated: false);
        Assert.NotEmpty(origResults);
        Assert.Equal(ch1.Id, origResults[0].ChapterId);
    }

    [Fact]
    public void GlossaryExtractor_ParsesJsonAndFiltersExistingTerms()
    {
        string json = @"[
            { ""source"": ""Lin Feng"", ""target"": ""หลินเฟิง"", ""category"": ""Person"", ""notes"": ""ตัวเอก"" },
            { ""source"": ""Sword Sect"", ""target"": ""สำนักกระบี่"", ""category"": ""Location"", ""notes"": ""นิกายหลัก"" },
            { ""source"": ""Old Guy"", ""target"": ""ตาแก่"", ""category"": ""Person"", ""notes"": ""มีอยู่แล้ว"" }
        ]";

        var existing = new System.Collections.Generic.HashSet<string> { "old guy" };
        var terms = NovelTranslator.Core.Glossary.GlossaryExtractor.ParseAndFilterResults(json, existing);

        Assert.Equal(2, terms.Count);
        Assert.Equal("Lin Feng", terms[0].SourceTerm);
        Assert.Equal("หลินเฟิง", terms[0].TargetTerm);
        Assert.Equal(GlossaryCategory.Person, terms[0].Category);

        Assert.Equal("Sword Sect", terms[1].SourceTerm);
        Assert.Equal(GlossaryCategory.Location, terms[1].Category);
    }

    [Fact]
    public void GlossaryExtractor_IgnoresTrailingBracketedNotes()
    {
        string jsonWithNote = """
            ```json
            [
                { "source": "Jade Seal", "target": "ตราหยก", "category": "Term", "notes": "ของวิเศษ" }
            ]
            ```
            [หมายเหตุ: ดึงเฉพาะคำสำคัญจากเนื้อหา]
            """;

        var terms = NovelTranslator.Core.Glossary.GlossaryExtractor.ParseAndFilterResults(jsonWithNote, new System.Collections.Generic.HashSet<string>());

        Assert.Single(terms);
        Assert.Equal("Jade Seal", terms[0].SourceTerm);
    }

    [Fact]
    public void GlossaryExtractor_StripsThinkTagsAndBuildsCustomPrompt()
    {
        string thinkResponse = """
            <think>
            Let me analyze [character names] and [skills] first.
            </think>
            [
                { "source": "Azure Flame", "target": "เพลิงคราม", "category": "Term", "notes": "ธาตุไฟ" }
            ]
            """;

        var terms = NovelTranslator.Core.Glossary.GlossaryExtractor.ParseAndFilterResults(
            thinkResponse,
            new System.Collections.Generic.HashSet<string>());

        Assert.Single(terms);
        Assert.Equal("Azure Flame", terms[0].SourceTerm);

        string withPlaceholders = NovelTranslator.Core.Glossary.GlossaryExtractor.BuildExtractionPrompt(
            "Custom extract:\n{SourceText}\n=>\n{TranslatedText}",
            "SRC",
            "TGT");
        Assert.Equal("Custom extract:\nSRC\n=>\nTGT", withPlaceholders);

        string omittedPlaceholders = NovelTranslator.Core.Glossary.GlossaryExtractor.BuildExtractionPrompt(
            "Only extract character names.",
            "SRC",
            "TGT");
        Assert.Contains("[ต้นฉบับ]\nSRC", omittedPlaceholders);
        Assert.Contains("[คำแปล]\nTGT", omittedPlaceholders);
    }

    [Fact]
    public async Task AutoExtractAndSaveGlossaryAsync_FiltersSentencesSavesAndNotifiesNewestFirst()
    {
        // Rejects >5 word sentence and sentence punctuation, deduplicates within same batch
        string json = """
            [
                { "source": "Arthur opened the iron gate slowly.", "target": "อาร์เธอร์เปิดประตูเหล็กอย่างช้าๆ", "category": "Person", "notes": "sentence" },
                { "source": "This is definitely a very long phrase exceeding five words", "target": "ยาวเกิน", "category": "Term", "notes": "too long" },
                { "source": "Kael", "target": "คาเอล", "category": "Person", "notes": "ตัวเอก" },
                { "source": "kael", "target": "คาเอลซ้ำ", "category": "Person", "notes": "ซ้ำ" },
                { "source": "Shadow Step", "target": "ก้าวเงา", "category": "Term", "notes": "สกิล" }
            ]
            """;

        var filtered = NovelTranslator.Core.Glossary.GlossaryExtractor.ParseAndFilterResults(json, new HashSet<string>());
        Assert.Equal(2, filtered.Count);
        Assert.Equal("Kael", filtered[0].SourceTerm);
        Assert.Equal("Shadow Step", filtered[1].SourceTerm);

        var story = new Story { Title = "Glossary Auto Save Story" };
        story.Id = await _repository.InsertStoryAsync(story);
        await _repository.InsertGlossaryEntryAsync(new GlossaryEntry
        {
            StoryId = story.Id,
            SourceTerm = "OldTerm",
            TargetTerm = "คำเก่า",
            Category = GlossaryCategory.Term
        });

        var chapter = new Chapter
        {
            StoryId = story.Id,
            Ordinal = 1,
            Title = "ตอนที่ 1",
            OriginalText = "Kael used Shadow Step.",
            TranslatedText = "คาเอลใช้ก้าวเงา",
            Status = ChapterStatus.Translated
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        var vm = new MainViewModel(new AppSettings(), _database, deferInitialLoad: true);
        var added = await vm.AutoExtractAndSaveGlossaryAsync(story, chapter, CancellationToken.None, new SingleResponseLlmClient(json));

        Assert.Equal(2, added.Count);
        Assert.True(vm.HasAutoGlossaryNotification);
        Assert.Contains("Kael → คาเอล", vm.AutoGlossaryNotificationText);
        Assert.Contains("Shadow Step → ก้าวเงา", vm.AutoGlossaryNotificationText);

        var allEntries = await _repository.GetGlossaryByStoryAsync(story.Id);
        Assert.Equal(3, allEntries.Count);
        // Newest entries sorted first (ORDER BY Id DESC)
        Assert.Equal("Shadow Step", allEntries[0].SourceTerm);
        Assert.Equal("Kael", allEntries[1].SourceTerm);
        Assert.Equal("OldTerm", allEntries[2].SourceTerm);
    }

    private sealed class SingleResponseLlmClient : ILlmClient
    {
        private readonly string _response;
        public SingleResponseLlmClient(string response) => _response = response;

        public IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, float temperature, CancellationToken cancellationToken = default)
            => StreamChatAsync(messages, temperature, null, cancellationToken);

        public async IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            float temperature,
            string? modelOverride,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return _response;
        }

        public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ModelInfo>());

        public Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((true, "OK"));
    }
}
