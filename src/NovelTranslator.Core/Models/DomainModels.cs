using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NovelTranslator.Core.Models;

public enum ChapterStatus
{
    NotTranslated, // ยังไม่แปล
    Translating,   // กำลังแปล
    Translated,    // แปลแล้ว
    Edited         // แก้ไขแล้ว
}

public class Story : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _styleNotes = "น้ำเสียงเป็นกันเอง ใช้ ฉัน/เธอ กับตัวเอก";

    public long Id { get; set; }

    public string Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnPropertyChanged(); } }
    }

    public string SourceLang { get; set; } = "English";
    public string TargetLang { get; set; } = "Thai";

    public string StyleNotes
    {
        get => _styleNotes;
        set { if (_styleNotes != value) { _styleNotes = value; OnPropertyChanged(); } }
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class Chapter : INotifyPropertyChanged
{
    private int _ordinal;
    private string _title = string.Empty;
    private ChapterStatus _status = ChapterStatus.NotTranslated;

    public long Id { get; set; }
    public long StoryId { get; set; }

    public int Ordinal
    {
        get => _ordinal;
        set { if (_ordinal != value) { _ordinal = value; OnPropertyChanged(); } }
    }

    public string Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnPropertyChanged(); } }
    }

    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;

    public ChapterStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(StatusBadgeColor));
            }
        }
    }

    public string StatusBadgeText => Status switch
    {
        ChapterStatus.NotTranslated => "⚪ ยังไม่แปล",
        ChapterStatus.Translating => "⏳ กำลังแปล",
        ChapterStatus.Translated => "🟢 แปลแล้ว",
        ChapterStatus.Edited => "✏️ แก้ไขแล้ว",
        _ => ""
    };

    public string StatusBadgeColor => Status switch
    {
        ChapterStatus.NotTranslated => "#94A3B8",
        ChapterStatus.Translating => "#E67E22",
        ChapterStatus.Translated => "#16A085",
        ChapterStatus.Edited => "#2980B9",
        _ => "#94A3B8"
    };

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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

    public string CategoryDisplay => Category switch
    {
        GlossaryCategory.Person => "👤 ชื่อคน",
        GlossaryCategory.Location => "📍 สถานที่",
        _ => "📘 ศัพท์ทั่วไป"
    };
}
