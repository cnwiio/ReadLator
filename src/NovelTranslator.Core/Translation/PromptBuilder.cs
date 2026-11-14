using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Models;

namespace NovelTranslator.Core.Translation;

public class PromptBuilder
{
    public static string BuildSystemPrompt(Story story, string? customSystemPrompt = null)
    {
        string style = string.IsNullOrWhiteSpace(story.StyleNotes)
            ? ""
            : $"\nคำแนะนำสไตล์และโทนเรื่อง: {story.StyleNotes}";

        string custom = string.IsNullOrWhiteSpace(customSystemPrompt)
            ? ""
            : $"\nคำสั่งพิเศษจากผู้ใช้: {customSystemPrompt.Trim()}";

        return $@"คุณคือนักแปลนิยายมืออาชีพ แปลจาก {story.SourceLang} เป็น {story.TargetLang}

หลักการแปลนิยายคุณภาพสูง:
1. แปลให้ครบถ้วนทุกประโยค ไม่ตัดทอน ไม่สรุป และไม่แต่งเนื้อหาเพิ่มเอง
2. รักษาโครงสร้างย่อหน้าและบทสนทนาให้ตรงกับต้นฉบับ
3. ภาษาไทยต้องลื่นไหลเป็นธรรมชาติแบบวรรณกรรม ไม่แปลตรงตัวทื่อๆ (Anti-Translationese):
   - หลีกเลี่ยง Passive Voice แข็งๆ เช่น 'ถูกทำให้...' หรือ 'ได้รับการ...' ให้ปรับเป็นประโยคที่คนไทยพูดจริง
   - ละประธานที่ซ้ำซ้อนในย่อหน้าเดียวกัน (ไม่จำเป็นต้องใส่ 'เขา' ทุกประโยคเหมือนภาษาอังกฤษ)
   - ปรับสำนวนบทสนทนาให้มีชีวิตชีวาตามบุคลิกและอารมณ์ของตัวละคร
4. รักษามุมมองการเล่า (Point of View - POV) ให้เคร่งครัดและต่อเนื่องตลอดทั้งเรื่อง:
   - หากเนื้อเรื่องเล่าแบบบุรุษที่ 1 (ตัวเอกเล่าเหตุการณ์ผ่านสายตา/ความคิดตัวเอง เช่น I walked...) ให้ใช้สรรพนามและน้ำเสียงของตัวเอกเสมอ ห้ามหลุดไปเล่าแบบบุคคลที่สาม
   - หากเล่าแบบบุรุษที่ 3 (ผู้บรรยายบุคคลภายนอก เช่น He/She walked...) ให้คุมระยะห่างและสรรพนามให้ถูกต้อง
5. ใช้คำศัพท์ตาม glossary อย่างเคร่งครัด
6. ตอบเฉพาะคำแปลเท่านั้น ห้ามใส่คำเกริ่น สรุป หรือคำอธิบายใดๆ ทั้งสิ้น{style}{custom}".Trim();
    }

