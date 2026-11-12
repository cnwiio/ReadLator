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
    public AppSettings Settings => _settings;
    private CancellationTokenSource? _translationCts;
    private readonly DispatcherTimer _autoSaveTimer;
    private DispatcherTimer? _fakeProgressTimer;

    // รายการเรื่องและตอน
    public ObservableCollection<Story> Stories { get; } = new();
    public ObservableCollection<Chapter> Chapters { get; } = new();
    public ObservableCollection<Chapter> FilteredChapters { get; } = new();

    [ObservableProperty]
    private Story? _selectedStory;

    [ObservableProperty]
    private Chapter? _selectedChapter;

    [ObservableProperty]
    private string _chapterSearchFilter = string.Empty;

    [ObservableProperty]
    private int _chapterStatusFilterIndex; // 0: All, 1: NotTranslated, 2: Translated, 3: Edited

    [ObservableProperty]
    private string _storyProgressText = "0 / 0 ตอน (0%)";

    [ObservableProperty]
    private double _storyProgressPercentage;

    [ObservableProperty]
    private string _chapterTitleEdit = string.Empty;

    [ObservableProperty]
    private bool _isStoryDetailsExpanded;

    [ObservableProperty]
    private string _storyTitleEdit = string.Empty;

    [ObservableProperty]
    private string _storyStyleNotesEdit = string.Empty;

    // UI State & Theme
    [ObservableProperty]
    private bool _isSidebarVisible = true;

    public string SidebarToggleText => IsSidebarVisible ? "ซ่อนแถบข้าง" : "แสดงแถบข้าง";
    partial void OnIsSidebarVisibleChanged(bool value) => OnPropertyChanged(nameof(SidebarToggleText));

    [ObservableProperty]
    private bool _isFocusMode;

    private bool _sidebarBeforeFocusMode = true;
    public bool IsHeaderVisible => !IsFocusMode;
    public string FocusModeButtonText => IsFocusMode ? "ออกโหมดโฟกัส" : "โหมดโฟกัส";
    partial void OnIsFocusModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsHeaderVisible));
        OnPropertyChanged(nameof(FocusModeButtonText));
    }

    [ObservableProperty]
    private bool _isReaderMode;

    public bool IsOriginalPaneVisible => !IsReaderMode;
    public string ReaderModeButtonText => IsReaderMode ? "มุมมองคู่ขนาน" : "อ่านเต็มจอ";
    partial void OnIsReaderModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsOriginalPaneVisible));
        OnPropertyChanged(nameof(ReaderModeButtonText));
    }

    [ObservableProperty]
    private bool _isSyncScrollEnabled = true;

    [ObservableProperty]
    private int _editorFontSize = 20;

    partial void OnEditorFontSizeChanged(int value)
    {
        if (value > 0 && _settings != null)
        {
            _settings.EditorFontSize = value;
            _settings.Save();
        }
    }

    public ObservableCollection<string> AvailableEditorFonts { get; } = new()
    {
        "Leelawadee UI",
        "Tahoma",
        "Cordia New",
        "Angsana New",
        "TH Sarabun New",
        "Segoe UI",
        "Microsoft Sans Serif",
        "Consolas",
        "Arial"
    };

    [ObservableProperty]
    private string _editorFontFamily = "Tahoma";

    partial void OnEditorFontFamilyChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && _settings != null)
        {
            _settings.EditorFontFamily = value;
            _settings.Save();
        }
    }

    [ObservableProperty]
    private string _themeMode = "Light";

    public string ThemeButtonText => ThemeMode switch
    {
        "Dark" => "โหมดมืด",
        "Sepia" => "ถนอมสายตา",
        _ => "โหมดสว่าง"
    };
    partial void OnThemeModeChanged(string value) => OnPropertyChanged(nameof(ThemeButtonText));

    [ObservableProperty]
    private string _activeModelDisplay = "ยังไม่ได้ตั้งค่าโมเดล";

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
    private int _translatedWordCount;

    [ObservableProperty]
    private string _chapterStatusText = "ยังไม่แปล";

    [ObservableProperty]
    private string _chapterStatusColor = "#737373";

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private string _saveStatusText = string.Empty;

    [ObservableProperty]
    private string _saveStateColor = "#16A34A";

    private bool _isLoadingChapter;

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
    public Action? RequestSelectAllChapters { get; set; }
    public Action? RequestClearChapterSelection { get; set; }
    public Action? RequestToggleFullScreen { get; set; }

    [RelayCommand]
    public void ToggleFullScreen() => RequestToggleFullScreen?.Invoke();

    public MainViewModel()
    {
        _settings = AppSettings.Load();
        _editorFontSize = Math.Clamp(_settings.EditorFontSize > 0 ? _settings.EditorFontSize : 20, 12, 32);
        if (!string.IsNullOrWhiteSpace(_settings.EditorFontFamily))
        {
            _editorFontFamily = _settings.EditorFontFamily;
        }
        _themeMode = string.IsNullOrWhiteSpace(_settings.ThemeMode) ? "Light" : _settings.ThemeMode;
        UpdateActiveModelDisplay();
        _database = new NovelDatabase();
        _database.Initialize();
        _repository = new NovelRepository(_database);

        // ตั้งค่า Auto-save Debounce
        double autoSaveSeconds = _settings.AutoSaveDelaySeconds > 0 ? _settings.AutoSaveDelaySeconds : 1.5;
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(autoSaveSeconds) };
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
        _settings.CustomSystemPrompt = fresh.CustomSystemPrompt;
        _settings.Shortkeys = fresh.Shortkeys;
        _settings.EditorFontFamily = fresh.EditorFontFamily;
        _settings.EditorFontSize = fresh.EditorFontSize;
        _settings.AutoSaveDelaySeconds = fresh.AutoSaveDelaySeconds;
        _settings.LastSelectedStoryId = fresh.LastSelectedStoryId;
        _settings.LastSelectedChapterId = fresh.LastSelectedChapterId;

        if (!string.IsNullOrWhiteSpace(fresh.EditorFontFamily))
        {
            EditorFontFamily = fresh.EditorFontFamily;
        }
        if (fresh.EditorFontSize > 0)
        {
            EditorFontSize = Math.Clamp(fresh.EditorFontSize, 12, 36);
        }
        if (fresh.AutoSaveDelaySeconds > 0)
        {
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(fresh.AutoSaveDelaySeconds);
        }

        UpdateActiveModelDisplay();
    }

    private void UpdateActiveModelDisplay()
    {
        string model = string.IsNullOrWhiteSpace(_settings.ModelName) ? "ยังไม่ระบุโมเดล" : _settings.ModelName;
        ActiveModelDisplay = $"{_settings.Backend} · {model}";
    }

    [RelayCommand]
    public void CycleTheme()
    {
        ThemeMode = ThemeMode switch
        {
            "Light" => "Dark",
            "Dark" => "Sepia",
            _ => "Light"
        };
        _settings.ThemeMode = ThemeMode;
        _settings.Save();
        App.ApplyTheme(ThemeMode);
    }

    [RelayCommand]
    public void ToggleSidebar() => IsSidebarVisible = !IsSidebarVisible;

    [RelayCommand]
    public void ToggleFocusMode()
    {
        if (!IsFocusMode)
        {
            _sidebarBeforeFocusMode = IsSidebarVisible;
            IsFocusMode = true;
            IsSidebarVisible = false;
        }
        else
        {
            IsFocusMode = false;
            IsSidebarVisible = _sidebarBeforeFocusMode;
        }
    }

    [RelayCommand]
    public void ToggleReaderMode() => IsReaderMode = !IsReaderMode;

    [RelayCommand]
    public void IncreaseFontSize()
    {
        EditorFontSize = Math.Clamp(EditorFontSize + 2, 12, 32);
    }

    [RelayCommand]
    public void DecreaseFontSize()
    {
        EditorFontSize = Math.Clamp(EditorFontSize - 2, 12, 32);
    }

    [RelayCommand]
    public void ToggleStoryDetails() => IsStoryDetailsExpanded = !IsStoryDetailsExpanded;

    [RelayCommand]
    public async Task SaveStoryDetailsAsync()
    {
        if (SelectedStory == null || string.IsNullOrWhiteSpace(StoryTitleEdit)) return;
        SelectedStory.Title = StoryTitleEdit.Trim();
        SelectedStory.StyleNotes = StoryStyleNotesEdit?.Trim() ?? string.Empty;
        await _repository.UpdateStoryAsync(SelectedStory);
        IsStoryDetailsExpanded = false;
        SaveStatusText = $"บันทึกข้อมูลเรื่อง \"{SelectedStory.Title}\" แล้ว";
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
        FilteredChapters.Clear();
        SelectedChapter = null;
        OriginalText = string.Empty;
        TranslatedText = string.Empty;

        if (value != null)
        {
            StoryTitleEdit = value.Title;
            StoryStyleNotesEdit = value.StyleNotes;

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

            RefreshFilteredChapters();

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
        else
        {
            StoryTitleEdit = string.Empty;
            StoryStyleNotesEdit = string.Empty;
        }
    }

    partial void OnChapterSearchFilterChanged(string value) => RefreshFilteredChapters();
    partial void OnChapterStatusFilterIndexChanged(int value) => RefreshFilteredChapters();

    private void RefreshFilteredChapters()
    {
        var current = SelectedChapter;
        string q = ChapterSearchFilter?.Trim() ?? string.Empty;
        IEnumerable<Chapter> matches = Chapters;

        if (!string.IsNullOrEmpty(q))
        {
            matches = matches.Where(c => c.Title.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        matches = ChapterStatusFilterIndex switch
        {
            1 => matches.Where(c => c.Status == ChapterStatus.NotTranslated),
            2 => matches.Where(c => c.Status == ChapterStatus.Translated),
            3 => matches.Where(c => c.Status == ChapterStatus.Edited),
            _ => matches
        };

        FilteredChapters.Clear();
        foreach (var c in matches)
        {
            FilteredChapters.Add(c);
        }

        if (current != null && FilteredChapters.Contains(current) && SelectedChapter != current)
        {
            SelectedChapter = current;
        }

        UpdateStoryProgress();
    }

    private void UpdateStoryProgress()
    {
        int total = Chapters.Count;
        if (total == 0)
        {
            StoryProgressText = "0 / 0 ตอน (0%)";
            StoryProgressPercentage = 0;
            return;
        }

        int done = Chapters.Count(c => c.Status is ChapterStatus.Translated or ChapterStatus.Edited);
        double pct = Math.Round((double)done / total * 100, 0);
        StoryProgressPercentage = pct;
        StoryProgressText = $"แปลแล้ว {done} / {total} ตอน ({pct:0}%)";
    }

    partial void OnSelectedChapterChanged(Chapter? value)
    {
        _isLoadingChapter = true;
        try
        {
            if (value != null)
            {
                // บันทึกตอนล่าสุด
                _settings.LastSelectedChapterId = value.Id;
                _settings.Save();

                ChapterTitleEdit = value.Title ?? string.Empty;
                OriginalText = value.OriginalText ?? string.Empty;
                TranslatedText = value.TranslatedText ?? string.Empty;
                UpdateChapterStatusDisplay(value.Status);
                UpdateWordAndParagraphCounts(OriginalText);
                TranslatedWordCount = Chunker.EstimateWordCount(TranslatedText);
                SaveStatusText = $"บันทึกแล้ว {value.UpdatedAt.ToLocalTime():HH:mm:ss}";
                SaveStateColor = "#4CC38A";
            }
            else
            {
                ChapterTitleEdit = string.Empty;
                OriginalText = string.Empty;
                TranslatedText = string.Empty;
                TranslatedWordCount = 0;
                ChapterStatusText = "";
                SaveStatusText = "";
            }
        }
        finally
        {
            _isLoadingChapter = false;
        }
    }

    private void MarkUnsavedChanges()
    {
        SaveStatusText = "ยังไม่ได้บันทึก";
        SaveStateColor = "#F59E0B";
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    partial void OnChapterTitleEditChanged(string value)
    {
        if (!_isLoadingChapter && SelectedChapter != null && !string.IsNullOrWhiteSpace(value) && SelectedChapter.Title != value)
        {
            SelectedChapter.Title = value;
            MarkUnsavedChanges();
        }
    }

    partial void OnOriginalTextChanged(string value)
    {
        UpdateWordAndParagraphCounts(value);
        if (!_isLoadingChapter && SelectedChapter != null && SelectedChapter.OriginalText != value)
        {
            SelectedChapter.OriginalText = value;
            MarkUnsavedChanges();
        }
    }

    partial void OnTranslatedTextChanged(string value)
    {
        TranslatedWordCount = Chunker.EstimateWordCount(value);
        if (!_isLoadingChapter && SelectedChapter != null && SelectedChapter.TranslatedText != value && !IsTranslating)
        {
            SelectedChapter.TranslatedText = value;
            SelectedChapter.Status = ChapterStatus.Edited;
            UpdateChapterStatusDisplay(SelectedChapter.Status);
            UpdateStoryProgress();
            MarkUnsavedChanges();
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
            ChapterStatus.Translating => "กำลังแปล",
            ChapterStatus.Translated => "แปลแล้ว",
            ChapterStatus.Edited => "แก้ไขแล้ว",
            _ => ""
        };
        ChapterStatusColor = status switch
        {
            ChapterStatus.NotTranslated => "#737373",
            ChapterStatus.Translating => "#F59E0B",
            ChapterStatus.Translated => "#4CC38A",
            ChapterStatus.Edited => "#3B82F6",
            _ => "#737373"
        };
    }

    // ================= คำสั่งควบคุมการแปล =================

    [RelayCommand]
    public async Task StartTranslationAsync()
    {
        await StartTranslationInternalAsync(forceRetranslate: false);
    }

    [RelayCommand]
    public async Task TranslateAllChaptersAsync()
    {
        if (SelectedStory == null || IsTranslating) return;

        var pending = Chapters.Where(c => c.Status == ChapterStatus.NotTranslated && !string.IsNullOrWhiteSpace(c.OriginalText)).ToList();
        if (pending.Count == 0)
        {
            ProgressText = "แปลครบทุกตอนแล้ว";
            return;
        }

        if (RequestConfirmDialog != null)
        {
            bool ok = await RequestConfirmDialog($"ต้องการแปลต่อเนื่องทุกตอนที่ยังไม่แปล จำนวน {pending.Count} ตอน ใช่หรือไม่?");
            if (!ok) return;
        }

        for (int i = 0; i < pending.Count; i++)
        {
            SelectedChapter = pending[i];
            ProgressText = $"กำลังแปลต่อเนื่อง {i + 1}/{pending.Count} · {SelectedChapter.Title}";
            await StartTranslationInternalAsync(forceRetranslate: false);

            // หากถูกกดหยุดหรือเกิด Error ให้หยุดคิวทันที
            if (HasError || SelectedChapter.Status != ChapterStatus.Translated)
            {
                break;
            }
        }
        UpdateStoryProgress();
    }

    [RelayCommand]
    public async Task RetranslateAllAsync()
    {
        if (SelectedChapter == null || string.IsNullOrWhiteSpace(OriginalText)) return;

        if (RequestConfirmDialog != null)
        {
            bool ok = await RequestConfirmDialog("คุณต้องการล้างคำแปลเดิมแล้วแปลใหม่ทั้งหมดสำหรับตอนนี้ใช่หรือไม่?");
            if (!ok) return;
        }

        await StartTranslationInternalAsync(forceRetranslate: true);
    }

    private void StartFakeProgressTimer(double basePercent, double spanPercent)
    {
        _fakeProgressTimer?.Stop();
        double currentSubProgress = 0.0;
        _fakeProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _fakeProgressTimer.Tick += (_, _) =>
        {
            currentSubProgress += (42.0 - currentSubProgress) * 0.08;
            TranslationProgressPercentage = Math.Min(45.0, basePercent + (spanPercent * (currentSubProgress / 100.0)));
        };
        _fakeProgressTimer.Start();
    }

    private void StopFakeProgressTimer()
    {
        _fakeProgressTimer?.Stop();
        _fakeProgressTimer = null;
    }

    private async Task StartTranslationInternalAsync(bool forceRetranslate)
    {
        if (SelectedStory == null || SelectedChapter == null) return;
        if (string.IsNullOrWhiteSpace(OriginalText)) return;

        // ซิงค์ข้อความต้นฉบับล่าสุดเข้า Chapter ทันที
        SelectedChapter.OriginalText = OriginalText;
        if (forceRetranslate)
        {
            SelectedChapter.TranslatedText = string.Empty;
            TranslatedText = string.Empty;
        }
        else
        {
            // แสดงเฉพาะคำแปลเดิมของตอนนี้เท่านั้น (ถ้ายังไม่มีก็จะเป็นค่าว่าง)
            TranslatedText = SelectedChapter.TranslatedText ?? string.Empty;
        }

        IsTranslating = true;
        HasError = false;
        LastErrorDetails = string.Empty;
        TranslationProgressPercentage = 0;
        IsTranslatingIndeterminate = false;
        _translationCts = new CancellationTokenSource();

        StartFakeProgressTimer(0, 100.0);

        var llmClient = new LlmClient(_settings);
        var pipeline = new TranslationPipeline(_repository, llmClient, _settings);

        int lastReportedChunk = 0;
        var progress = new Progress<TranslationProgressInfo>(info =>
        {
            if (!string.IsNullOrEmpty(info.CombinedTranslatedTextSoFar))
            {
                TranslatedText = info.CombinedTranslatedTextSoFar;
            }
            if (!string.IsNullOrEmpty(info.StatusMessage))
            {
                ProgressText = info.StatusMessage;
            }

            int total = Math.Max(1, info.TotalChunks);
            int currentChunk = Math.Clamp(info.CurrentChunkIndex, 1, total);
            double chunkBase = ((double)(currentChunk - 1) / total) * 100.0;
            double chunkSpan = 100.0 / total;

            if (currentChunk != lastReportedChunk)
            {
                lastReportedChunk = currentChunk;
                StartFakeProgressTimer(chunkBase, chunkSpan);
            }

            if (info.StreamedWordCount > 0)
            {
                StopFakeProgressTimer();
                double withinChunk = 50.0 + Math.Min(45.0, (info.StreamedWordCount / 200.0) * 45.0);
                double targetPercent = chunkBase + (chunkSpan * (withinChunk / 100.0));
                TranslationProgressPercentage = Math.Max(TranslationProgressPercentage, targetPercent);
            }
        });

        try
        {
            ProgressText = forceRetranslate ? "กำลังแปลใหม่ทั้งหมดตั้งแต่เริ่มต้น..." : "กำลังเริ่มขั้นตอนการแปล...";
            await Task.Run(async () =>
            {
                await pipeline.TranslateChapterAsync(SelectedStory, SelectedChapter, progress, forceRetranslate, _translationCts.Token);
            });
            TranslatedText = SelectedChapter.TranslatedText ?? string.Empty;
            UpdateChapterStatusDisplay(SelectedChapter.Status);
            UpdateStoryProgress();
            StopFakeProgressTimer();
            ProgressText = "แปลเสร็จสิ้น";
            TranslationProgressPercentage = 100;
            SaveStatusText = $"บันทึกอัตโนมัติ {DateTime.Now:HH:mm:ss}";
            SaveStateColor = "#4CC38A";
        }
        catch (OperationCanceledException)
        {
            StopFakeProgressTimer();
            ProgressText = "หยุดการแปลแล้ว";
            SelectedChapter.Status = ChapterStatus.Edited;
            UpdateChapterStatusDisplay(SelectedChapter.Status);
        }
        catch (Exception ex)
        {
            StopFakeProgressTimer();
            HasError = true;
            LastErrorDetails = ex.ToString();
            string firstLine = ex.Message.Split('\n')[0];
            ProgressText = $"ข้อผิดพลาด · {firstLine}";
        }
        finally
        {
            StopFakeProgressTimer();
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
            SaveStatusText = "คัดลอกรายละเอียดข้อผิดพลาดแล้ว";
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
            ProgressText = "แปลซ้ำย่อหน้านี้แล้ว";
            // แจ้งเตือนผู้ใช้ให้นำผลแปลไปวาง
            if (RequestPromptDialog != null)
            {
                await RequestPromptDialog("ผลการแปลซ้ำย่อหน้าที่เลือก", newTranslation);
            }
        }
        catch (Exception ex)
        {
            ProgressText = $"แปลซ้ำไม่สำเร็จ · {ex.Message}";
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

        // บันทึกตอนปัจจุบันก่อนถ้ามี
        if (SelectedChapter != null)
        {
            await SaveCurrentChapterInternalAsync(isAutoSave: false);
        }

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
        RefreshFilteredChapters();
        SelectedChapter = chapter;

        // ล้างค่าบนหน้าจอให้ว่างเปล่าสำหรับตอนใหม่ทันที
        OriginalText = string.Empty;
        TranslatedText = string.Empty;
        ProgressText = string.Empty;
        TranslationProgressPercentage = 0;
        HasError = false;
        LastErrorDetails = string.Empty;
        UpdateChapterStatusDisplay(ChapterStatus.NotTranslated);
        SaveStatusText = "ตอนใหม่ (ยังไม่มีข้อความ)";
    }

    [RelayCommand]
    public async Task InsertChapterBeforeAsync()
    {
        if (SelectedStory == null) return;
        int targetIndex = SelectedChapter != null ? Chapters.IndexOf(SelectedChapter) : 0;
        if (targetIndex < 0) targetIndex = 0;

        int newOrdinal = targetIndex + 1;
        var chapter = new Chapter
        {
            StoryId = SelectedStory.Id,
            Ordinal = newOrdinal,
            Title = $"ตอนที่ {newOrdinal}",
            OriginalText = string.Empty,
            TranslatedText = string.Empty,
            Status = ChapterStatus.NotTranslated
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        Chapters.Insert(targetIndex, chapter);
        await ResequenceOrdinalsAsync();
        RefreshFilteredChapters();
        SelectedChapter = chapter;

        OriginalText = string.Empty;
        TranslatedText = string.Empty;
        ProgressText = string.Empty;
        TranslationProgressPercentage = 0;
        HasError = false;
        LastErrorDetails = string.Empty;
        UpdateChapterStatusDisplay(ChapterStatus.NotTranslated);
        SaveStatusText = $"แทรกตอนใหม่ก่อนหน้าแล้ว";
    }

    [RelayCommand]
    public async Task InsertChapterAfterAsync()
    {
        if (SelectedStory == null) return;
        int targetIndex = SelectedChapter != null ? Chapters.IndexOf(SelectedChapter) + 1 : Chapters.Count;
        if (targetIndex > Chapters.Count || targetIndex < 0) targetIndex = Chapters.Count;

        int newOrdinal = targetIndex + 1;
        var chapter = new Chapter
        {
            StoryId = SelectedStory.Id,
            Ordinal = newOrdinal,
            Title = $"ตอนที่ {newOrdinal}",
            OriginalText = string.Empty,
            TranslatedText = string.Empty,
            Status = ChapterStatus.NotTranslated
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        Chapters.Insert(targetIndex, chapter);
        await ResequenceOrdinalsAsync();
        RefreshFilteredChapters();
        SelectedChapter = chapter;

        OriginalText = string.Empty;
        TranslatedText = string.Empty;
        ProgressText = string.Empty;
        TranslationProgressPercentage = 0;
        HasError = false;
        LastErrorDetails = string.Empty;
        UpdateChapterStatusDisplay(ChapterStatus.NotTranslated);
        SaveStatusText = $"แทรกตอนใหม่ต่อจากนี้แล้ว";
    }

    [RelayCommand]
    public void StartRenameChapter(Chapter? chapter)
    {
        chapter ??= SelectedChapter;
        if (chapter == null) return;

        foreach (var c in Chapters)
        {
            if (c != chapter && c.IsEditingTitle)
            {
                c.IsEditingTitle = false;
            }
        }

        chapter.EditingTitle = chapter.Title;
        chapter.IsEditingTitle = true;
    }

    [RelayCommand]
    public async Task CommitRenameChapterAsync(Chapter? chapter)
    {
        chapter ??= SelectedChapter;
        if (chapter == null || !chapter.IsEditingTitle) return;

        string newTitle = chapter.EditingTitle?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(newTitle) && newTitle != chapter.Title)
        {
            chapter.Title = newTitle;
            await _repository.UpdateChapterAsync(chapter);
            RefreshFilteredChapters();
            if (chapter == SelectedChapter)
            {
                ChapterTitleEdit = newTitle;
            }
        }

        chapter.IsEditingTitle = false;
    }

    [RelayCommand]
    public void CancelRenameChapter(Chapter? chapter)
    {
        chapter ??= SelectedChapter;
        if (chapter == null) return;
        chapter.EditingTitle = chapter.Title;
        chapter.IsEditingTitle = false;
    }

    [RelayCommand]
    public async Task AddMultipleChaptersAsync()
    {
        if (SelectedStory == null) return;
        if (RequestPromptDialog == null) return;

        string? input = await RequestPromptDialog("เพิ่มหลายตอนพร้อมกัน", "กรุณาระบุจำนวนตอนที่ต้องการเพิ่ม (1-100):");
        if (string.IsNullOrWhiteSpace(input) || !int.TryParse(input.Trim(), out int count) || count <= 0)
        {
            return;
        }
        if (count > 200) count = 200;

        int startOrdinal = Chapters.Count > 0 ? Chapters.Max(c => c.Ordinal) + 1 : 1;
        Chapter? firstNew = null;

        for (int i = 0; i < count; i++)
        {
            int ord = startOrdinal + i;
            var chapter = new Chapter
            {
                StoryId = SelectedStory.Id,
                Ordinal = ord,
                Title = $"ตอนที่ {ord}",
                OriginalText = string.Empty,
                TranslatedText = string.Empty,
                Status = ChapterStatus.NotTranslated
            };
            chapter.Id = await _repository.InsertChapterAsync(chapter);
            Chapters.Add(chapter);
            firstNew ??= chapter;
        }

        await ResequenceOrdinalsAsync();
        RefreshFilteredChapters();
        if (firstNew != null)
        {
            SelectedChapter = firstNew;
        }
        SaveStatusText = $"เพิ่ม {count} ตอนใหม่แล้ว";
    }

    [RelayCommand]
    public void ToggleSelectAllChapters()
    {
        if (FilteredChapters.Count == 0) return;
        bool allChecked = FilteredChapters.All(c => c.IsSelected);
        if (allChecked)
        {
            RequestClearChapterSelection?.Invoke();
            foreach (var c in FilteredChapters)
            {
                c.IsSelected = false;
            }
        }
        else
        {
            RequestSelectAllChapters?.Invoke();
            foreach (var c in FilteredChapters)
            {
                c.IsSelected = true;
            }
        }
    }

    [RelayCommand]
    public async Task DeleteChapterAsync()
    {
        if (RequestConfirmDialog == null) return;

        var checkedChapters = Chapters.Where(c => c.IsSelected).ToList();
        if (checkedChapters.Count > 1)
        {
            bool ok = await RequestConfirmDialog($"คุณแน่ใจหรือไม่ว่าต้องการลบตอนที่เลือกทั้งหมด {checkedChapters.Count} ตอน?");
            if (!ok) return;

            foreach (var ch in checkedChapters)
            {
                await _repository.DeleteChapterAsync(ch.Id);
                Chapters.Remove(ch);
            }
            await ResequenceOrdinalsAsync();
            RefreshFilteredChapters();
            SelectedChapter = FilteredChapters.FirstOrDefault() ?? Chapters.FirstOrDefault();
            return;
        }

        var target = checkedChapters.FirstOrDefault() ?? SelectedChapter;
        if (target == null) return;

        bool singleOk = await RequestConfirmDialog($"คุณแน่ใจหรือไม่ว่าต้องการลบตอน \"{target.Title}\"?");
        if (!singleOk) return;

        await _repository.DeleteChapterAsync(target.Id);
        Chapters.Remove(target);
        await ResequenceOrdinalsAsync();
        RefreshFilteredChapters();
        SelectedChapter = FilteredChapters.FirstOrDefault() ?? Chapters.FirstOrDefault();
    }

    public async Task ReorderChaptersAsync(IList<Chapter> movingChapters, Chapter targetChapter, bool dropAfter)
    {
        if (movingChapters == null || movingChapters.Count == 0 || targetChapter == null) return;
        if (movingChapters.Contains(targetChapter)) return;

        var selectedIds = Chapters.Where(c => c.IsSelected).Select(c => c.Id).ToHashSet();
        foreach (var m in movingChapters)
        {
            selectedIds.Add(m.Id);
        }

        // กรองเอาเฉพาะตอนที่มีอยู่ใน Chapters และเรียงลำดับตาม Chapters เดิม
        var toMove = Chapters.Where(c => movingChapters.Contains(c)).ToList();
        if (toMove.Count == 0) return;

        // นำ moving chapters ออกจากรายการชั่วคราว
        foreach (var c in toMove)
        {
            Chapters.Remove(c);
        }

        int targetIdx = Chapters.IndexOf(targetChapter);
        if (targetIdx < 0)
        {
            targetIdx = Chapters.Count;
        }
        else if (dropAfter)
        {
            targetIdx++;
        }

        if (targetIdx > Chapters.Count) targetIdx = Chapters.Count;

        for (int i = 0; i < toMove.Count; i++)
        {
            Chapters.Insert(targetIdx + i, toMove[i]);
        }

        await ResequenceOrdinalsAsync();
        RefreshFilteredChapters();

        // คงสถานะเลือกให้กับตอนเดิมและตอนที่เพิ่งถูกย้าย
        foreach (var c in Chapters)
        {
            c.IsSelected = selectedIds.Contains(c.Id);
        }
        if (toMove.Count > 0)
        {
            SelectedChapter = toMove[0];
        }
    }

    public async Task ResequenceOrdinalsAsync()
    {
        var updates = new List<(long Id, int Ordinal)>();
        for (int i = 0; i < Chapters.Count; i++)
        {
            int newOrd = i + 1;
            if (Chapters[i].Ordinal != newOrd)
            {
                Chapters[i].Ordinal = newOrd;
                updates.Add((Chapters[i].Id, newOrd));
            }
        }
        if (updates.Count > 0)
        {
            await _repository.UpdateChapterOrdinalsAsync(updates);
        }
    }

    [RelayCommand]
    public void PreviousChapter()
    {
        if (SelectedChapter == null || Chapters.Count == 0) return;
        int idx = Chapters.IndexOf(SelectedChapter);
        if (idx > 0)
        {
            SelectedChapter = Chapters[idx - 1];
        }
    }

    [RelayCommand]
    public void NextChapter()
    {
        if (SelectedChapter == null || Chapters.Count == 0) return;
        int idx = Chapters.IndexOf(SelectedChapter);
        if (idx >= 0 && idx < Chapters.Count - 1)
        {
            SelectedChapter = Chapters[idx + 1];
        }
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

        var current = SelectedChapter;
        Chapters.Move(index, index - 1);
        RefreshFilteredChapters();
        SelectedChapter = current;
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

        var current = SelectedChapter;
        Chapters.Move(index, index + 1);
        RefreshFilteredChapters();
        SelectedChapter = current;
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

        SaveStatusText = "กำลังบันทึก";
        SaveStateColor = "#F59E0B";

        try
        {
            SelectedChapter.OriginalText = OriginalText;
            SelectedChapter.TranslatedText = TranslatedText;
            await _repository.UpdateChapterAsync(SelectedChapter);

            SaveStatusText = isAutoSave
                ? $"บันทึกอัตโนมัติ {DateTime.Now:HH:mm:ss}"
                : $"บันทึกแล้ว {DateTime.Now:HH:mm:ss}";
            SaveStateColor = "#4CC38A";
        }
        catch (Exception ex)
        {
            SaveStatusText = $"บันทึกล้มเหลว · {ex.Message}";
            SaveStateColor = "#E5484D";
        }
    }

    [RelayCommand]
    public async Task ImportTxtFilesAsync()
    {
        if (SelectedStory == null || RequestOpenFilesDialog == null) return;

        var files = await RequestOpenFilesDialog("เลือกไฟล์ .txt ต้นฉบับ");
        if (files == null || files.Length == 0) return;

        int nextOrdinal = Chapters.Count > 0 ? Chapters.Max(c => c.Ordinal) + 1 : 1;
        foreach (var path in files.OrderBy(f => f))
        {
            var chap = await TextExportImportService.ImportChapterFromFileAsync(path, SelectedStory.Id, nextOrdinal++);
            chap.Id = await _repository.InsertChapterAsync(chap);
            Chapters.Add(chap);
        }

        RefreshFilteredChapters();
        SaveStatusText = $"นำเข้าแล้ว {files.Length} ตอน";
    }

    [RelayCommand]
    public async Task ExportCurrentChapterAsync()
    {
        if (SelectedChapter == null || RequestSaveFileDialog == null) return;
        string? path = await RequestSaveFileDialog($"{SelectedChapter.Title}.txt");
        if (string.IsNullOrEmpty(path)) return;

        await TextExportImportService.ExportChapterAsync(path, SelectedChapter);
        SaveStatusText = "ส่งออกตอนนี้แล้ว";
    }

    [RelayCommand]
    public async Task ExportStoryAsync()
    {
        if (SelectedStory == null || RequestSaveFileDialog == null) return;
        string? path = await RequestSaveFileDialog($"{SelectedStory.Title}.txt");
        if (string.IsNullOrEmpty(path)) return;

        await TextExportImportService.ExportStorySingleFileAsync(path, SelectedStory, Chapters);
        SaveStatusText = "ส่งออกทั้งเรื่องแล้ว";
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
        SaveStatusText = "สำรองฐานข้อมูลแล้ว";
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
        SaveStatusText = "กู้คืนฐานข้อมูลแล้ว";
    }
}
