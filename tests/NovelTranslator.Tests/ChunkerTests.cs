using System.Collections.Generic;
using NovelTranslator.Core.Translation;
using Xunit;

namespace NovelTranslator.Tests;

public class ChunkerTests
{
    [Fact]
    public void SplitIntoChunks_EmptyText_ReturnsEmptyList()
    {
        var result = Chunker.SplitIntoChunks("");
        Assert.Empty(result);
    }

    [Fact]
    public void SplitIntoChunks_ShortParagraphs_CombinesUpToLimit()
    {
        string text = "Paragraph one with several words.\n\nParagraph two with some more words.\n\nParagraph three.";
        var chunks = Chunker.SplitIntoChunks(text, maxWords: 50);

        Assert.Single(chunks);
        Assert.Contains("Paragraph one", chunks[0]);
        Assert.Contains("Paragraph three", chunks[0]);
    }

    [Fact]
    public void SplitIntoChunks_ExceedingLimit_SplitsIntoMultipleChunks()
    {
        // สร้างย่อหน้าละประมาณ 10 คำ
        string p1 = "One two three four five six seven eight nine ten.";
        string p2 = "Eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty.";
        string fullText = $"{p1}\n\n{p2}";

        // ตั้งขีดจำกัดไว้ที่ 12 คำ เพื่อให้ถูกแบ่งเป็น 2 chunks
        var chunks = Chunker.SplitIntoChunks(fullText, maxWords: 12);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(p1, chunks[0]);
        Assert.Equal(p2, chunks[1]);
    }

    [Fact]
    public void SplitIntoChunks_DialogueInLongParagraph_DoesNotSplitInsideQuotes()
    {
        string dialogueSentence = "\"Don't go there!\" he warned loudly.";
        string nextSentence = "She turned back and nodded.";
        string text = $"{dialogueSentence} {nextSentence}";

        var chunks = Chunker.SplitIntoChunks(text, maxWords: 5);

        // ต้องแยกเป็นประโยค แต่ไม่ควรตัดกลางคำพูดที่มีเครื่องหมายคำพูด
        Assert.True(chunks.Count >= 2);
        Assert.Contains("\"Don't go there!\"", chunks[0]);
    }

    [Fact]
    public void BuildSystemPrompt_IncludesCustomPromptAndStyleNotes()
    {
        var story = new NovelTranslator.Core.Models.Story
        {
            Title = "Test",
            StyleNotes = "ใช้ภาษายุคโบราณ"
        };
        string prompt = PromptBuilder.BuildSystemPrompt(story, "ห้ามทับศัพท์");
        Assert.Contains("ใช้ภาษายุคโบราณ", prompt);
        Assert.Contains("คำสั่งพิเศษจากผู้ใช้: ห้ามทับศัพท์", prompt);
    }

    [Fact]
    public void SplitIntoChunks_ZeroOrNegativeMaxWords_FallsBackSafelyWithoutExplodingSentences()
    {
        string text = "First sentence. Second sentence. Third sentence.";
        var chunks = Chunker.SplitIntoChunks(text, maxWords: 0);
        Assert.Single(chunks);
        Assert.Equal(text, chunks[0]);
    }
}
