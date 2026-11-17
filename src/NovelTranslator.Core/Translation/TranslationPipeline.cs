using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Glossary;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Models;
using NovelTranslator.Core.Settings;

namespace NovelTranslator.Core.Translation;

public class TranslationProgressInfo
{
    public int CurrentChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public string StreamingDelta { get; set; } = string.Empty;
    public string FullCurrentChunkText { get; set; } = string.Empty;
    public string CombinedTranslatedTextSoFar { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }
    public int StreamedWordCount { get; set; }
}

public class TranslationPipeline
{
    private readonly NovelRepository _repository;
    private readonly ILlmClient _llmClient;
    private readonly AppSettings _settings;

    public TranslationPipeline(NovelRepository repository, ILlmClient llmClient, AppSettings settings)
    {
        _repository = repository;
        _llmClient = llmClient;
        _settings = settings;
    }

    /// <summary>
    /// แปลทั้งตอน พร้อมรองรับการ Resume ต่อจากส่วนที่ค้างไว้
    /// </summary>
    public async Task TranslateChapterAsync(
        Story story,
        Chapter chapter,
        IProgress<TranslationProgressInfo> progress,
        bool forceRetranslate = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chapter.OriginalText)) return;

        // ดึง Chunks เดิมจาก DB หรือสร้างใหม่ถ้ายังไม่มีหรือเนื้อหาเปลี่ยน
        var chunks = await _repository.GetChunksByChapterAsync(chapter.Id);
        var splitTexts = Chunker.SplitIntoChunks(chapter.OriginalText, _settings.MaxChunkWords);

        // ตรวจสอบว่า chunks เดิมสอดคล้องกับ splitTexts ปัจจุบันหรือไม่ หรือมีการสั่ง forceRetranslate
        bool needsResplit = forceRetranslate || chunks.Count != splitTexts.Count ||
            !chunks.Select(c => c.SourceText).SequenceEqual(splitTexts);

        if (needsResplit)
        {
            await _repository.SaveChunksAsync(chapter.Id, splitTexts, forceRetranslate);
            chunks = await _repository.GetChunksByChapterAsync(chapter.Id);
        }

        int completedCount = chunks.Count(c => c.IsCompleted && !string.IsNullOrEmpty(c.TranslatedText));
        bool isContiguousPrefix = chunks.Take(completedCount).All(c => c.IsCompleted && !string.IsNullOrEmpty(c.TranslatedText));
        bool preserveExistingTranslation = !forceRetranslate && !needsResplit && completedCount > 0 && isContiguousPrefix && !string.IsNullOrWhiteSpace(chapter.TranslatedText);
        var appendedParts = new List<string>();
        if (preserveExistingTranslation)
        {
            appendedParts.Add(chapter.TranslatedText);
        }
        else
        {
            // ประกอบคำแปลรวมล่าสุดที่มีอยู่แล้ว
            chapter.TranslatedText = string.Join("\n\n", chunks.Select(c => c.TranslatedText).Where(t => !string.IsNullOrEmpty(t)));
        }

        progress.Report(new TranslationProgressInfo
        {
            CurrentChunkIndex = completedCount,
            TotalChunks = chunks.Count,
            CombinedTranslatedTextSoFar = chapter.TranslatedText,
            StatusMessage = completedCount == chunks.Count
                ? "เนื้อหาทั้งหมดแปลเสร็จเรียบร้อยแล้ว"
                : $"กำลังเตรียมแปล {chunks.Count - completedCount} ส่วนที่เหลือ (จากทั้งหมด {chunks.Count} ส่วน)..."
        });

        var allGlossary = await _repository.GetGlossaryByStoryAsync(story.Id);

        // ตั้งสถานะเป็นกำลังแปล
        chapter.Status = ChapterStatus.Translating;
        await _repository.UpdateChapterAsync(chapter);

        string? previousSourceContext = null;
        string? previousTranslationContext = null;

        // เชื่อมโยงบริบทข้ามตอน (Cross-Chapter Continuity): ถ้าชิ้นแรกยังไม่มีบริบท ดึงตอนก่อนหน้าที่แปลแล้วมาต่อทันที
        if (_settings.EnableCrossChapterContext && _settings.ContextParagraphCount > 0)
        {
            var prevChapter = await _repository.GetPreviousTranslatedChapterAsync(story.Id, chapter.Ordinal);
            if (prevChapter != null && !string.IsNullOrWhiteSpace(prevChapter.OriginalText) && !string.IsNullOrWhiteSpace(prevChapter.TranslatedText))
            {
                previousSourceContext = GetLastParagraphs(prevChapter.OriginalText, _settings.ContextParagraphCount);
                previousTranslationContext = GetLastParagraphs(prevChapter.TranslatedText, _settings.ContextParagraphCount);
            }
        }

        for (int i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = chunks[i];

            // ถ้าชิ้นนี้แปลเสร็จแล้ว ให้ใช้เป็น context แล้วข้ามไปยังชิ้นถัดไป (Resume)
            if (chunk.IsCompleted && !string.IsNullOrWhiteSpace(chunk.TranslatedText))
            {
                previousSourceContext = GetLastParagraphs(chunk.SourceText, _settings.ContextParagraphCount);
                previousTranslationContext = GetLastParagraphs(chunk.TranslatedText, _settings.ContextParagraphCount);
                continue;
            }

            // Look-Ahead Context: ดึงย่อหน้าแรกของชิ้นถัดไปเพื่อช่วยคาดเดาผู้พูด/ทิศทางฉาก
            string? nextSourceContext = null;
            if (_settings.EnableLookAheadContext && _settings.ContextParagraphCount > 0 && i + 1 < chunks.Count)
            {
                nextSourceContext = GetFirstParagraphs(chunks[i + 1].SourceText, Math.Min(2, _settings.ContextParagraphCount));
            }

            // ชิ้นนี้ต้องแปล
            var matchedGlossary = GlossaryService.FindMatchingEntries(chunk.SourceText, allGlossary);
            string translatedChunk = string.Empty;
            string? retryHint = null;
            bool validated = false;
            int maxAttempts = 1 + Math.Clamp(_settings.MaxRetryAttempts, 0, 5);
            int attempt = 0;

            while (attempt < maxAttempts && !validated)
            {
                attempt++;
                var messages = PromptBuilder.BuildMessages(
                    story,
                    chunk.SourceText,
                    matchedGlossary,
                    previousSourceContext,
                    previousTranslationContext,
                    retryHint,
                    _settings.CustomSystemPrompt,
                    nextSourceContext,
                    _settings.PovMode);

                progress.Report(new TranslationProgressInfo
                {
                    CurrentChunkIndex = i + 1,
                    TotalChunks = chunks.Count,
                    StatusMessage = $"กำลังแปลส่วนที่ {i + 1}/{chunks.Count} (รอบที่ {attempt})..."
                });

                string rawResult = await StreamChunkWithProgressAsync(
                    messages,
                    modelOverride: null,
                    statusPrefix: $"กำลังแปลส่วนที่ {i + 1}/{chunks.Count}",
                    chunks,
                    chunkIndex: i,
                    chapter,
                    preserveExistingTranslation,
                    appendedParts,
                    progress,
                    cancellationToken);

                var validation = OutputValidator.Validate(
                    chunk.SourceText,
                    rawResult,
                    matchedGlossary,
                    _settings.StripThinkTags,
                    _settings.MaxEnglishRatioPercent,
                    _settings.CustomStripPrefixes);

                if (validation.IsValid)
                {
                    translatedChunk = validation.CleanedText;
                    chunk.HasWarning = validation.HasParagraphMismatchWarning;
                    chunk.WarningMessage = validation.RetryHint ?? string.Empty;
                    validated = true;
                }
                else
                {
                    retryHint = validation.RetryHint;
                    if (attempt >= maxAttempts)
                    {
                        // ถ้าลองครบตามจำนวนครั้งแล้วยังไม่ผ่าน ให้ใช้ข้อความที่ Cleaned แล้ว เพื่อไม่ให้สะดุด
                        translatedChunk = validation.CleanedText;
                        chunk.HasWarning = true;
                        chunk.WarningMessage = $"ตรวจพบข้อผิดพลาดหลังลองแปล {maxAttempts} ครั้ง: {validation.RetryHint}";
                    }
                }
            }

            if (_settings.EnablePolishMode && !string.IsNullOrWhiteSpace(translatedChunk))
            {
                string? polishModel = string.IsNullOrWhiteSpace(_settings.PolishModelName)
                    ? null
                    : _settings.PolishModelName.Trim();

                var polishMessages = PromptBuilder.BuildPolishMessages(
                    story,
                    chunk.SourceText,
                    translatedChunk,
                    matchedGlossary,
                    previousTranslationContext,
                    _settings.CustomPolishSystemPrompt,
                    _settings.PovMode);

                progress.Report(new TranslationProgressInfo
                {
                    CurrentChunkIndex = i + 1,
                    TotalChunks = chunks.Count,
                    StatusMessage = $"กำลังเกลาคำส่วนที่ {i + 1}/{chunks.Count}..."
                });

                string rawPolished = await StreamChunkWithProgressAsync(
                    polishMessages,
                    modelOverride: polishModel,
                    statusPrefix: $"กำลังเกลาคำส่วนที่ {i + 1}/{chunks.Count}",
                    chunks,
                    chunkIndex: i,
                    chapter,
                    preserveExistingTranslation,
                    appendedParts,
                    progress,
                    cancellationToken);

                var polishValidation = OutputValidator.Validate(
                    chunk.SourceText,
                    rawPolished,
                    matchedGlossary,
                    _settings.StripThinkTags,
                    _settings.MaxEnglishRatioPercent,
                    _settings.CustomStripPrefixes);
                if (polishValidation.IsValid)
                {
                    translatedChunk = polishValidation.CleanedText;
                    chunk.HasWarning = polishValidation.HasParagraphMismatchWarning;
                    chunk.WarningMessage = polishValidation.RetryHint ?? string.Empty;
                }
            }

            // บันทึกผลชิ้นนี้ลงฐานข้อมูลทันที
            chunk.TranslatedText = translatedChunk;
            chunk.IsCompleted = true;
            await _repository.UpdateChunkAsync(chunk);

            // ประกอบคำแปลรวมล่าสุดของทั้งตอน (คงคำแปลที่ผู้ใช้แก้เองไว้เมื่อ resume)
            if (preserveExistingTranslation)
            {
                if (!string.IsNullOrEmpty(chunk.TranslatedText))
                {
                    appendedParts.Add(chunk.TranslatedText);
                }
                chapter.TranslatedText = string.Join("\n\n", appendedParts);
            }
            else
            {
                chapter.TranslatedText = string.Join("\n\n", chunks.Select(c => c.TranslatedText).Where(t => !string.IsNullOrEmpty(t)));
            }
            await _repository.UpdateChapterAsync(chapter);

            // เตรียม context สำหรับชิ้นถัดไป
            previousSourceContext = GetLastParagraphs(chunk.SourceText, _settings.ContextParagraphCount);
            previousTranslationContext = GetLastParagraphs(chunk.TranslatedText, _settings.ContextParagraphCount);

            progress.Report(new TranslationProgressInfo
            {
                CurrentChunkIndex = i + 1,
                TotalChunks = chunks.Count,
                StreamingDelta = "",
                FullCurrentChunkText = chunk.TranslatedText,
                CombinedTranslatedTextSoFar = chapter.TranslatedText,
                StatusMessage = $"แปลส่วนที่ {i + 1}/{chunks.Count} เสร็จสิ้น"
            });
        }

        // เมื่อแปลครบทุกชิ้น
        chapter.Status = ChapterStatus.Translated;
        await _repository.UpdateChapterAsync(chapter);
    }

    /// <summary>
    /// แปลซ้ำเฉพาะย่อหน้าที่ผู้ใช้เลือก
    /// </summary>
    public async Task<string> RetranslateParagraphAsync(
        Story story,
        string selectedSourceParagraph,
        string? surroundingSourceContext,
        CancellationToken cancellationToken = default)
    {
        var allGlossary = await _repository.GetGlossaryByStoryAsync(story.Id);
        var matchedGlossary = GlossaryService.FindMatchingEntries(selectedSourceParagraph, allGlossary);

        var messages = PromptBuilder.BuildMessages(
            story,
            selectedSourceParagraph,
            matchedGlossary,
            surroundingSourceContext,
            null,
            null,
            _settings.CustomSystemPrompt,
            null,
            _settings.PovMode);

        var sb = new StringBuilder();
        await foreach (var delta in _llmClient.StreamChatAsync(messages, _settings.Temperature, cancellationToken))
        {
            sb.Append(delta);
        }

        string draft = OutputValidator.CleanPreamble(sb.ToString(), _settings.StripThinkTags, _settings.CustomStripPrefixes);
        if (_settings.EnablePolishMode && !string.IsNullOrWhiteSpace(draft))
        {
            string? polishModel = string.IsNullOrWhiteSpace(_settings.PolishModelName)
                ? null
                : _settings.PolishModelName.Trim();

            var polishMessages = PromptBuilder.BuildPolishMessages(
                story,
                selectedSourceParagraph,
                draft,
                matchedGlossary,
                null,
                _settings.CustomPolishSystemPrompt,
                _settings.PovMode);

            var polishSb = new StringBuilder();
            await foreach (var delta in _llmClient.StreamChatAsync(polishMessages, _settings.Temperature, polishModel, cancellationToken))
            {
                polishSb.Append(delta);
            }

            string polished = OutputValidator.CleanPreamble(polishSb.ToString(), _settings.StripThinkTags, _settings.CustomStripPrefixes);
            if (!string.IsNullOrWhiteSpace(polished))
            {
                return polished;
            }
        }

        return draft;
    }

    private async Task<string> StreamChunkWithProgressAsync(
        List<ChatMessage> messages,
        string? modelOverride,
        string statusPrefix,
        List<ChapterChunk> chunks,
        int chunkIndex,
        Chapter chapter,
        bool preserveExistingTranslation,
        List<string> appendedParts,
        IProgress<TranslationProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        var chunkSb = new StringBuilder();
        long lastReportTick = Environment.TickCount64 - 80;

        await foreach (var delta in _llmClient.StreamChatAsync(messages, _settings.Temperature, modelOverride, cancellationToken))
        {
            chunkSb.Append(delta);
            long now = Environment.TickCount64;
            if (now - lastReportTick >= 80 || delta.Contains('\n'))
            {
                lastReportTick = now;
                string currentText = chunkSb.ToString();
                int wordCount = Chunker.EstimateWordCount(currentText);
                string liveCombined = _settings.TranslationAppearance == TranslationAppearanceMode.WordByWord
                    ? (preserveExistingTranslation
                        ? (currentText.Length == 0
                            ? chapter.TranslatedText
                            : string.Join("\n\n", appendedParts.Concat(new[] { OutputValidator.CleanPreamble(currentText, _settings.StripThinkTags, _settings.CustomStripPrefixes) })))
                        : BuildCombinedTextSoFar(chunks, chunkIndex, currentText, _settings.StripThinkTags, _settings.CustomStripPrefixes))
                    : string.Empty;
                progress.Report(new TranslationProgressInfo
                {
                    CurrentChunkIndex = chunkIndex + 1,
                    TotalChunks = chunks.Count,
                    StreamingDelta = _settings.TranslationAppearance == TranslationAppearanceMode.WordByWord ? delta : string.Empty,
                    FullCurrentChunkText = _settings.TranslationAppearance == TranslationAppearanceMode.WordByWord ? currentText : string.Empty,
                    CombinedTranslatedTextSoFar = liveCombined,
                    StreamedWordCount = wordCount,
                    StatusMessage = $"{statusPrefix} (สตรีมสด {wordCount} คำ)..."
                });
            }
        }

        return chunkSb.ToString();
    }

    private static string GetLastParagraphs(string text, int count)
    {
        if (string.IsNullOrWhiteSpace(text) || count <= 0) return string.Empty;
        var pList = text.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var take = pList.TakeLast(count);
        return string.Join("\n\n", take);
    }

    private static string GetFirstParagraphs(string text, int count)
    {
        if (string.IsNullOrWhiteSpace(text) || count <= 0) return string.Empty;
        var pList = text.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var take = pList.Take(count);
        return string.Join("\n\n", take);
    }

    private static string BuildCombinedTextSoFar(
        List<ChapterChunk> chunks,
        int currentIndex,
        string currentStreamingText,
        bool stripThinkTags,
        string? customStripPrefixes)
    {
        var parts = new List<string>();
        for (int i = 0; i < chunks.Count; i++)
        {
            if (i < currentIndex)
            {
                if (!string.IsNullOrEmpty(chunks[i].TranslatedText))
                    parts.Add(chunks[i].TranslatedText);
            }
            else if (i == currentIndex)
            {
                if (!string.IsNullOrEmpty(currentStreamingText))
                    parts.Add(OutputValidator.CleanPreamble(currentStreamingText, stripThinkTags, customStripPrefixes));
            }
        }
        return string.Join("\n\n", parts);
    }
}
