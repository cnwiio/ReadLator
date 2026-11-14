using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NovelTranslator.App.ViewModels;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Export;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Models;
using NovelTranslator.Core.Settings;
using NovelTranslator.Core.Translation;
using Xunit;

namespace NovelTranslator.Tests;

public class NewFeaturesTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly NovelDatabase _database;
    private readonly NovelRepository _repository;

    public NewFeaturesTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_new_{Path.GetRandomFileName()}.db");
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

    [Theory]
    [InlineData("ch_001", "Chapter 12: The Awakening\nOnce upon a time...", 1, "ตอนที่ 12: The Awakening")]
    [InlineData("My Novel - Chapter 4: Rain | RoyalRoad", "Rain fell softly on the roof.", 1, "ตอนที่ 4: Rain")]
    [InlineData("003_The_Trial", "The hall was silent.", 3, "ตอนที่ 3: The Trial")]
    [InlineData("005", "Silent night.", 5, "ตอนที่ 5")]
    [InlineData("raw_dump", "第15章 剑气", 1, "ตอนที่ 15: 剑气")]
    public void SmartChapterNaming_ResolvesCleanThaiNormalizedTitles(string rawName, string content, int ordinal, string expected)
    {
        string actual = SmartChapterNaming.ResolveTitle(rawName, content, ordinal);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task UndoDeleteChapter_RestoresChapterAndChunksAtOriginalOrdinal()
    {
        var story = new Story { Title = "Undo Story" };
        story.Id = await _repository.InsertStoryAsync(story);

        var ch1 = new Chapter { StoryId = story.Id, Ordinal = 1, Title = "ตอนที่ 1", OriginalText = "One", TranslatedText = "หนึ่ง", Status = ChapterStatus.Translated };
        var ch2 = new Chapter { StoryId = story.Id, Ordinal = 2, Title = "ตอนที่ 2", OriginalText = "Two", TranslatedText = "สอง", Status = ChapterStatus.Translated };
        var ch3 = new Chapter { StoryId = story.Id, Ordinal = 3, Title = "ตอนที่ 3", OriginalText = "Three", TranslatedText = "สาม", Status = ChapterStatus.Translated };
        ch1.Id = await _repository.InsertChapterAsync(ch1);
        ch2.Id = await _repository.InsertChapterAsync(ch2);
        ch3.Id = await _repository.InsertChapterAsync(ch3);

        await _repository.SaveChunksAsync(ch2.Id, new List<string> { "Two" });
        var chunks = await _repository.GetChunksByChapterAsync(ch2.Id);
        chunks[0].TranslatedText = "สอง";
        chunks[0].IsCompleted = true;
        await _repository.UpdateChunkAsync(chunks[0]);

        var vm = new MainViewModel(new AppSettings(), _database, deferInitialLoad: true);
        vm.SelectedStory = story;
        await Task.Delay(150);
        vm.SelectedChapter = vm.Chapters[1];
        vm.RequestConfirmDialog = _ => Task.FromResult(true);

        await vm.DeleteChapterAsync();

        Assert.True(vm.CanUndoDeleteChapter);
        Assert.Equal(2, vm.Chapters.Count);
        Assert.DoesNotContain(vm.Chapters, c => c.Title == "ตอนที่ 2");

        await vm.UndoDeleteChapterAsync();

        Assert.False(vm.CanUndoDeleteChapter);
        Assert.Equal(3, vm.Chapters.Count);
        Assert.Equal("ตอนที่ 2", vm.Chapters[1].Title);
        Assert.Equal(2, vm.Chapters[1].Ordinal);

        var restoredChunks = await _repository.GetChunksByChapterAsync(vm.Chapters[1].Id);
        Assert.Single(restoredChunks);
        Assert.True(restoredChunks[0].IsCompleted);
        Assert.Equal("สอง", restoredChunks[0].TranslatedText);
    }

    private sealed class FakeStreamingLlmClient : ILlmClient
    {
        private readonly string[] _tokens;
        public FakeStreamingLlmClient(params string[] tokens) => _tokens = tokens;

        public Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((true, "OK"));

        public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ModelInfo>());

        public async IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            float temperature = 0.3f,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var token in _tokens)
            {
                await Task.Yield();
                yield return token;
            }
        }
    }

    [Theory]
    [InlineData(TranslationAppearanceMode.PerChunk, false)]
    [InlineData(TranslationAppearanceMode.WordByWord, true)]
    public async Task TranslationPipeline_RespectsTranslationAppearanceMode(TranslationAppearanceMode mode, bool expectStreamingTextUpdates)
    {
        var story = new Story { Title = "Appearance Story" };
        story.Id = await _repository.InsertStoryAsync(story);
        var chapter = new Chapter { StoryId = story.Id, Ordinal = 1, Title = "ตอนที่ 1", OriginalText = "Hello world." };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        var settings = new AppSettings { TranslationAppearance = mode };
        var fakeLlm = new FakeStreamingLlmClient("สวัสดี", " ", "ชาวโลก", " ", "ทั้งหลาย", "!");
        var pipeline = new TranslationPipeline(_repository, fakeLlm, settings);

        var updates = new List<TranslationProgressInfo>();
        var syncProgress = new SynchronousProgress<TranslationProgressInfo>(info => updates.Add(info));

        await pipeline.TranslateChapterAsync(story, chapter, syncProgress, forceRetranslate: true);

        bool hadIntermediateCombinedText = updates.Any(u => u.StreamedWordCount > 0 && !string.IsNullOrEmpty(u.CombinedTranslatedTextSoFar));
        Assert.Equal(expectStreamingTextUpdates, hadIntermediateCombinedText);
        Assert.Equal("สวัสดี ชาวโลก ทั้งหลาย!", chapter.TranslatedText);
    }

    [Fact]
    public async Task ImportWebChapterAsync_ReplacesStarterPlaceholderInChapter1()
    {
        var story = new Story { Title = "New Story" };
        story.Id = await _repository.InsertStoryAsync(story);
        var ch1 = new Chapter
        {
            StoryId = story.Id,
            Ordinal = 1,
            Title = "ตอนที่ 1",
            OriginalText = "Paste your English story here...\n\nThen click the 'แปล' button!"
        };
        ch1.Id = await _repository.InsertChapterAsync(ch1);

        var vm = new NovelTranslator.App.ViewModels.MainViewModel(new AppSettings(), _database, deferInitialLoad: true);
        vm.Stories.Add(story);
        vm.SelectedStory = story;
        for (int i = 0; i < 20 && vm.Chapters.Count == 0; i++)
        {
            await Task.Delay(25);
        }
        Assert.Single(vm.Chapters);
        vm.SelectedChapter = vm.Chapters[0];

        await vm.ImportWebChapterAsync(new NovelTranslator.Core.Export.WebChapterResult("Chapter One", "Imported web content."));

        Assert.Single(vm.Chapters);
        Assert.Equal("Chapter One", vm.SelectedChapter.Title);
        Assert.Equal("Imported web content.", vm.OriginalText);
    }

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SynchronousProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }
}
