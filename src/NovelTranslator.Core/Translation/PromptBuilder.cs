using System.Collections.Generic;
using System.Linq;
using System.Text;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Models;

namespace NovelTranslator.Core.Translation;

public class PromptBuilder
{
    public static string BuildSystemPrompt(Story story)
    {
        string style = string.IsNullOrWhiteSpace(story.StyleNotes)
            ? ""
            : $"คำแนะนำสไตล์เพิ่มเติม: {story.StyleNotes}";

        return $@"คุณคือนักแปลนิยายมืออาชีพ แปลจาก {story.SourceLang} เป็น {story.TargetLang}
กฎ:
1. แปลให้ครบทุกประโยค ไม่ตัด ไม่สรุป ไม่เพิ่มเนื้อหา
2. รักษาโครงสร้างย่อหน้าและบทสนทนาให้ตรงกับต้นฉบับ
3. ใช้สำนวนภาษาไทยที่ลื่นไหลแบบนิยาย ไม่แปลตรงตัวจนแข็ง
4. ใช้คำศัพท์ตาม glossary อย่างเคร่งครัด
5. ตอบเฉพาะคำแปลเท่านั้น ห้ามมีคำเกริ่นหรือคำอธิบายใดๆ ทั้งสิ้น
{style}".Trim();
    }

    public static List<ChatMessage> BuildMessages(
        Story story,
        string chunkText,
        IEnumerable<GlossaryEntry> matchedGlossary,
        string? previousSourceContext = null,
        string? previousTranslationContext = null,
        string? retryHint = null)
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage { Role = "system", Content = BuildSystemPrompt(story) }
        };

        var userSb = new StringBuilder();

        // 1. Glossary ที่เกี่ยวข้อง
        var glossaryList = matchedGlossary.ToList();
        if (glossaryList.Count > 0)
        {
            userSb.AppendLine("### Glossary (บังคับใช้ตามนี้):");
            foreach (var g in glossaryList)
            {
                string note = string.IsNullOrWhiteSpace(g.Notes) ? "" : $" ({g.Notes})";
                userSb.AppendLine($"- {g.SourceTerm} => {g.TargetTerm}{note}");
            }
            userSb.AppendLine();
        }

        // 2. บริบทย่อหน้าก่อนหน้า (ถ้ามี)
        if (!string.IsNullOrWhiteSpace(previousSourceContext) && !string.IsNullOrWhiteSpace(previousTranslationContext))
        {
            userSb.AppendLine("### บริบทย่อหน้าก่อนหน้า (ห้ามแปลซ้ำ ใช้อ้างอิงความต่อเนื่องเท่านั้น):");
            userSb.AppendLine("[ต้นฉบับก่อนหน้า]");
            userSb.AppendLine(previousSourceContext.Trim());
            userSb.AppendLine("[คำแปลก่อนหน้า]");
            userSb.AppendLine(previousTranslationContext.Trim());
            userSb.AppendLine();
        }

        // 3. ข้อความเตือนแก้ไขถ้าเป็นการลองแปลใหม่ (Retry Hint)
        if (!string.IsNullOrWhiteSpace(retryHint))
        {
            userSb.AppendLine("### ข้อผิดพลาดจากการแปลรอบก่อนที่ต้องแก้ไข:");
            userSb.AppendLine(retryHint.Trim());
            userSb.AppendLine();
        }

        // 4. เนื้อหาที่ต้องแปล
        userSb.AppendLine("### ข้อความที่ต้องแปล:");
        userSb.AppendLine(chunkText.Trim());

        messages.Add(new ChatMessage { Role = "user", Content = userSb.ToString() });
        return messages;
    }
}
