using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Models;
using NovelTranslator.Core.Settings;
using NovelTranslator.Core.Translation;

namespace NovelTranslator.Core.Glossary;

public class ExtractedTerm
{
    public string SourceTerm { get; set; } = string.Empty;
    public string TargetTerm { get; set; } = string.Empty;
    public GlossaryCategory Category { get; set; } = GlossaryCategory.Term;
    public string Notes { get; set; } = string.Empty;
}

public class GlossaryExtractor
{
    public const string DefaultGlossaryPromptTemplate = @"คุณคือผู้เชี่ยวชาญการจัดทำพจนานุกรมชื่อเฉพาะในนิยาย (Glossary)
จากเนื้อหานิยายต้นฉบับและคำแปลด้านล่างนี้ ให้สกัดเฉพาะ ""ชื่อเฉพาะ (Proper Nouns)"" ที่สำคัญ ได้แก่:
1. ชื่อตัวละคร หรือฉายาเฉพาะ (Person)
2. ชื่อสถานที่ เมือง ดันเจี้ยน อาณาจักร (Location)
3. ชื่อสกิล วิชา ไอเทมสำคัญ หรือองค์กรเฉพาะเรื่อง (Term)

กฎเหล็ก:
- ห้ามดึงคำนามทั่วไป คำกริยา หรือคำสามัญ (เช่น guards, sword, room, father)
- ห้ามดึงทั้งประโยค ให้ดึงเฉพาะตัวชื่อสั้นๆ เท่านั้น
- ตอบกลับเป็น JSON Array เท่านั้น ห้ามมีข้อความอื่นปน ในรูปแบบ:
[
  {
    ""source"": ""ชื่อเฉพาะในภาษาต้นฉบับ"",
    ""target"": ""คำแปลภาษาไทยที่ใช้ในเรื่อง"",
    ""category"": ""Person"" | ""Location"" | ""Term"",
    ""notes"": ""คำอธิบายสั้นๆ เช่น ตัวเอก, เมืองหลวง, สกิลดาบ""
  }
]

[ต้นฉบับ]
{SourceText}

[คำแปล]
{TranslatedText}";

    private readonly ILlmClient _llmClient;
    private readonly AppSettings _settings;

    public GlossaryExtractor(ILlmClient llmClient, AppSettings settings)
    {
        _llmClient = llmClient;
        _settings = settings;
    }

    public static string BuildExtractionPrompt(string? customPrompt, string sourceText, string translatedText)
    {
        string template = string.IsNullOrWhiteSpace(customPrompt)
            ? DefaultGlossaryPromptTemplate
            : customPrompt.Trim();

        bool hasSource = template.Contains("{SourceText}", StringComparison.OrdinalIgnoreCase);
        bool hasTrans = template.Contains("{TranslatedText}", StringComparison.OrdinalIgnoreCase);

        string prompt = template
            .Replace("{SourceText}", sourceText, StringComparison.OrdinalIgnoreCase)
            .Replace("{TranslatedText}", translatedText, StringComparison.OrdinalIgnoreCase);

        if (!hasSource || !hasTrans)
        {
            prompt = $"{prompt.TrimEnd()}\n\n[ต้นฉบับ]\n{sourceText}\n\n[คำแปล]\n{translatedText}";
        }

        return prompt;
    }

    /// <summary>
    /// สแกนหาชื่อเฉพาะและคำศัพท์ใหม่จากต้นฉบับและคำแปล แล้วคัดกรองคำที่มีใน Glossary อยู่แล้วออก
    /// </summary>
    public async Task<List<ExtractedTerm>> ExtractNewTermsAsync(
        string sourceText,
        string translatedText,
        IEnumerable<GlossaryEntry> existingGlossary,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(translatedText))
        {
            return new();
        }

        string truncatedSource = _settings.GlossaryScanMaxWords > 0
            ? TruncateWords(sourceText, _settings.GlossaryScanMaxWords)
            : sourceText;
        string truncatedTrans = _settings.GlossaryScanMaxWords > 0
            ? TruncateWords(translatedText, _settings.GlossaryScanMaxWords)
            : translatedText;

