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

    [Fact]
    public async Task LlmClient_StreamChatAsync_ThrowsFriendlyErrorWhenModelNameEmpty()
    {
        var client = new LlmClient(new AppSettings { ModelName = "" });
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.StreamChatAsync(new[] { new ChatMessage { Role = "user", Content = "hi" } }, 0.3f))
            {
            }
        });
    }

    [Fact]
    public async Task TranslateChapterAsync_EditingLastChunk_ReplacesInsteadOfDuplicating()
    {
        var story = new Story { Title = "Resplit Reuse Story" };
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
        var firstPipeline = new TranslationPipeline(_repository, new QueueLlmClient("ย่อหน้าหนึ่ง", "ย่อหน้าสองเดิม"), settings);
        await firstPipeline.TranslateChapterAsync(story, chapter, new Progress<TranslationProgressInfo>());
        Assert.Equal("ย่อหน้าหนึ่ง\n\nย่อหน้าสองเดิม", chapter.TranslatedText);

        // User edits chunk 2 source text and resumes without forceRetranslate
        chapter.OriginalText = "First paragraph here.\n\nSecond paragraph edited.";
        var resumePipeline = new TranslationPipeline(_repository, new QueueLlmClient("ย่อหน้าสองใหม่"), settings);
        await resumePipeline.TranslateChapterAsync(story, chapter, new Progress<TranslationProgressInfo>());

        Assert.Equal("ย่อหน้าหนึ่ง\n\nย่อหน้าสองใหม่", chapter.TranslatedText);
        Assert.DoesNotContain("ย่อหน้าสองเดิม", chapter.TranslatedText);
    }

    [Fact]
    public async Task TranslateChapterAsync_WhenPolishModeEnabled_RunsPolishPassAndUsesPolishedText()
    {
        var story = new Story { Title = "Polish Story" };
        story.Id = await _repository.InsertStoryAsync(story);

        await _repository.InsertGlossaryEntryAsync(new GlossaryEntry
        {
            StoryId = story.Id,
            SourceTerm = "Dragon",
            TargetTerm = "มังกร"
        });

        var chapter = new Chapter
        {
            StoryId = story.Id,
            Ordinal = 1,
            Title = "Ch 1",
            OriginalText = "First Dragon here.\n\nSecond paragraph here."
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        var settings = new AppSettings
        {
            MaxChunkWords = 3,
            EnablePolishMode = true,
            PolishModelName = "polish-model"
        };

        var llm = new QueueLlmClient(
            "มังกรตัวแรกอยู่ที่นี่",
            "มังกรตนแรกปรากฏกาย ณ ที่แห่งนี้",
            "ย่อหน้าสองตรงนี้",
            "ส่วนที่สองสละสลวยยิ่งขึ้น");
        var pipeline = new TranslationPipeline(_repository, llm, settings);

        await pipeline.TranslateChapterAsync(story, chapter, new Progress<TranslationProgressInfo>());

        Assert.Equal("มังกรตนแรกปรากฏกาย ณ ที่แห่งนี้\n\nส่วนที่สองสละสลวยยิ่งขึ้น", chapter.TranslatedText);
        Assert.Equal(ChapterStatus.Translated, chapter.Status);
        Assert.Equal(4, llm.CallCount);
        Assert.Equal(new string?[] { null, "polish-model", null, "polish-model" }, llm.RecordedModelOverrides);

        // Pass 2 ของส่วนที่ 1 ต้องมีทั้งต้นฉบับ ร่างคำแปลแรก และ Glossary
        string chunk1PolishPrompt = llm.RecordedMessages[1][1].Content;
        Assert.Contains("First Dragon here.", chunk1PolishPrompt);
        Assert.Contains("มังกรตัวแรกอยู่ที่นี่", chunk1PolishPrompt);
        Assert.Contains("Dragon => มังกร", chunk1PolishPrompt);

        // ส่วนที่ 2 ต้องได้รับบริบทที่เกลาแล้วจากส่วนที่ 1
        string chunk2TranslatePrompt = llm.RecordedMessages[2][1].Content;
        Assert.Contains("มังกรตนแรกปรากฏกาย ณ ที่แห่งนี้", chunk2TranslatePrompt);
    }

    [Fact]
    public async Task TranslateChapterAsync_WhenPolishPassFailsValidation_FallsBackToFirstPassTranslation()
    {
        var story = new Story { Title = "Polish Fallback Story" };
        story.Id = await _repository.InsertStoryAsync(story);

        var chapter = new Chapter
        {
            StoryId = story.Id,
            Ordinal = 1,
            Title = "Ch 1",
            OriginalText = "The hero walked into the quiet forest at dawn."
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        var settings = new AppSettings
        {
            MaxChunkWords = 50,
            EnablePolishMode = true
        };

        // Pass 1 valid, Pass 2 returns empty (fails validation)
        var llm = new QueueLlmClient("ผู้กล้าเดินเข้าสู่ป่าอันเงียบสงบยามรุ่งสาง", "");
        var pipeline = new TranslationPipeline(_repository, llm, settings);

        await pipeline.TranslateChapterAsync(story, chapter, new Progress<TranslationProgressInfo>());

        Assert.Equal("ผู้กล้าเดินเข้าสู่ป่าอันเงียบสงบยามรุ่งสาง", chapter.TranslatedText);
        Assert.Equal(2, llm.CallCount);
    }

    [Fact]
    public async Task RetranslateParagraphAsync_WhenPolishModeEnabled_ReturnsPolishedParagraph()
    {
        var story = new Story { Title = "Polish Paragraph Story" };
        story.Id = await _repository.InsertStoryAsync(story);

        var settings = new AppSettings
        {
            EnablePolishMode = true,
            PolishModelName = "editor-llm"
        };

        var llm = new QueueLlmClient("เขาเปิดประตูอย่างช้าๆ", "เขาค่อยๆ แง้มบานประตูออก");
        var pipeline = new TranslationPipeline(_repository, llm, settings);

        string result = await pipeline.RetranslateParagraphAsync(story, "He opened the door slowly.", null);

        Assert.Equal("เขาค่อยๆ แง้มบานประตูออก", result);
        Assert.Equal(2, llm.CallCount);
        Assert.Equal(new string?[] { null, "editor-llm" }, llm.RecordedModelOverrides);
    }

    [Fact]
    public void LlmClient_BuildRequestPayload_IncludesOptionalFieldsOnlyWhenAdjusted()
    {
        var defaultClient = new LlmClient(new AppSettings());
        var defaultPayload = defaultClient.BuildRequestPayload("qwen2.5:7b", new[] { new ChatMessage("user", "hi") }, 0.3f);

        Assert.False(defaultPayload.ContainsKey("top_p"));
        Assert.False(defaultPayload.ContainsKey("max_tokens"));
        Assert.False(defaultPayload.ContainsKey("frequency_penalty"));

        var customClient = new LlmClient(new AppSettings
        {
            TopP = 0.85f,
            MaxTokens = 1500,
            FrequencyPenalty = 0.25f
        });
        var customPayload = customClient.BuildRequestPayload("qwen2.5:7b", new[] { new ChatMessage("user", "hi") }, 0.4f);

        Assert.True(customPayload.ContainsKey("top_p"));
        Assert.Equal(1500, customPayload["max_tokens"]);
        Assert.True(customPayload.ContainsKey("frequency_penalty"));
    }

    [Fact]
    public async Task TranslateChapterAsync_RespectsMaxRetryAttemptsAndContextToggles()
    {
        var story = new Story { Title = "Toggle Story" };
        story.Id = await _repository.InsertStoryAsync(story);

        var prevChapter = new Chapter
        {
            StoryId = story.Id,
            Ordinal = 1,
            Title = "Ch 1",
            OriginalText = "Previous chapter source paragraph.",
            TranslatedText = "ย่อหน้าก่อนหน้าจากตอนที่หนึ่ง",
            Status = ChapterStatus.Translated
        };
        await _repository.InsertChapterAsync(prevChapter);

        var currentChapter = new Chapter
        {
            StoryId = story.Id,
            Ordinal = 2,
            Title = "Ch 2",
            OriginalText = "First chunk paragraph.\n\nSecond chunk paragraph."
        };
        currentChapter.Id = await _repository.InsertChapterAsync(currentChapter);

        // MaxRetryAttempts = 0 -> tries once even on empty response; Context toggles disabled -> no prev chapter or look-ahead
        var noRetrySettings = new AppSettings
        {
            MaxChunkWords = 3,
            MaxRetryAttempts = 0,
            EnableCrossChapterContext = false,
            EnableLookAheadContext = false,
            PovMode = PovDetectionMode.FirstPerson
        };
        var llm = new QueueLlmClient("", "ส่วนที่สอง");
        var pipeline = new TranslationPipeline(_repository, llm, noRetrySettings);

        await pipeline.TranslateChapterAsync(story, currentChapter, new Progress<TranslationProgressInfo>());

        Assert.Equal(2, llm.CallCount);
        string firstUserMsg = llm.RecordedMessages[0][1].Content;
        Assert.DoesNotContain("ย่อหน้าก่อนหน้าจากตอนที่หนึ่ง", firstUserMsg);
        Assert.DoesNotContain("บริบทย่อหน้าถัดไป", firstUserMsg);
        Assert.Contains("บุรุษที่ 1", firstUserMsg);

        // MaxRetryAttempts = 2 -> up to 3 total attempts on failing validation
        currentChapter.OriginalText = "Single chunk text.";
        currentChapter.TranslatedText = string.Empty;
        await _repository.UpdateChapterAsync(currentChapter);

        var twoRetrySettings = new AppSettings { MaxChunkWords = 50, MaxRetryAttempts = 2 };
        var retryLlm = new QueueLlmClient("", "", "คำแปลรอบที่สาม");
        var retryPipeline = new TranslationPipeline(_repository, retryLlm, twoRetrySettings);

        await retryPipeline.TranslateChapterAsync(story, currentChapter, new Progress<TranslationProgressInfo>(), forceRetranslate: true);

        Assert.Equal(3, retryLlm.CallCount);
        Assert.Equal("คำแปลรอบที่สาม", currentChapter.TranslatedText);
    }

    [Fact]
    public async Task TranslateChapterAsync_UsesCustomPolishSystemPromptIndependently()
    {
        var story = new Story { Title = "Custom Polish Prompt Story", SourceLang = "English", TargetLang = "Thai" };
        story.Id = await _repository.InsertStoryAsync(story);

        var chapter = new Chapter
        {
            StoryId = story.Id,
            Ordinal = 1,
            Title = "Ch 1",
            OriginalText = "He stood quietly by the river."
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        var settings = new AppSettings
        {
            EnablePolishMode = true,
            CustomSystemPrompt = "MAIN_TEMPLATE {SourceLang}->{TargetLang}",
            CustomPolishSystemPrompt = "POLISH_TEMPLATE {SourceLang}->{TargetLang}"
        };
        var llm = new QueueLlmClient("เขายืนเงียบๆ ริมแม่น้ำ", "เขายืนสงบนิ่งอยู่ริมสายน้ำ");
        var pipeline = new TranslationPipeline(_repository, llm, settings);

        await pipeline.TranslateChapterAsync(story, chapter, new Progress<TranslationProgressInfo>());

        Assert.Contains("MAIN_TEMPLATE English->Thai", llm.RecordedMessages[0][0].Content);
        Assert.Contains("POLISH_TEMPLATE English->Thai", llm.RecordedMessages[1][0].Content);
    }

    [Fact]
    public void LlmClient_BuildEndpointUrl_TrimsWhitespaceAndRejectsEmptyBaseUrl()
    {
        var clientWithSpaces = new LlmClient(new AppSettings { BaseUrl = "  http://127.0.0.1:1234/v1/  " });
        Assert.Equal("http://127.0.0.1:1234/v1/models", clientWithSpaces.BuildEndpointUrl("/models"));
        Assert.Equal("http://127.0.0.1:1234/v1/chat/completions", clientWithSpaces.BuildEndpointUrl("/chat/completions"));

        var clientNoV1 = new LlmClient(new AppSettings { BaseUrl = " http://127.0.0.1:1234/ " });
        Assert.Equal("http://127.0.0.1:1234/v1/models", clientNoV1.BuildEndpointUrl("/models"));

        var emptyClient = new LlmClient(new AppSettings { BaseUrl = "   " });
        Assert.Throws<InvalidOperationException>(() => emptyClient.BuildEndpointUrl("/models"));
    }

    [Fact]
    public async Task TranslateChapterAsync_PureAppendToEditedChapter_PreservesManualEdits()
    {
        var story = new Story { Title = "Pure Append Story" };
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
        var firstPipeline = new TranslationPipeline(_repository, new QueueLlmClient("ย่อหน้าหนึ่ง", "ย่อหน้าสองเดิม"), settings);
        await firstPipeline.TranslateChapterAsync(story, chapter, new Progress<TranslationProgressInfo>());

        // User manually polishes chapter.TranslatedText (chunks table still holds raw LLM output)
        chapter.TranslatedText = "ย่อหน้าหนึ่ง (ขัดเกลา)\n\nย่อหน้าสอง (ขัดเกลา)";
        chapter.Status = ChapterStatus.Edited;
        await _repository.UpdateChapterAsync(chapter);

        // User appends a 3rd paragraph and clicks Translate without forceRetranslate
        chapter.OriginalText = "First paragraph here.\n\nSecond paragraph here.\n\nThird paragraph appended.";
        var appendPipeline = new TranslationPipeline(_repository, new QueueLlmClient("ย่อหน้าสามใหม่"), settings);
        await appendPipeline.TranslateChapterAsync(story, chapter, new Progress<TranslationProgressInfo>());

        Assert.Equal("ย่อหน้าหนึ่ง (ขัดเกลา)\n\nย่อหน้าสอง (ขัดเกลา)\n\nย่อหน้าสามใหม่", chapter.TranslatedText);
    }

    [Fact]
    public async Task RetranslateParagraphAsync_IncludesSurroundingSourceContextEvenWithoutTranslation()
    {
        var story = new Story { Title = "Paragraph Context Story" };
        story.Id = await _repository.InsertStoryAsync(story);

        var llm = new QueueLlmClient("“ไปกันเถอะ” เธอพูด");
        var pipeline = new TranslationPipeline(_repository, llm, new AppSettings());

        await pipeline.RetranslateParagraphAsync(
            story,
            "\"Let's go,\" she said.",
            surroundingSourceContext: "Elena raised her lantern.");

        string userPrompt = llm.RecordedMessages[0][1].Content;
        Assert.Contains("บริบทย่อหน้าก่อนหน้า", userPrompt);
        Assert.Contains("Elena raised her lantern.", userPrompt);
    }

    [Fact]
    public async Task TranslateChapterAsync_WhenAlreadyCompleted_ReturnsFalseAndPreservesEditedStatus()
    {
        var story = new Story { Title = "Completed Edited Story" };
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
        var firstLlm = new QueueLlmClient("สวัสดีชาวโลก");
        var firstPipeline = new TranslationPipeline(_repository, firstLlm, settings);
        Assert.True(await firstPipeline.TranslateChapterAsync(story, chapter, new Progress<TranslationProgressInfo>()));

        chapter.TranslatedText = "สวัสดีชาวโลก (ขัดเกลาแล้ว)";
        chapter.Status = ChapterStatus.Edited;
        await _repository.UpdateChapterAsync(chapter);

        var noopLlm = new QueueLlmClient("ไม่ควรถูกเรียก");
        var noopPipeline = new TranslationPipeline(_repository, noopLlm, settings);
        bool anyTranslated = await noopPipeline.TranslateChapterAsync(story, chapter, new Progress<TranslationProgressInfo>());

        Assert.False(anyTranslated);
        Assert.Equal(0, noopLlm.CallCount);
        Assert.Equal(ChapterStatus.Edited, chapter.Status);
        Assert.Equal("สวัสดีชาวโลก (ขัดเกลาแล้ว)", chapter.TranslatedText);
    }

    private sealed class QueueLlmClient : ILlmClient
    {
        private readonly Queue<string> _responses;
        public int CallCount { get; private set; }
        public List<string?> RecordedModelOverrides { get; } = new();
        public List<List<ChatMessage>> RecordedMessages { get; } = new();

        public QueueLlmClient(params string[] responses)
            => _responses = new Queue<string>(responses);

        public IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            float temperature,
            CancellationToken cancellationToken = default)
            => StreamChatAsync(messages, temperature, null, cancellationToken);

        public async IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            float temperature,
            string? modelOverride,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CallCount++;
            RecordedModelOverrides.Add(modelOverride);
            RecordedMessages.Add(new List<ChatMessage>(messages));
            await Task.CompletedTask;
            yield return _responses.Count > 0 ? _responses.Dequeue() : string.Empty;
        }

        public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ModelInfo>());

        public Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((true, "OK"));
    }
}
