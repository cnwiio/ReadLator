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
}
