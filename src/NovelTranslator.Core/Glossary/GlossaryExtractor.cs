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

namespace NovelTranslator.Core.Glossary;

public class ExtractedTerm
{
    public string SourceTerm { get; set; } = string.Empty;
    public string TargetTerm { get; set; } = string.Empty;
    public GlossaryCategory Category { get; set; } = GlossaryCategory.Term;
    public string Notes { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = true;
}

public class GlossaryExtractor
{
    private readonly ILlmClient _llmClient;
    private readonly AppSettings _settings;

    public GlossaryExtractor(ILlmClient llmClient, AppSettings settings)
    {
        _llmClient = llmClient;
        _settings = settings;
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

        // คัดตัดเฉพาะส่วนแรก 1500 คำ เพื่อความเร็วและประหยัด Token
        string truncatedSource = TruncateWords(sourceText, 1200);
        string truncatedTrans = TruncateWords(translatedText, 1200);

        var existingSet = new HashSet<string>(
            existingGlossary.Select(g => g.SourceTerm.Trim().ToLowerInvariant())
        );

        string prompt = $@"คุณคือผู้เชี่ยวชาญการทำพจนานุกรมศัพท์นิยาย (Glossary)
จากเนื้อหานิยายต้นฉบับและคำแปลด้านล่างนี้ ให้สกัดคำศัพท์เฉพาะที่เป็น:
1. ชื่อตัวละคร (Person)
2. ชื่อสถานที่ (Location)
3. สกิล, ไอเทม, ตำแหน่ง, องค์กร หรือศัพท์เทคนิคเฉพาะเรื่อง (Term)

โดยตอบกลับเป็น JSON Array เท่านั้น ไม่มีข้อความอื่นปน ในรูปแบบ:
[
  {{
    ""source"": ""คำภาษาอังกฤษ"",
    ""target"": ""คำแปลภาษาไทยที่ปรากฏในเนื้อเรื่อง"",
    ""category"": ""Person"" | ""Location"" | ""Term"",
    ""notes"": ""คำอธิบายสั้นๆ เช่น ตัวเอก, เมืองหลวง, วิชาดาบ""
  }}
]

[ต้นฉบับ]
{truncatedSource}

[คำแปล]
{truncatedTrans}";

        var messages = new List<ChatMessage>
        {
            new ChatMessage { Role = "system", Content = "ตอบกลับเป็น JSON Array ของคำศัพท์เท่านั้น ห้ามมีบทเกริ่นหรือ Markdown ปิดกั้นอื่นใด" },
            new ChatMessage { Role = "user", Content = prompt }
        };

        var sb = new System.Text.StringBuilder();
        await foreach (var delta in _llmClient.StreamChatAsync(messages, 0.1f, cancellationToken))
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

        // ดึงเฉพาะส่วนที่เป็น JSON Array [...]
        var match = Regex.Match(jsonText, @"\[.*\]", RegexOptions.Singleline);
        if (!match.Success) return results;

        try
        {
            using var doc = JsonDocument.Parse(match.Value);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return results;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                string source = element.TryGetProperty("source", out var s) ? s.GetString() ?? "" : "";
                string target = element.TryGetProperty("target", out var t) ? t.GetString() ?? "" : "";
                string catStr = element.TryGetProperty("category", out var c) ? c.GetString() ?? "" : "";
                string notes = element.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "";

                source = source.Trim();
                target = target.Trim();

                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) continue;

                // กรองคำที่มีใน Glossary อยู่แล้วออก
                if (existingLowerTerms.Contains(source.ToLowerInvariant())) continue;

                var category = catStr.Equals("Person", StringComparison.OrdinalIgnoreCase) ? GlossaryCategory.Person
                             : catStr.Equals("Location", StringComparison.OrdinalIgnoreCase) ? GlossaryCategory.Location
                             : GlossaryCategory.Term;

                results.Add(new ExtractedTerm
                {
                    SourceTerm = source,
                    TargetTerm = target,
                    Category = category,
                    Notes = notes.Trim(),
                    IsSelected = true
                });
            }
        }
        catch
        {
            // ละเว้นกรณี parse ไม่ผ่าน
        }

        return results;
    }

    private static string TruncateWords(string text, int maxWords)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords) return text;
        return string.Join(" ", words.Take(maxWords));
    }
}
