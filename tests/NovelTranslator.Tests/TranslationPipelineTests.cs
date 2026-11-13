using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Models;
using NovelTranslator.Core.Settings;
using NovelTranslator.Core.Translation;
using Xunit;

namespace NovelTranslator.Tests;

public class TranslationPipelineTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly NovelDatabase _database;
    private readonly NovelRepository _repository;

    public TranslationPipelineTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_pipeline_{Path.GetRandomFileName()}.db");
        _database = new NovelDatabase(_tempDbPath);
        _database.Initialize();
        _repository = new NovelRepository(_database);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public async Task TranslateChapterAsync_MultiChunk_AssemblesAllChunksInOrder()
    {
        var story = new Story { Title = "Multi-Chunk Story" };
        story.Id = await _repository.InsertStoryAsync(story);

        var chapter = new Chapter
        {
            StoryId = story.Id,
            Ordinal = 1,
            Title = "Ch 1",
            OriginalText = "First paragraph here.\n\nSecond paragraph here."
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        var settings = new AppSettings { MaxChunkWords = 3 };
        var llm = new QueueLlmClient("ย่อหน้าหนึ่ง", "ย่อหน้าสอง");
        var pipeline = new TranslationPipeline(_repository, llm, settings);

        await pipeline.TranslateChapterAsync(story, chapter, new Progress<TranslationProgressInfo>());

        Assert.Equal("ย่อหน้าหนึ่ง\n\nย่อหน้าสอง", chapter.TranslatedText);
        Assert.Equal(ChapterStatus.Translated, chapter.Status);
        Assert.Equal(2, llm.CallCount);
    }

    [Fact]
    public async Task TranslateChapterAsync_RetriesWhenValidationFailsOnFirstAttempt()
    {
        var story = new Story { Title = "Retry Story" };
        story.Id = await _repository.InsertStoryAsync(story);

        var chapter = new Chapter
        {
            StoryId = story.Id,
            Ordinal = 1,
            Title = "Ch 1",
            OriginalText = "Hello world."
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        var settings = new AppSettings { MaxChunkWords = 50 };
        // Attempt 1 returns empty output (fails OutputValidator), Attempt 2 returns Thai
        var llm = new QueueLlmClient("", "สวัสดีชาวโลก");
        var pipeline = new TranslationPipeline(_repository, llm, settings);

        await pipeline.TranslateChapterAsync(story, chapter, new Progress<TranslationProgressInfo>());

        Assert.Equal("สวัสดีชาวโลก", chapter.TranslatedText);
        Assert.Equal(2, llm.CallCount);
    }

    [Fact]
    public async Task TranslateChapterAsync_ResumesPartiallyCompletedChapter()
    {
        var story = new Story { Title = "Resume Story" };
        story.Id = await _repository.InsertStoryAsync(story);

        var chapter = new Chapter
        {
            StoryId = story.Id,
            Ordinal = 1,
            Title = "Ch 1",
            OriginalText = "First paragraph here.\n\nSecond paragraph here.",
            TranslatedText = "ย่อหน้าหนึ่ง (แปลไว้แล้ว)"
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        var settings = new AppSettings { MaxChunkWords = 3 };
        var splitTexts = Chunker.SplitIntoChunks(chapter.OriginalText, settings.MaxChunkWords);
        await _repository.SaveChunksAsync(chapter.Id, splitTexts);
        var chunks = await _repository.GetChunksByChapterAsync(chapter.Id);
        chunks[0].TranslatedText = "ย่อหน้าหนึ่ง (แปลไว้แล้ว)";
        chunks[0].IsCompleted = true;
        await _repository.UpdateChunkAsync(chunks[0]);

        var llm = new QueueLlmClient("ย่อหน้าสอง (แปลต่อ)");
        var pipeline = new TranslationPipeline(_repository, llm, settings);

        await pipeline.TranslateChapterAsync(story, chapter, new Progress<TranslationProgressInfo>());

        Assert.Equal("ย่อหน้าหนึ่ง (แปลไว้แล้ว)\n\nย่อหน้าสอง (แปลต่อ)", chapter.TranslatedText);
        Assert.Equal(1, llm.CallCount);
    }

    private sealed class QueueLlmClient : ILlmClient
    {
        private readonly Queue<string> _responses;
        public int CallCount { get; private set; }

        public QueueLlmClient(params string[] responses)
            => _responses = new Queue<string>(responses);

        public async IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            float temperature,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CallCount++;
            await Task.CompletedTask;
            yield return _responses.Count > 0 ? _responses.Dequeue() : string.Empty;
        }

        public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ModelInfo>());

        public Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((true, "OK"));

        public Task PullOllamaModelAsync(string modelName, IProgress<string> progress, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
