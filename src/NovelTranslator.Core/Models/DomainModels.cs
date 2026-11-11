using System;

namespace NovelTranslator.Core.Models;

public enum ChapterStatus
{
    NotTranslated, // ยังไม่แปล
    Translating,   // กำลังแปล
    Translated,    // แปลแล้ว
    Edited         // แก้ไขแล้ว
}

public class Story
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SourceLang { get; set; } = "English";
    public string TargetLang { get; set; } = "Thai";
    public string StyleNotes { get; set; } = "น้ำเสียงเป็นกันเอง ใช้ ฉัน/เธอ กับตัวเอก";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Chapter
{
    public long Id { get; set; }
    public long StoryId { get; set; }
    public int Ordinal { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public ChapterStatus Status { get; set; } = ChapterStatus.NotTranslated;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ChapterChunk
{
    public long Id { get; set; }
    public long ChapterId { get; set; }
    public int ChunkIndex { get; set; }
    public string SourceText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public bool HasWarning { get; set; } = false;
    public string WarningMessage { get; set; } = string.Empty;
}

public enum GlossaryCategory
{
    Person,   // ชื่อบุคคล / ตัวละคร
    Location, // สถานที่
    Term      // ศัพท์เฉพาะ / สิ่งของ / ท่าไม้ตาย
}

public class GlossaryEntry
{
    public long Id { get; set; }
    public long StoryId { get; set; }
    public string SourceTerm { get; set; } = string.Empty;
    public string TargetTerm { get; set; } = string.Empty;
    public GlossaryCategory Category { get; set; } = GlossaryCategory.Term;
    public string Notes { get; set; } = string.Empty;
}
