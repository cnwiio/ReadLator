using System;
using System.IO;
using System.Threading.Tasks;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Models;
using Xunit;

namespace NovelTranslator.Tests;

public class RepositoryTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly NovelDatabase _database;
    private readonly NovelRepository _repository;

    public RepositoryTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_novel_{Path.GetRandomFileName()}.db");
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
    public void CheckIntegrity_ReturnsTrue()
    {
        Assert.True(_database.CheckIntegrity());
    }

    [Fact]
    public async Task StoryAndChapter_Crud_WorksCorrectly()
    {
        // 1. Insert Story
        var story = new Story { Title = "Test Story", SourceLang = "English", TargetLang = "Thai" };
        long storyId = await _repository.InsertStoryAsync(story);
        Assert.True(storyId > 0);

        // 2. Insert Chapter
        var chapter = new Chapter
        {
            StoryId = storyId,
            Ordinal = 1,
            Title = "Chapter 1",
            OriginalText = "Once upon a time."
        };
        long chapterId = await _repository.InsertChapterAsync(chapter);
        Assert.True(chapterId > 0);

        // 3. Save Chunks
        await _repository.SaveChunksAsync(chapterId, new() { "Once upon a time." });
        var chunks = await _repository.GetChunksByChapterAsync(chapterId);
        Assert.Single(chunks);
        Assert.Equal("Once upon a time.", chunks[0].SourceText);

        // 4. Update Chunk status
        chunks[0].TranslatedText = "กาลครั้งหนึ่งนานมาแล้ว";
        chunks[0].IsCompleted = true;
        await _repository.UpdateChunkAsync(chunks[0]);

        var updatedChunks = await _repository.GetChunksByChapterAsync(chapterId);
        Assert.True(updatedChunks[0].IsCompleted);
        Assert.Equal("กาลครั้งหนึ่งนานมาแล้ว", updatedChunks[0].TranslatedText);
    }

    [Fact]
    public async Task SaveChunks_PreservesCompletedChunksWhenNewChunkAppended()
    {
        var story = new Story { Title = "Test Story", SourceLang = "English", TargetLang = "Thai" };
        long storyId = await _repository.InsertStoryAsync(story);

        var chapter = new Chapter
        {
            StoryId = storyId,
            Ordinal = 1,
            Title = "Chapter 1",
            OriginalText = "Part 1"
        };
        long chapterId = await _repository.InsertChapterAsync(chapter);

        // 1. First save with 1 chunk and complete it
        await _repository.SaveChunksAsync(chapterId, new() { "Part 1" });
        var chunks = await _repository.GetChunksByChapterAsync(chapterId);
        chunks[0].TranslatedText = "แปลส่วนที่ 1";
        chunks[0].IsCompleted = true;
        await _repository.UpdateChunkAsync(chunks[0]);

        // 2. Add Part 2 without force reset
        await _repository.SaveChunksAsync(chapterId, new() { "Part 1", "Part 2" }, forceReset: false);
        var newChunks = await _repository.GetChunksByChapterAsync(chapterId);

        Assert.Equal(2, newChunks.Count);
        // Part 1 should retain translation and completion status
        Assert.Equal("แปลส่วนที่ 1", newChunks[0].TranslatedText);
        Assert.True(newChunks[0].IsCompleted);
        // Part 2 should be empty and not completed
        Assert.Equal("", newChunks[1].TranslatedText);
        Assert.False(newChunks[1].IsCompleted);

        // 3. Force reset should clear all
        await _repository.SaveChunksAsync(chapterId, new() { "Part 1", "Part 2" }, forceReset: true);
        var resetChunks = await _repository.GetChunksByChapterAsync(chapterId);
        Assert.Equal(2, resetChunks.Count);
        Assert.False(resetChunks[0].IsCompleted);
        Assert.Equal("", resetChunks[0].TranslatedText);
    }

    [Fact]
    public async Task UpdateChapterOrdinals_BatchUpdatesCorrectly()
    {
        var story = new Story { Title = "Story For Ordinals" };
        long storyId = await _repository.InsertStoryAsync(story);

        var ch1 = new Chapter { StoryId = storyId, Ordinal = 1, Title = "Ch 1" };
        var ch2 = new Chapter { StoryId = storyId, Ordinal = 2, Title = "Ch 2" };
        var ch3 = new Chapter { StoryId = storyId, Ordinal = 3, Title = "Ch 3" };
        ch1.Id = await _repository.InsertChapterAsync(ch1);
        ch2.Id = await _repository.InsertChapterAsync(ch2);
        ch3.Id = await _repository.InsertChapterAsync(ch3);

        // Swap order: 3, 1, 2
        var updates = new (long, int)[]
        {
            (ch3.Id, 1),
            (ch1.Id, 2),
            (ch2.Id, 3)
        };
        await _repository.UpdateChapterOrdinalsAsync(updates);

        var fetched = await _repository.GetChaptersByStoryAsync(storyId);
        Assert.Equal(3, fetched.Count);
        Assert.Equal("Ch 3", fetched[0].Title);
        Assert.Equal(1, fetched[0].Ordinal);
        Assert.Equal("Ch 1", fetched[1].Title);
        Assert.Equal(2, fetched[1].Ordinal);
        Assert.Equal("Ch 2", fetched[2].Title);
        Assert.Equal(3, fetched[2].Ordinal);
    }

    [Fact]
    public async Task GetPreviousTranslatedChapter_ReturnsLatestTranslatedChapterBeforeCurrent()
    {
        var story = new Story { Title = "Cross Chapter Test" };
        long storyId = await _repository.InsertStoryAsync(story);

        var ch1 = new Chapter { StoryId = storyId, Ordinal = 1, Title = "ตอนที่ 1", OriginalText = "P1", TranslatedText = "บทแปล 1" };
        var ch2 = new Chapter { StoryId = storyId, Ordinal = 2, Title = "ตอนที่ 2", OriginalText = "P2", TranslatedText = "" }; // ยังไม่แปล
        var ch3 = new Chapter { StoryId = storyId, Ordinal = 3, Title = "ตอนที่ 3", OriginalText = "P3", TranslatedText = "บทแปล 3" };
        var ch4 = new Chapter { StoryId = storyId, Ordinal = 4, Title = "ตอนที่ 4", OriginalText = "P4", TranslatedText = "" }; // ตอนปัจจุบัน

        ch1.Id = await _repository.InsertChapterAsync(ch1);
        ch2.Id = await _repository.InsertChapterAsync(ch2);
        ch3.Id = await _repository.InsertChapterAsync(ch3);
        ch4.Id = await _repository.InsertChapterAsync(ch4);

        var prev = await _repository.GetPreviousTranslatedChapterAsync(storyId, 4);
        Assert.NotNull(prev);
        Assert.Equal(ch3.Id, prev.Id);
        Assert.Equal("ตอนที่ 3", prev.Title);

        // ตอนที่ 1 ไม่มีตอนก่อนหน้า
        var none = await _repository.GetPreviousTranslatedChapterAsync(storyId, 1);
        Assert.Null(none);
    }

    [Fact]
    public async Task DeleteStory_CascadesToChaptersChunksAndGlossary()
    {
        long storyId = await _repository.InsertStoryAsync(new Story { Title = "Cascade Story" });
        long chapterId = await _repository.InsertChapterAsync(new Chapter
        {
            StoryId = storyId,
            Ordinal = 1,
            Title = "Ch 1",
            OriginalText = "Hello"
        });
        await _repository.SaveChunksAsync(chapterId, new() { "Hello" });
        await _repository.InsertGlossaryEntryAsync(new GlossaryEntry
        {
            StoryId = storyId,
            SourceTerm = "Hero",
            TargetTerm = "ฮีโร่"
        });

        await _repository.DeleteStoryAsync(storyId);

        Assert.Empty(await _repository.GetChaptersByStoryAsync(storyId));
        Assert.Empty(await _repository.GetChunksByChapterAsync(chapterId));
        Assert.Empty(await _repository.GetGlossaryByStoryAsync(storyId));
    }

    [Fact]
    public async Task SearchReplace_WithDollarSignInReplacement_ReplacesLiterally()
    {
        var story = new Story { Title = "Dollar Story" };
        story.Id = await _repository.InsertStoryAsync(story);
        var chapter = new Chapter
        {
            StoryId = story.Id,
            Ordinal = 1,
            Title = "Ch 1",
            TranslatedText = "Cost is OLD_PRICE."
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        var vm = new NovelTranslator.App.ViewModels.SearchReplaceViewModel(_repository, story, chapter)
        {
            SearchText = "old_price",
            ReplaceText = "$100"
        };

        await vm.ApplyReplaceAsync();

        var updated = await _repository.GetChapterByIdAsync(chapter.Id);
        Assert.NotNull(updated);
        Assert.Equal("Cost is $100.", updated!.TranslatedText);
    }

    [Fact]
    public async Task TranslateChapter_Resume_PreservesManualEdits()
    {
        var story = new Story { Title = "Resume Story", SourceLang = "English", TargetLang = "Thai" };
        story.Id = await _repository.InsertStoryAsync(story);

        var chapter = new Chapter
        {
            StoryId = story.Id,
            Ordinal = 1,
            Title = "Ch 1",
            OriginalText = "First paragraph.\n\nSecond paragraph.",
            TranslatedText = "ย่อหน้าแรก (แก้แล้ว)"
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        // Save chunk 0 as already completed with old AI output, and chunk 1 as uncompleted
        var settings = new NovelTranslator.Core.Settings.AppSettings { MaxChunkWords = 2 };
        var splitTexts = NovelTranslator.Core.Translation.Chunker.SplitIntoChunks(chapter.OriginalText, settings.MaxChunkWords);
        Assert.True(splitTexts.Count >= 2);
        await _repository.SaveChunksAsync(chapter.Id, splitTexts);
        var chunks = await _repository.GetChunksByChapterAsync(chapter.Id);
        chunks[0].TranslatedText = "ย่อหน้าแรก (AI เก่า)";
        chunks[0].IsCompleted = true;
        await _repository.UpdateChunkAsync(chunks[0]);

        var pipeline = new NovelTranslator.Core.Translation.TranslationPipeline(_repository, new FakeLlmClient("ย่อหน้าสอง (AI ใหม่)"), settings);
        await pipeline.TranslateChapterAsync(story, chapter, new Progress<NovelTranslator.Core.Translation.TranslationProgressInfo>());

        Assert.StartsWith("ย่อหน้าแรก (แก้แล้ว)", chapter.TranslatedText);
        Assert.Contains("ย่อหน้าสอง (AI ใหม่)", chapter.TranslatedText);
        Assert.DoesNotContain("ย่อหน้าแรก (AI เก่า)", chapter.TranslatedText);
    }

    private sealed class FakeLlmClient : NovelTranslator.Core.Llm.ILlmClient
    {
        private readonly string _response;
        public FakeLlmClient(string response) => _response = response;

        public async IAsyncEnumerable<string> StreamChatAsync(
            System.Collections.Generic.IEnumerable<NovelTranslator.Core.Llm.ChatMessage> messages,
            float temperature,
            [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return _response;
        }

        public Task<System.Collections.Generic.List<NovelTranslator.Core.Llm.ModelInfo>> GetModelsAsync(System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult(new System.Collections.Generic.List<NovelTranslator.Core.Llm.ModelInfo>());

        public Task<(bool Success, string Message)> TestConnectionAsync(System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult((true, "OK"));

        public Task PullOllamaModelAsync(string modelName, IProgress<string> progress, System.Threading.CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