    public static string BuildPolishSystemPrompt(Story story, string? customSystemPrompt = null)
    {
        string style = string.IsNullOrWhiteSpace(story.StyleNotes)
            ? ""
            : $"\nคำแนะนำสไตล์และโทนเรื่อง: {story.StyleNotes}";

        string custom = string.IsNullOrWhiteSpace(customSystemPrompt)
            ? ""
            : $"\nคำสั่งพิเศษจากผู้ใช้: {customSystemPrompt.Trim()}";

        return $@"คุณคือบรรณาธิการและนักเกลาสำนวนนิยายมืออาชีพ (ภาษาเป้าหมาย: {story.TargetLang})
หน้าที่ของคุณคืออ่านทวนคำแปลร่างแรกเทียบกับต้นฉบับ ({story.SourceLang}) แล้วเกลาสำนวนให้สละสลวย ลื่นไหลเป็นธรรมชาติยิ่งขึ้น

หลักการเกลาคำคุณภาพสูง:
1. ปรับภาษาให้เป็นธรรมชาติแบบวรรณกรรม ขจัดกลิ่นอายคำแปลตรงตัว (Anti-Translationese) ลดคำซ้ำ ประธานฟุ่มเฟือย และโครงสร้างประโยคแข็งๆ
2. รักษาความหมายและรายละเอียดจากต้นฉบับให้ครบถ้วนทุกประโยค ห้ามตัดทอน สรุป หรือแต่งเนื้อหาเพิ่มเอง
3. รักษาโครงสร้างย่อหน้า บทสนทนา จังหวะการเล่า และมุมมองการเล่า (POV) ให้ตรงกับต้นฉบับและต่อเนื่องกับบริบทก่อนหน้า
4. คงคำศัพท์เฉพาะ ชื่อตัวละคร และสถานที่ตาม Glossary อย่างเคร่งครัด ห้ามเปลี่ยนคำที่กำหนดไว้ใน Glossary
5. ตอบเฉพาะเนื้อหาฉบับเกลาคำแล้วเท่านั้น ห้ามใส่คำเกริ่น หมายเหตุ คำอธิบาย หรือเปรียบเทียบก่อน-หลังใดๆ ทั้งสิ้น{style}{custom}".Trim();
    }

    /// <summary>
    /// สแกนมุมมองการเล่าเรื่อง (POV) จากเนื้อหานอกเครื่องหมายคำพูด (Narration only)
    /// </summary>
    public static string DetectPovHint(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // ลบข้อความในเครื่องหมายคำพูดออกเพื่อไม่ให้สรรพนามในบทสนทนาหลอกตัวตรวจจับ
        string narrationOnly = Regex.Replace(text, @"[""“][^""”]*[""”]", " ", RegexOptions.Singleline);

        // นับสรรพนามบุรุษที่ 1 ในบทบรรยาย
        var firstPersonMatches = Regex.Matches(narrationOnly, @"\b(I|me|my|mine|myself|I'm|I've|I'll|I'd)\b", RegexOptions.IgnoreCase);
        // นับสรรพนามบุรุษที่ 3 ในบทบรรยาย
        var thirdPersonMatches = Regex.Matches(narrationOnly, @"\b(he|him|his|she|her|hers|they|them|their)\b", RegexOptions.IgnoreCase);

        int firstCount = firstPersonMatches.Count;
        int thirdCount = thirdPersonMatches.Count;

        if (firstCount >= 3 && firstCount * 1.5 >= thirdCount)
        {
            return "มุมมองการเล่าของส่วนนี้: บุรุษที่ 1 (เล่าผ่านมุมมอง/สายตา/ความคิดของตัวเอกโดยตรง ใช้สรรพนามแทนตัวเอกให้เหมาะสมต่อเนื่อง)";
        }
        else if (thirdCount >= 3)
        {
            return "มุมมองการเล่าของส่วนนี้: บุรุษที่ 3 (ผู้บรรยายเล่าเหตุการณ์บุคคลที่สาม คุมสรรพนามและชื่อตัวละครให้สม่ำเสมอ)";
        }

        return string.Empty;
    }

