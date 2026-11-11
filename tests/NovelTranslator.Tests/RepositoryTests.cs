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
}
