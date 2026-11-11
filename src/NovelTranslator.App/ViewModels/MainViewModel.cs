using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Export;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Models;
using NovelTranslator.Core.Settings;
using NovelTranslator.Core.Translation;

namespace NovelTranslator.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly NovelDatabase _database;
    private readonly NovelRepository _repository;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _translationCts;
    private readonly DispatcherTimer _autoSaveTimer;

    // รายการเรื่องและตอน
    public ObservableCollection<Story> Stories { get; } = new();
    public ObservableCollection<Chapter> Chapters { get; } = new();

    [ObservableProperty]
    private Story? _selectedStory;

    [ObservableProperty]
    private Chapter? _selectedChapter;

    // ช่องข้อความหลัก
    [ObservableProperty]
    private string _originalText = string.Empty;

    [ObservableProperty]
    private string _translatedText = string.Empty;

    // สถิติและสถานะ
    [ObservableProperty]
    private int _originalWordCount;

    [ObservableProperty]
    private int _originalParagraphCount;

    [ObservableProperty]
    private string _chapterStatusText = "ยังไม่แปล";

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private string _saveStatusText = string.Empty;

    // Error Debug Details
    [ObservableProperty]
    private string _lastErrorDetails = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    // ความคืบหน้าการแปล
    [ObservableProperty]
    private double _translationProgressPercentage;

    [ObservableProperty]
    private bool _isTranslatingIndeterminate;

    // Actions สำหรับเปิดหน้าต่างย่อย
    public Func<Story, Task>? RequestOpenGlossary { get; set; }
    public Func<Task>? RequestOpenSettings { get; set; }
    public Func<Story, Chapter?, Task>? RequestOpenSearchReplace { get; set; }
    public Func<string, Task<string?>>? RequestSaveFileDialog { get; set; }
    public Func<string, Task<string[]?>>? RequestOpenFilesDialog { get; set; }
    public Func<string, string, Task<string?>>? RequestPromptDialog { get; set; }
    public Func<string, Task<bool>>? RequestConfirmDialog { get; set; }
    public Func<string, Task>? RequestCopyToClipboard { get; set; }
    public Action<string>? RequestShowErrorDialog { get; set; }

    public MainViewModel()
    {
        _settings = AppSettings.Load();
        _database = new NovelDatabase();
        _database.Initialize();
        _repository = new NovelRepository(_database);

        // ตั้งค่า Auto-save Debounce 1.5 วินาที
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _autoSaveTimer.Tick += async (_, _) =>
        {
            _autoSaveTimer.Stop();
            await SaveCurrentChapterInternalAsync(isAutoSave: true);
        };

        _ = LoadInitialDataAsync();
    }

    public void ReloadSettings()
    {
        var fresh = AppSettings.Load();
        _settings.Backend = fresh.Backend;
        _settings.BaseUrl = fresh.BaseUrl;
        _settings.ApiKey = fresh.ApiKey;
        _settings.ModelName = fresh.ModelName;
        _settings.Temperature = fresh.Temperature;
        _settings.MaxChunkWords = fresh.MaxChunkWords;
        _settings.ContextParagraphCount = fresh.ContextParagraphCount;
        _settings.LastSelectedStoryId = fresh.LastSelectedStoryId;
        _settings.LastSelectedChapterId = fresh.LastSelectedChapterId;
    }

    private async Task LoadInitialDataAsync()
    {
        var stories = await _repository.GetAllStoriesAsync();
        Stories.Clear();
        foreach (var s in stories)
        {
            Stories.Add(s);
        }

        if (Stories.Count == 0)
        {
            // สร้างเรื่องเริ่มต้นให้พร้อมใช้งาน
            var defaultStory = new Story
            {
                Title = "นิยายเรื่องแรกของฉัน",
                SourceLang = "English",
                TargetLang = "Thai",
                StyleNotes = "น้ำเสียงเป็นกันเอง ใช้ ฉัน/เธอ กับตัวเอก"
            };
            defaultStory.Id = await _repository.InsertStoryAsync(defaultStory);
            Stories.Add(defaultStory);
        }

        // กู้คืนเรื่องล่าสุดที่เลือกไว้
        if (_settings.LastSelectedStoryId.HasValue)
        {
            SelectedStory = Stories.FirstOrDefault(s => s.Id == _settings.LastSelectedStoryId.Value)
                            ?? Stories.FirstOrDefault();
        }
        else
        {
            SelectedStory = Stories.FirstOrDefault();
        }
    }

    async partial void OnSelectedStoryChanged(Story? value)
    {
        Chapters.Clear();
        SelectedChapter = null;
        OriginalText = string.Empty;
        TranslatedText = string.Empty;

        if (value != null)
        {
            // บันทึกเรื่องล่าสุด
            _settings.LastSelectedStoryId = value.Id;
            _settings.Save();

            var chapters = await _repository.GetChaptersByStoryAsync(value.Id);
            foreach (var c in chapters)
            {
                Chapters.Add(c);
            }

            if (Chapters.Count == 0)
            {
                // สร้างตอนเริ่มต้น
                var firstChap = new Chapter
                {
                    StoryId = value.Id,
                    Ordinal = 1,
                    Title = "ตอนที่ 1",
                    OriginalText = "Paste your English story here...\n\nThen click the 'แปล' button!",
                    Status = ChapterStatus.NotTranslated
                };
                firstChap.Id = await _repository.InsertChapterAsync(firstChap);
                Chapters.Add(firstChap);
            }

            // กู้คืนตอนล่าสุดของเรื่องนี้ถ้ามี
            if (_settings.LastSelectedChapterId.HasValue)
            {
                SelectedChapter = Chapters.FirstOrDefault(c => c.Id == _settings.LastSelectedChapterId.Value)
                                  ?? Chapters.FirstOrDefault();
            }
            else
            {
                SelectedChapter = Chapters.FirstOrDefault();
            }
        }
    }

    partial void OnSelectedChapterChanged(Chapter? value)
    {
        if (value != null)
        {
            // บันทึกตอนล่าสุด
            _settings.LastSelectedChapterId = value.Id;
            _settings.Save();

            OriginalText = value.OriginalText ?? string.Empty;
            TranslatedText = value.TranslatedText ?? string.Empty;
            UpdateChapterStatusDisplay(value.Status);
            UpdateWordAndParagraphCounts(OriginalText);
            SaveStatusText = $"แก้ไขล่าสุด: {value.UpdatedAt.ToLocalTime():HH:mm:ss}";
        }
        else
        {
            OriginalText = string.Empty;
            TranslatedText = string.Empty;
            ChapterStatusText = "";
            SaveStatusText = "";
        }
    }

    partial void OnOriginalTextChanged(string value)
    {
        UpdateWordAndParagraphCounts(value);
        if (SelectedChapter != null && SelectedChapter.OriginalText != value)
        {
            SelectedChapter.OriginalText = value;
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }
    }

    partial void OnTranslatedTextChanged(string value)
    {
        if (SelectedChapter != null && SelectedChapter.TranslatedText != value && !IsTranslating)
        {
            SelectedChapter.TranslatedText = value;
            SelectedChapter.Status = ChapterStatus.Edited;
            UpdateChapterStatusDisplay(SelectedChapter.Status);
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }
    }

    private void UpdateWordAndParagraphCounts(string text)
    {
        OriginalWordCount = Chunker.EstimateWordCount(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            OriginalParagraphCount = 0;
        }
        else
        {
            OriginalParagraphCount = text.Replace("\r\n", "\n")
                .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }

    private void UpdateChapterStatusDisplay(ChapterStatus status)
    {
        ChapterStatusText = status switch
        {
            ChapterStatus.NotTranslated => "ยังไม่แปล",
            ChapterStatus.Translating => "กำลังแปล...",
            ChapterStatus.Translated => "แปลแล้ว",
            ChapterStatus.Edited => "แก้ไขแล้ว",
            _ => ""
        };
    }

    // ================= คำสั่งควบคุมการแปล =================

    [RelayCommand]
    public async Task StartTranslationAsync()
    {
        if (SelectedStory == null || SelectedChapter == null) return;
        if (string.IsNullOrWhiteSpace(OriginalText)) return;

        IsTranslating = true;
        HasError = false;
        LastErrorDetails = string.Empty;
        TranslationProgressPercentage = 0;
        IsTranslatingIndeterminate = true;
        _translationCts = new CancellationTokenSource();

        var llmClient = new LlmClient(_settings);
        var pipeline = new TranslationPipeline(_repository, llmClient, _settings);

        var progress = new Progress<TranslationProgressInfo>(info =>
        {
            if (!string.IsNullOrEmpty(info.CombinedTranslatedTextSoFar))
            {
                TranslatedText = info.CombinedTranslatedTextSoFar;
            }
            ProgressText = info.StatusMessage ?? string.Empty;
            if (info.TotalChunks > 0)
            {
                IsTranslatingIndeterminate = false;
                TranslationProgressPercentage = (double)info.CurrentChunkIndex / info.TotalChunks * 100;
            }
        });

        try
        {
            ProgressText = "กำลังเริ่มขั้นตอนการแปล...";
            await Task.Run(async () =>
            {
                await pipeline.TranslateChapterAsync(SelectedStory, SelectedChapter, progress, _translationCts.Token);
            });
            TranslatedText = SelectedChapter.TranslatedText;
            UpdateChapterStatusDisplay(SelectedChapter.Status);
            ProgressText = "การแปลเสร็จสิ้นสมบูรณ์!";
            TranslationProgressPercentage = 100;
            SaveStatusText = "บันทึกอัตโนมัติแล้ว";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "หยุดการแปลแล้ว (สามารถกดแปลต่อได้)";
            SelectedChapter.Status = ChapterStatus.Edited;
            UpdateChapterStatusDisplay(SelectedChapter.Status);
        }
        catch (Exception ex)
        {
            HasError = true;
            LastErrorDetails = ex.ToString();
            string firstLine = ex.Message.Split('\n')[0];
            ProgressText = $"เกิดข้อผิดพลาด: {firstLine}";
        }
        finally
        {
            IsTranslating = false;
            IsTranslatingIndeterminate = false;
        }
    }

    [RelayCommand]
    public async Task CopyErrorDetailsAsync()
    {
        if (!string.IsNullOrEmpty(LastErrorDetails) && RequestCopyToClipboard != null)
        {
            await RequestCopyToClipboard(LastErrorDetails);
            SaveStatusText = "คัดลอกรายละเอียด Error ลงคลิปบอร์ดแล้ว!";
        }
    }

    [RelayCommand]
    public void ShowErrorDialog()
    {
        if (!string.IsNullOrEmpty(LastErrorDetails) && RequestShowErrorDialog != null)
        {
            RequestShowErrorDialog(LastErrorDetails);
        }
    }

    [RelayCommand]
    public void StopTranslation()
    {
        _translationCts?.Cancel();
    }

    [RelayCommand]
    public async Task RetranslateParagraphAsync(string selectedParagraph)
    {
        if (SelectedStory == null || SelectedChapter == null || string.IsNullOrWhiteSpace(selectedParagraph)) return;

        IsTranslating = true;
        ProgressText = "กำลังแปลซ้ำเฉพาะย่อหน้าที่เลือก...";

        var llmClient = new LlmClient(_settings);
        var pipeline = new TranslationPipeline(_repository, llmClient, _settings);

        try
        {
            string newTranslation = await Task.Run(async () =>
            {
                return await pipeline.RetranslateParagraphAsync(SelectedStory, selectedParagraph, null);
            });
            ProgressText = "แปลซ้ำย่อหน้านั้นสำเร็จ!";
            // แจ้งเตือนผู้ใช้ให้นำผลแปลไปวาง
            if (RequestPromptDialog != null)
            {
                await RequestPromptDialog("ผลการแปลซ้ำย่อหน้าที่เลือก", newTranslation);
            }
        }
        catch (Exception ex)
        {
            ProgressText = $"แปลซ้ำไม่สำเร็จ: {ex.Message}";
        }
        finally
        {
            IsTranslating = false;
        }
    }

    // ================= จัดการเรื่อง (Story) =================

    [RelayCommand]
    public async Task CreateStoryAsync()
    {
        if (RequestPromptDialog == null) return;
        string? title = await RequestPromptDialog("สร้างเรื่องใหม่", "กรุณาระบุชื่อเรื่อง:");
        if (string.IsNullOrWhiteSpace(title)) return;

        var story = new Story
        {
            Title = title.Trim(),
            SourceLang = "English",
            TargetLang = "Thai",
            StyleNotes = "น้ำเสียงเป็นกันเอง ใช้ ฉัน/เธอ กับตัวเอก"
        };
        story.Id = await _repository.InsertStoryAsync(story);
        Stories.Insert(0, story);
        SelectedStory = story;
    }

    [RelayCommand]
    public async Task DeleteStoryAsync()
    {
        if (SelectedStory == null || RequestConfirmDialog == null) return;
        bool ok = await RequestConfirmDialog($"คุณแน่ใจหรือไม่ว่าต้องการลบเรื่อง \"{SelectedStory.Title}\" รวมทั้งทุกตอนในเรื่องนี้?");
        if (!ok) return;

        await _repository.DeleteStoryAsync(SelectedStory.Id);
        Stories.Remove(SelectedStory);
        SelectedStory = Stories.FirstOrDefault();
    }

    // ================= จัดการตอน (Chapter) =================

    [RelayCommand]
    public async Task CreateChapterAsync()
    {
        if (SelectedStory == null) return;
        int nextOrdinal = Chapters.Count > 0 ? Chapters.Max(c => c.Ordinal) + 1 : 1;

        var chapter = new Chapter
        {
            StoryId = SelectedStory.Id,
            Ordinal = nextOrdinal,
            Title = $"ตอนที่ {nextOrdinal}",
            OriginalText = string.Empty,
            TranslatedText = string.Empty,
            Status = ChapterStatus.NotTranslated
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);
        Chapters.Add(chapter);
        SelectedChapter = chapter;
    }

    [RelayCommand]
    public async Task DeleteChapterAsync()
    {
        if (SelectedChapter == null || RequestConfirmDialog == null) return;
        bool ok = await RequestConfirmDialog($"คุณแน่ใจหรือไม่ว่าต้องการลบตอน \"{SelectedChapter.Title}\"?");
        if (!ok) return;

        await _repository.DeleteChapterAsync(SelectedChapter.Id);
        Chapters.Remove(SelectedChapter);
        SelectedChapter = Chapters.FirstOrDefault();
    }

    [RelayCommand]
    public async Task MoveChapterUpAsync()
    {
        if (SelectedChapter == null) return;
        int index = Chapters.IndexOf(SelectedChapter);
        if (index <= 0) return;

        var prev = Chapters[index - 1];
        int temp = SelectedChapter.Ordinal;
        SelectedChapter.Ordinal = prev.Ordinal;
        prev.Ordinal = temp;

        await _repository.UpdateChapterAsync(SelectedChapter);
        await _repository.UpdateChapterAsync(prev);

        Chapters.Move(index, index - 1);
    }

    [RelayCommand]
    public async Task MoveChapterDownAsync()
    {
        if (SelectedChapter == null) return;
        int index = Chapters.IndexOf(SelectedChapter);
        if (index >= Chapters.Count - 1 || index < 0) return;

        var next = Chapters[index + 1];
        int temp = SelectedChapter.Ordinal;
        SelectedChapter.Ordinal = next.Ordinal;
        next.Ordinal = temp;

        await _repository.UpdateChapterAsync(SelectedChapter);
        await _repository.UpdateChapterAsync(next);

        Chapters.Move(index, index + 1);
    }

    // ================= บันทึกและนำเข้า/ส่งออก =================

    [RelayCommand]
    public async Task SaveCurrentChapterAsync()
    {
        await SaveCurrentChapterInternalAsync(isAutoSave: false);
    }

    private async Task SaveCurrentChapterInternalAsync(bool isAutoSave)
    {
        if (SelectedChapter == null) return;

        SelectedChapter.OriginalText = OriginalText;
        SelectedChapter.TranslatedText = TranslatedText;
        await _repository.UpdateChapterAsync(SelectedChapter);

        SaveStatusText = isAutoSave
            ? $"บันทึกอัตโนมัติ ({DateTime.Now:HH:mm:ss})"
            : $"บันทึกแล้ว ({DateTime.Now:HH:mm:ss})";
    }

    [RelayCommand]
    public async Task ImportTxtFilesAsync()
    {
        if (SelectedStory == null || RequestOpenFilesDialog == null) return;

        var files = await RequestOpenFilesDialog("เลือกไฟล์ .txt ต้นฉบับ (เลือกได้หลายไฟล์)");
        if (files == null || files.Length == 0) return;

        int nextOrdinal = Chapters.Count > 0 ? Chapters.Max(c => c.Ordinal) + 1 : 1;
        foreach (var path in files.OrderBy(f => f))
        {
            var chap = await TextExportImportService.ImportChapterFromFileAsync(path, SelectedStory.Id, nextOrdinal++);
            chap.Id = await _repository.InsertChapterAsync(chap);
            Chapters.Add(chap);
        }

        SaveStatusText = $"นำเข้าสำเร็จ {files.Length} ตอน";
    }

    [RelayCommand]
    public async Task ExportCurrentChapterAsync()
    {
        if (SelectedChapter == null || RequestSaveFileDialog == null) return;
        string? path = await RequestSaveFileDialog($"{SelectedChapter.Title}.txt");
        if (string.IsNullOrEmpty(path)) return;

        await TextExportImportService.ExportChapterAsync(path, SelectedChapter);
        SaveStatusText = "ส่งออกตอนนี้สำเร็จ!";
    }

    [RelayCommand]
    public async Task ExportStoryAsync()
    {
        if (SelectedStory == null || RequestSaveFileDialog == null) return;
        string? path = await RequestSaveFileDialog($"{SelectedStory.Title}.txt");
        if (string.IsNullOrEmpty(path)) return;

        await TextExportImportService.ExportStorySingleFileAsync(path, SelectedStory, Chapters);
        SaveStatusText = "ส่งออกทั้งเรื่องสำเร็จ!";
    }

    // ================= เปิดหน้าต่างเสริม =================

    [RelayCommand]
    public async Task OpenSettingsAsync()
    {
        if (RequestOpenSettings != null)
        {
            await RequestOpenSettings();
        }
    }

    [RelayCommand]
    public async Task OpenGlossaryAsync()
    {
        if (SelectedStory != null && RequestOpenGlossary != null)
        {
            await RequestOpenGlossary(SelectedStory);
        }
    }

    [RelayCommand]
    public async Task OpenSearchReplaceAsync()
    {
        if (SelectedStory != null && RequestOpenSearchReplace != null)
        {
            await RequestOpenSearchReplace(SelectedStory, SelectedChapter);
            // โหลดเนื้อหาใหม่หากมีการแทนที่
            if (SelectedChapter != null)
            {
                var refreshed = await _repository.GetChapterByIdAsync(SelectedChapter.Id);
                if (refreshed != null)
                {
                    TranslatedText = refreshed.TranslatedText;
                }
            }
        }
    }

    [RelayCommand]
    public async Task BackupDatabaseAsync()
    {
        if (RequestSaveFileDialog == null) return;
        string? path = await RequestSaveFileDialog($"novels_backup_{DateTime.Now:yyyyMMdd_HHmm}.db");
        if (string.IsNullOrEmpty(path)) return;

        _database.BackupTo(path);
        SaveStatusText = "สำรองฐานข้อมูลสำเร็จ!";
    }

    [RelayCommand]
    public async Task RestoreDatabaseAsync()
    {
        if (RequestOpenFilesDialog == null || RequestConfirmDialog == null) return;
        var files = await RequestOpenFilesDialog("เลือกไฟล์ .db เพื่อกู้คืนฐานข้อมูล");
        if (files == null || files.Length == 0) return;

        bool ok = await RequestConfirmDialog("การกู้คืนจะเขียนทับข้อมูลปัจจุบันทั้งหมด คุณต้องการดำเนินการต่อหรือไม่?");
        if (!ok) return;

        _database.RestoreFrom(files[0]);
        await LoadInitialDataAsync();
        SaveStatusText = "กู้คืนฐานข้อมูลเรียบร้อยแล้ว!";
    }
}