    public static List<ChatMessage> BuildMessages(
        Story story,
        string chunkText,
        IEnumerable<GlossaryEntry> matchedGlossary,
        string? previousSourceContext = null,
        string? previousTranslationContext = null,
        string? retryHint = null,
        string? customSystemPrompt = null,
        string? nextSourceContext = null)
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage { Role = "system", Content = BuildSystemPrompt(story, customSystemPrompt) }
        };

        var userSb = new StringBuilder();
        AppendGlossaryAndPov(userSb, chunkText, matchedGlossary);

        // 3. บริบทย่อหน้าก่อนหน้า (ถ้ามี)
        if (!string.IsNullOrWhiteSpace(previousSourceContext) && !string.IsNullOrWhiteSpace(previousTranslationContext))
        {
            userSb.AppendLine("### บริบทย่อหน้าก่อนหน้า (ห้ามแปลซ้ำ ใช้อ้างอิงความต่อเนื่องของสำนวนและสรรพนามเท่านั้น):");
            userSb.AppendLine("[ต้นฉบับก่อนหน้า]");
            userSb.AppendLine(previousSourceContext.Trim());
            userSb.AppendLine("[คำแปลก่อนหน้า]");
            userSb.AppendLine(previousTranslationContext.Trim());
            userSb.AppendLine();
        }

        // 4. บริบทย่อหน้าถัดไป (Look-Ahead Context ถ้ามี)
        if (!string.IsNullOrWhiteSpace(nextSourceContext))
        {
            userSb.AppendLine("### บริบทย่อหน้าถัดไป (ห้ามแปล ใช้อ้างอิงผู้พูดและเหตุการณ์ที่กำลังจะเกิดขึ้นเพื่อเลือกคำแปลให้เข้ากัน):");
            userSb.AppendLine(nextSourceContext.Trim());
            userSb.AppendLine();
        }

        // 5. ข้อความเตือนแก้ไขถ้าเป็นการลองแปลใหม่ (Retry Hint)
        if (!string.IsNullOrWhiteSpace(retryHint))
        {
            userSb.AppendLine("### ข้อผิดพลาดจากการแปลรอบก่อนที่ต้องแก้ไข:");
            userSb.AppendLine(retryHint.Trim());
            userSb.AppendLine();
        }

        // 6. เนื้อหาที่ต้องแปล
        userSb.AppendLine("### ข้อความที่ต้องแปล:");
        userSb.AppendLine(chunkText.Trim());

        messages.Add(new ChatMessage { Role = "user", Content = userSb.ToString() });
        return messages;
    }

    public static List<ChatMessage> BuildPolishMessages(
        Story story,
        string chunkText,
        string draftTranslation,
        IEnumerable<GlossaryEntry> matchedGlossary,
        string? previousTranslationContext = null,
        string? customSystemPrompt = null)
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage { Role = "system", Content = BuildPolishSystemPrompt(story, customSystemPrompt) }
        };

        var userSb = new StringBuilder();
        AppendGlossaryAndPov(userSb, chunkText, matchedGlossary);

        if (!string.IsNullOrWhiteSpace(previousTranslationContext))
        {
            userSb.AppendLine("### บริบทคำแปลก่อนหน้า (ห้ามแปลซ้ำ ใช้อ้างอิงความต่อเนื่องของสำนวนเท่านั้น):");
            userSb.AppendLine(previousTranslationContext.Trim());
            userSb.AppendLine();
        }

        userSb.AppendLine("### ข้อความต้นฉบับ (ใช้อ้างอิงความถูกต้องครบถ้วน):");
        userSb.AppendLine(chunkText.Trim());
        userSb.AppendLine();

        userSb.AppendLine("### คำแปลร่างแรกที่ต้องเกลาสำนวนให้ดีขึ้น:");
        userSb.AppendLine(draftTranslation.Trim());

        messages.Add(new ChatMessage { Role = "user", Content = userSb.ToString() });
        return messages;
    }

    private static void AppendGlossaryAndPov(
        StringBuilder userSb,
        string chunkText,
        IEnumerable<GlossaryEntry> matchedGlossary)
    {
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

        // 2. คำแนะนำมุมมองการเล่าเรื่อง (POV Hint)
        string povHint = DetectPovHint(chunkText);
        if (!string.IsNullOrWhiteSpace(povHint))
        {
            userSb.AppendLine("### คำแนะนำมุมมองการเล่าเรื่อง (POV):");
            userSb.AppendLine($"- {povHint}");
            userSb.AppendLine();
        }
    }
}
