using System.Collections.Generic;
using NovelTranslator.Core.Models;
using NovelTranslator.Core.Translation;
using Xunit;

namespace NovelTranslator.Tests;

public class OutputValidatorTests
{
    [Fact]
    public void CleanPreamble_StripsPreambleLinesAndCodeBlocks()
    {
        string raw = "```markdown\nนี่คือคำแปล:\n\nท้องฟ้ายามค่ำคืนเต็มไปด้วยดวงดาวระยิบระยับ\n```";
        string cleaned = OutputValidator.CleanPreamble(raw);

        Assert.Equal("ท้องฟ้ายามค่ำคืนเต็มไปด้วยดวงดาวระยิบระยับ", cleaned);
    }

    [Fact]
    public void CleanPreamble_StripsCodeBlocksPrecededByIntroLine()
    {
        string raw = "Here is the translation:\n```markdown\nท้องฟ้ายามค่ำคืนเต็มไปด้วยดวงดาวระยิบระยับ\n```";
        string cleaned = OutputValidator.CleanPreamble(raw);

        Assert.Equal("ท้องฟ้ายามค่ำคืนเต็มไปด้วยดวงดาวระยิบระยับ", cleaned);
    }

    [Fact]
    public void Validate_EmptyTranslation_ReturnsInvalid()
    {
        var result = OutputValidator.Validate("Some English text that needs translation", "   ");
        Assert.False(result.IsValid);
        Assert.NotNull(result.RetryHint);
    }

    [Fact]
    public void Validate_TooMuchEnglishLeftover_ReturnsInvalid()
    {
        string source = "The knight walked into the dark forest and searched for the hidden cave where the monster lives.";
        string rawTranslation = "The knight เดินเข้าไปใน dark forest เพื่อค้นหา cave ลึกลับที่ monster อาศัยอยู่";

        var result = OutputValidator.Validate(source, rawTranslation);
        // มีภาษาอังกฤษค้างมากเกินไป
        Assert.False(result.IsValid);
        Assert.Contains("มีข้อความภาษาอังกฤษค้างอยู่มากเกินไป", result.RetryHint);
    }

    [Fact]
    public void Validate_GlossaryKeptTerm_DoesNotCountAsEnglishLeftover()
    {
        string source = "The scientist injected DNA samples into the cell and tested the result.";
        // ให้คำว่า DNA คงรูปภาษาอังกฤษตาม Glossary
        var glossary = new List<GlossaryEntry>
        {
            new() { SourceTerm = "DNA", TargetTerm = "DNA" }
        };
        string rawTranslation = "นักวิทยาศาสตร์ได้ฉีดตัวอย่าง DNA เข้าไปในเซลล์และทำการทดสอบผลลัพธ์ที่ได้ออกมาอย่างละเอียด";

        var result = OutputValidator.Validate(source, rawTranslation, glossary);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ValidThaiTranslation_ReturnsValid()
    {
        string source = "The morning breeze was cold.\n\nHe wrapped his cloak tighter.";
        string translation = "สายลมยามเช้าพัดมาอย่างเหน็บหนาว\n\nเขาดึงเสื้อคลุมของตนให้กระชับแน่นขึ้น";

        var result = OutputValidator.Validate(source, translation);
        Assert.True(result.IsValid);
        Assert.False(result.HasParagraphMismatchWarning);
    }

    [Fact]
    public void ChatMessage_SerializesWithLowercaseRoleAndContent()
    {
        var message = new NovelTranslator.Core.Llm.ChatMessage("user", "Hello world");
        string json = System.Text.Json.JsonSerializer.Serialize(message);
        Assert.Contains("\"role\":\"user\"", json);
        Assert.Contains("\"content\":\"Hello world\"", json);
        Assert.DoesNotContain("\"Role\":", json);
        Assert.DoesNotContain("\"Content\":", json);
    }

    [Fact]
    public void CleanPreamble_StripsThinkTagsAndCustomPrefixes()
    {
        string raw = "<think>Analyze [1] and [2]</think>\nหมายเหตุผู้แปล: ส่วนนี้แปลยาก\n\nเขาก้าวเดินไปข้างหน้าอย่างมั่นคง";

        string cleaned = OutputValidator.CleanPreamble(raw, stripThinkTags: true, customStripPrefixes: "หมายเหตุผู้แปล:, Note:");
        Assert.Equal("เขาก้าวเดินไปข้างหน้าอย่างมั่นคง", cleaned);

        string keptThink = OutputValidator.CleanPreamble(raw, stripThinkTags: false);
        Assert.Contains("<think>", keptThink);

        string unclosedThink = "internal reasoning\n</think>\nเนื้อหาที่แปลแล้ว";
        Assert.Equal("เนื้อหาที่แปลแล้ว", OutputValidator.CleanPreamble(unclosedThink, stripThinkTags: true));

        // Unclosed opening <think> while streaming in WordByWord mode or truncated output
        Assert.Equal(string.Empty, OutputValidator.CleanPreamble("<think>thinking during stream...", stripThinkTags: true));
        Assert.Equal("เนื้อหาแรก", OutputValidator.CleanPreamble("เนื้อหาแรก\n<think>truncated", stripThinkTags: true));
    }

    [Fact]
    public void Validate_RespectsMaxEnglishRatioPercentAndDisablesAt100()
    {
        string source = "The knight opened the status window and checked his HP, MP, STR, AGI, and Quest Log.";
        string litRpgTranslation = "อัศวินเปิดหน้าต่าง Status Window และตรวจสอบค่า HP, MP, STR, AGI, และ Quest Log ของตนอย่างละเอียด";

        var strictResult = OutputValidator.Validate(source, litRpgTranslation, maxEnglishRatioPercent: 25);
        Assert.False(strictResult.IsValid);

        var relaxedResult = OutputValidator.Validate(source, litRpgTranslation, maxEnglishRatioPercent: 100);
        Assert.True(relaxedResult.IsValid);
    }
}
