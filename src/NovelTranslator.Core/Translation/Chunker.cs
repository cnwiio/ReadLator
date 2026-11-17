using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NovelTranslator.Core.Translation;

public class Chunker
{
    private static readonly char[] WordSeparators = new[] { ' ', '\t', '\r', '\n' };

    public static int EstimateWordCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// แบ่งข้อความเป็นชิ้นๆ (Chunks) ตามย่อหน้าโดยไม่ตัดกลางประโยคหรือบทสนทนา
    /// หากมีย่อหน้าเดียวยาวเกิน maxWords จะตัดย่อยตามจบประโยค
    /// </summary>
    public static List<string> SplitIntoChunks(string text, int maxWords = 600)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        if (maxWords <= 0) maxWords = 600;

        // แยกข้อความเป็นย่อหน้า (รองรับทั้ง \r\n\r\n และ \n\n รวมถึงบรรทัดเดี่ยว)
        string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] rawParagraphs = normalized.Split(new[] { "\n\n" }, StringSplitOptions.None);

        var currentChunkParagraphs = new List<string>();
        int currentWordCount = 0;

        foreach (var p in rawParagraphs)
        {
            string trimmedP = p.Trim();
            if (string.IsNullOrEmpty(trimmedP)) continue;

            int pWords = EstimateWordCount(trimmedP);

            // ถ้าย่อหน้าเดียวยาวเกิน maxWords ให้แบ่งย่อยย่อหน้านั้นตามประโยค
            if (pWords > maxWords)
            {
                // ถ้ามีข้อความใน chunk ปัจจุบันค้างอยู่ ให้ปิด chunk ก่อน
                if (currentChunkParagraphs.Count > 0)
                {
                    result.Add(string.Join("\n\n", currentChunkParagraphs));
                    currentChunkParagraphs.Clear();
                    currentWordCount = 0;
                }

                var sentencePieces = SplitLongParagraphBySentences(trimmedP, maxWords);
                foreach (var piece in sentencePieces)
                {
                    result.Add(piece);
                }
                continue;
            }

            // ถ้าเพิ่มย่อหน้านี้แล้วเกินขีดจำกัด และใน chunk มีเนื้อหาอยู่แล้ว ให้ตัดเป็น chunk ใหม่
            if (currentWordCount + pWords > maxWords && currentChunkParagraphs.Count > 0)
            {
                result.Add(string.Join("\n\n", currentChunkParagraphs));
                currentChunkParagraphs.Clear();
                currentWordCount = 0;
            }

            currentChunkParagraphs.Add(trimmedP);
            currentWordCount += pWords;
        }

        if (currentChunkParagraphs.Count > 0)
        {
            result.Add(string.Join("\n\n", currentChunkParagraphs));
        }

        return result;
    }

    /// <summary>
    /// แบ่งย่อหน้าที่ยาวมากออกเป็นชิ้นย่อยโดยแบ่งตามจุดสิ้นสุดประโยค (. ! ?) และไม่ตัดกลางบทสนทนา ("...")
    /// </summary>
    private static List<string> SplitLongParagraphBySentences(string paragraph, int maxWords)
    {
        var pieces = new List<string>();
        var sentences = ExtractSentencesSafe(paragraph);

        var currentPiece = new StringBuilder();
        int currentWords = 0;

        foreach (var s in sentences)
        {
            int sWords = EstimateWordCount(s);
            if (currentWords + sWords > maxWords && currentPiece.Length > 0)
            {
                pieces.Add(currentPiece.ToString().Trim());
                currentPiece.Clear();
                currentWords = 0;
            }

            if (currentPiece.Length > 0) currentPiece.Append(" ");
            currentPiece.Append(s);
            currentWords += sWords;
        }

        if (currentPiece.Length > 0)
        {
            pieces.Add(currentPiece.ToString().Trim());
        }

        return pieces;
    }

    /// <summary>
    /// แยกประโยคโดยระวังไม่ตัดกลางเครื่องหมายคำพูด ("..." หรือ “...”)
    /// </summary>
    private static List<string> ExtractSentencesSafe(string text)
    {
        var sentences = new List<string>();
        var sb = new StringBuilder();
        bool inDoubleQuotes = false;
        bool inCurlyQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            sb.Append(c);

            if (c == '"')
            {
                inDoubleQuotes = !inDoubleQuotes;
            }
            else if (c == '“' || c == '”')
            {
                inCurlyQuotes = (c == '“');
            }

            // จุดจบประโยคคือ . ! ? ที่ไม่ได้อยู่ในเครื่องหมายคำพูด และตัวถัดไปคือช่องว่างหรือจบท้ายข้อความ
            if (!inDoubleQuotes && !inCurlyQuotes && (c == '.' || c == '!' || c == '?'))
            {
                // ตรวจดูตัวถัดไปว่าเป็นช่องว่างหรือจบท้ายข้อความหรือไม่
                if (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1]))
                {
                    sentences.Add(sb.ToString().Trim());
                    sb.Clear();
                }
            }
        }

        if (sb.Length > 0)
        {
            string remaining = sb.ToString().Trim();
            if (!string.IsNullOrEmpty(remaining))
            {
                sentences.Add(remaining);
            }
        }

        return sentences;
    }
}