        var existingSet = new HashSet<string>(
            existingGlossary.Select(g => g.SourceTerm.Trim().ToLowerInvariant())
        );

        string prompt = BuildExtractionPrompt(_settings.CustomGlossaryPrompt, truncatedSource, truncatedTrans);

        var messages = new List<ChatMessage>
        {
            new ChatMessage { Role = "system", Content = "ตอบกลับเป็น JSON Array ของคำศัพท์เท่านั้น ห้ามมีบทเกริ่นหรือ Markdown ปิดกั้นอื่นใด" },
            new ChatMessage { Role = "user", Content = prompt }
        };

        string? modelOverride = string.IsNullOrWhiteSpace(_settings.GlossaryModelName)
            ? null
            : _settings.GlossaryModelName.Trim();

        var sb = new System.Text.StringBuilder();
        await foreach (var delta in _llmClient.StreamChatAsync(messages, 0.1f, modelOverride, cancellationToken))
        {
            sb.Append(delta);
        }

        string rawResponse = sb.ToString().Trim();
        return ParseAndFilterResults(rawResponse, existingSet);
    }

    public static List<ExtractedTerm> ParseAndFilterResults(string jsonText, HashSet<string> existingLowerTerms)
    {
        var results = new List<ExtractedTerm>();
        if (string.IsNullOrWhiteSpace(jsonText)) return results;

        // ตัด <think>...</think> ออกก่อนเผื่อ reasoning trace มีวงเล็บ [...]
        string cleanedText = OutputValidator.CleanPreamble(jsonText, stripThinkTags: true);

        // ดึงเฉพาะส่วนที่เป็น JSON Array [...] แรกที่วงเล็บสมดุล (ป้องกันข้อความหมายเหตุ [...] ต่อท้าย)
        string? jsonArray = ExtractFirstJsonArray(cleanedText);
        if (jsonArray == null) return results;

        try
        {
            using var doc = JsonDocument.Parse(jsonArray);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return results;

            var seen = new HashSet<string>(existingLowerTerms, StringComparer.OrdinalIgnoreCase);

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                string source = element.TryGetProperty("source", out var s) ? s.GetString() ?? "" : "";
                string target = element.TryGetProperty("target", out var t) ? t.GetString() ?? "" : "";
                string catStr = element.TryGetProperty("category", out var c) ? c.GetString() ?? "" : "";
                string notes = element.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "";

                source = source.Trim();
                target = target.Trim();

                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) continue;
                if (source.Length > 60 || target.Length > 80) continue;
                if (source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length > 5) continue;
                if (source.EndsWith('.') || source.EndsWith('!') || source.EndsWith('?')) continue;

                // กรองคำที่มีใน Glossary อยู่แล้วหรือซ้ำภายในชุดเดียวกันออก
                if (!seen.Add(source.ToLowerInvariant())) continue;

                var category = catStr.Equals("Person", StringComparison.OrdinalIgnoreCase) ? GlossaryCategory.Person
                             : catStr.Equals("Location", StringComparison.OrdinalIgnoreCase) ? GlossaryCategory.Location
                             : GlossaryCategory.Term;

                results.Add(new ExtractedTerm
                {
                    SourceTerm = source,
                    TargetTerm = target,
                    Category = category,
                    Notes = notes.Trim()
                });
            }
        }
        catch
        {
            // ละเว้นกรณี parse ไม่ผ่าน
        }

        return results;
    }

    private static string? ExtractFirstJsonArray(string text)
    {
        int start = text.IndexOf('[');
        if (start < 0) return null;

        int depth = 0;
        bool inQuotes = false;
        bool escape = false;

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (escape)
            {
                escape = false;
                continue;
            }
            if (inQuotes)
            {
                if (c == '\\') escape = true;
                else if (c == '"') inQuotes = false;
                continue;
            }

            if (c == '"') inQuotes = true;
            else if (c == '[') depth++;
            else if (c == ']' && --depth == 0)
            {
                return text.Substring(start, i - start + 1);
            }
        }

        return null;
    }

    private static string TruncateWords(string text, int maxWords)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords) return text;
        return string.Join(" ", words.Take(maxWords));
    }
}
