using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NovelTranslator.Core.Models;

namespace NovelTranslator.Core.Translation;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string CleanedText { get; set; } = string.Empty;
    public string? RetryHint { get; set; }
    public bool HasParagraphMismatchWarning { get; set; }
}

public class OutputValidator
{
    // คำเกริ่นหรือเครื่องหมายที่ AI มักชอบใส่ติดมา
    private static readonly string[] PreamblePrefixes = new[]
    {
        "นี่คือคำแปล",
        "นี่คือผลการแปล",
        "คำแปล:",
        "คำแปลมีดังนี้",
        "Here is the translation",
        "Translation:",
        "Sure, here is",
        "Here's the translation",
        "บทแปล:",
        "แน่นอน นี่คือคำแปล",
        "นี่คือข้อความที่เกลาแล้ว",
        "ข้อความที่เกลาแล้ว:",
        "ฉบับเกลาคำ:",
        "คำแปลที่เกลาแล้ว:",
        "Here is the polished"
    };

    /// <summary>
    /// ตัดคำเกริ่น แท็กคิดวิเคราะห์ (<think>) และ Markdown โค้ดบล็อกที่ AI แอบใส่มา
    /// </summary>
    public static string CleanPreamble(string rawText, bool stripThinkTags = true, string? customStripPrefixes = null)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        string text = rawText.Trim();

        if (stripThinkTags)
        {
            text = Regex.Replace(text, @"<think>[\s\S]*?</think>", string.Empty, RegexOptions.IgnoreCase).Trim();
            int closeThinkIdx = text.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase);
            if (closeThinkIdx >= 0)
            {
                text = text.Substring(closeThinkIdx + "</think>".Length).Trim();
            }
            int openThinkIdx = text.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            if (openThinkIdx >= 0)
            {
                text = text.Substring(0, openThinkIdx).Trim();
            }
        }

        string[] customPrefixes = string.IsNullOrWhiteSpace(customStripPrefixes)
            ? Array.Empty<string>()
            : customStripPrefixes.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

        while (true)
        {
            string before = text;

            // 1. ตัดครอบโค้ดบล็อก เช่น ```markdown ... ``` หรือ ``` ... ```
            if (text.StartsWith("```"))
            {
                int firstNewline = text.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    text = text.Substring(firstNewline + 1).Trim();
                }
                if (text.EndsWith("```"))
                {
                    text = text.Substring(0, text.Length - 3).Trim();
                }
            }

            // 2. ตัดบรรทัดคำเกริ่น
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            while (lines.Count > 0)
            {
                string firstLine = lines[0].Trim();
                bool isPreamble = PreamblePrefixes.Any(p => firstLine.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    || customPrefixes.Any(p => firstLine.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                if (isPreamble)
                {
                    lines.RemoveAt(0);
                    // ตัดบรรทัดว่างถัดไป
                    while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
                    {
                        lines.RemoveAt(0);
                    }
                }
                else
                {
                    break;
                }
            }

            text = string.Join("\n", lines).Trim();
            if (text == before) break;
        }

        return text;
    }

    /// <summary>
    /// ตรวจสอบคุณภาพคำแปล
    /// </summary>
    public static ValidationResult Validate(
        string sourceChunk,
        string rawTranslation,
        IReadOnlyList<GlossaryEntry>? activeGlossary = null,
        bool stripThinkTags = true,
        int maxEnglishRatioPercent = 25,
        string? customStripPrefixes = null)
    {
        string cleaned = CleanPreamble(rawTranslation, stripThinkTags, customStripPrefixes);

        // 1. ตรวจว่าว่างเปล่าหรือไม่
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return new ValidationResult
            {
                IsValid = false,
                CleanedText = string.Empty,
                RetryHint = "ผลการแปลว่างเปล่า กรุณาแปลเนื้อหาทั้งหมดและตอบเฉพาะคำแปลเท่านั้น"
            };
        }

        // 2. ตรวจสอบความยาวเทียบกับต้นฉบับ
        int sourceLen = sourceChunk.Trim().Length;
        int translatedLen = cleaned.Length;

        if (sourceLen > 50)
        {
            // ถ้าคำแปลสั้นกว่า 20% หรือยาวกว่า 400% ของต้นฉบับ
            if (translatedLen < sourceLen * 0.2)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    CleanedText = cleaned,
                    RetryHint = "คำแปลสั้นผิดปกติมากเมื่อเทียบกับต้นฉบับ (อาจมีการตัดทอนเนื้อหา) กรุณาแปลให้ครบทุกประโยค"
                };
            }
            if (translatedLen > sourceLen * 4.5)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    CleanedText = cleaned,
                    RetryHint = "คำแปลยาวผิดปกติมาก กรุณาไม่เติมแต่งบทวิเคราะห์หรือคำอธิบายเพิ่ม"
                };
            }
        }

        // 3. ตรวจสอบภาษาอังกฤษที่หลงเหลือ (English ratio)
        // ยกเว้นคำศัพท์ที่อยู่ใน Glossary และตั้งค่า TargetTerm ให้คงรูปเดิมไว้
        string textForRatio = cleaned;
        if (activeGlossary != null)
        {
            foreach (var g in activeGlossary)
            {
                if (!string.IsNullOrWhiteSpace(g.TargetTerm) && g.TargetTerm.Any(char.IsAsciiLetter))
                {
                    textForRatio = textForRatio.Replace(g.TargetTerm, "");
                }
            }
        }

        int latinCount = textForRatio.Count(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
        int nonWhitespaceCount = textForRatio.Count(c => !char.IsWhiteSpace(c));

        if (nonWhitespaceCount > 40)
        {
            double latinRatio = (double)latinCount / nonWhitespaceCount;
            int effectiveMaxEnglish = Math.Clamp(maxEnglishRatioPercent, 5, 100);
            // ถ้ายังมีตัวอักษรละตินค้างเกินเกณฑ์ที่กำหนด (100% = ปิดการตรวจ) แสดงว่าลืมแปลเป็นภาษาไทย
            if (effectiveMaxEnglish < 100 && latinRatio > (effectiveMaxEnglish / 100.0))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    CleanedText = cleaned,
                    RetryHint = $"มีข้อความภาษาอังกฤษค้างอยู่มากเกินไป ({latinRatio * 100:F0}%) กรุณาแปลข้อความทั้งหมดเป็นภาษาไทย"
                };
            }
        }

        // 4. ตรวจสอบจำนวนย่อหน้าเทียบกับต้นฉบับ
        int sourceParagraphs = CountParagraphs(sourceChunk);
        int translatedParagraphs = CountParagraphs(cleaned);

        bool paragraphMismatch = sourceParagraphs > 1 && Math.Abs(sourceParagraphs - translatedParagraphs) >= 2;

        return new ValidationResult
        {
            IsValid = true,
            CleanedText = cleaned,
            HasParagraphMismatchWarning = paragraphMismatch,
            RetryHint = paragraphMismatch
                ? $"จำนวนย่อหน้า ({translatedParagraphs} ย่อหน้า) ไม่ตรงกับต้นฉบับ ({sourceParagraphs} ย่อหน้า)"
                : null
        };
    }

    private static int CountParagraphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        return normalized.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
